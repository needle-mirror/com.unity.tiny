using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Tiny.Assertions;
using System;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Tiny.Rendering
{
    public struct LightMatrices : IComponentData
    {
        public float4x4 projection;
        public float4x4 view;
        public float4x4 mvp;
        public Frustum frustum;
    }

    public struct Light : IComponentData
    {
        // always points in z direction 
        public float clipZNear; // near clip, applies only for mapped lights 
        public float clipZFar;
        
        public float intensity;
        public float3 color;
        // if no other components are not to a light, it's a simple non-shadowed omni light
    }

    public struct ShadowmappedLight : IComponentData // next to light
    {
        public int shadowMapResolution;     // for auto creation, this is the texture resolution, so if there are multiple cascades in the map this includes all of them 
        public Entity shadowMap;            // the shadow map texture
        public Entity shadowMapRenderNode;  // node used for shadow map creation
    }

    public struct CascadeShadowmappedLight : IComponentData // next to light and ShadowMappedLight, AutoMovingDirectionalLight, and DirectionalLight
    {
        public float3 cascadeScale;      // The four cascades are scaled according to these weights, the largest cascade has an implicit weight of 1
                                         // 1>x>y>z>0. z is the scale of the highest detail cascade.
        public float cascadeBlendWidth;  // Blend width for blending between cascades: 0=no blending, 1=maximum blending 
        public Entity camera;            // The camera this cascade is computed from - must match the camera rendering the shadows 
    }

    public struct CascadeData
    {
        public float4x4 view;
        public float4x4 proj;
        public Frustum frustum;
        public float2 offset;
        public float scale;
    }

    public struct CascadeShadowmappedLightCache : IComponentData // next to CascadeShadowmappedLight
    {
        public CascadeData c0;
        public CascadeData c1;
        public CascadeData c2;
        public CascadeData c3;

        public CascadeData GetCascadeData(int idx)
        {
            switch ( idx ) {
                default: Assert.IsTrue(idx==0); return c0;
                case 1: return c1;
                case 2: return c2;
                case 3: return c3;
            }
        }

        public void SetCascadeData(int idx, in CascadeData cd)
        {
            switch ( idx ) {
                case 0: c0 = cd; break;
                case 1: c1 = cd; break;
                case 2: c2 = cd; break;
                case 3: c3 = cd; break;
                default: Assert.IsTrue(false); break;
            }
        }
    }

    public struct SpotLight : IComponentData // next to light
    {
        // always points in z direction
        public float fov; // in degrees 
        public float innerRadius; // [0..1[, start of circle falloff 1=sharp circle, 0=smooth, default 0
        public float ratio; // ]0..1] 1=circle, 0=line, default 1
    }

    public struct DirectionalLight : IComponentData // next to light
    {
    }

    public struct LightMask : IComponentData // next to light or lit renderer
    {
        // light affects renderer if the light mask on the light entity AND light mask on the renderer !=0 
        // if the mask component is missing it implies a mask with all bits set
        public ulong Value;
    }

    // This component automatically updates a directional lights position & size 
    // so the shadow map covers the intersection of the bounds of interest and the cameras frustum
    // because it changes the size and position of the directional light it is not suitable for projection textures in the light
    // Also requires a NonUniformScale component next to it
    public struct AutoMovingDirectionalLight : IComponentData // next to mapped directional light 
    {
        public AABB bounds;                 // bounds of the world to track (world space)
        public bool autoBounds;             // automatically get bounds from world bounds of renderers
        public Entity clipToCamera;         // if not Entity.Null, clip the shadow receivers bounds to the frustum of the camera
                                            // entity here. The entity pointed to here must have a Frustum component.
        public AABB boundsClipped;          // set to the clipped receiver bounds if clipToCamera is set (world space)
    }
    
    // mesh renderers grab lighting setups from this entity
    public struct LightingRef : ISharedComponentData
    {
        public Entity e;
    }

    // a group of lights, referenced via LightingRef
    // next to this component should be the platform lighting setup, like LightingBGFX 
    public struct LightingSetup : IComponentData, IEquatable<LightingSetup>
    {
        public const int maxPointOrDirLights = 8;
        public const int maxMappedLights = 2;
        public const int maxCsmLights = 1;

        // this is pretty hard limited to what tiny rendering can do, but can be expanded in the future 
        public Entity CSMLight; // directional
        public Entity MappedLight0; // spot or directional
        public Entity MappedLight1; // spot or directional
        public FixedList32<Entity> PlainLights; // point or directional, no shadows
        public Entity AmbientLight;
        public Entity Fog;
        public ulong EntityMask; // lightmask this setup applies to 

        public bool Equals(LightingSetup other)
        {
            if (CSMLight != other.CSMLight) return false;
            if (MappedLight0 != other.MappedLight0) return false;
            if (MappedLight1 != other.MappedLight1) return false;
            if (PlainLights.Length != other.PlainLights.Length) return false;
            for (int i=0; i<PlainLights.Length; i++)
                if (PlainLights[i] != other.PlainLights[i]) return false;
            if (AmbientLight != other.AmbientLight) return false;
            if (Fog != other.Fog) return false;
            if (EntityMask != other.EntityMask) return false;
            return true;
        }
    }

    /// <summary>
    /// Ambient light. To add next to entity with a Light component on it.
    /// The ambient light color and intensity must be set in the Light Component.
    /// </summary>
    public struct AmbientLight : IComponentData { }

    public struct Fog : IComponentData
    {
        public enum Mode
        {
            None = 0,
            Linear = 1,
            Exponential = 2,
            ExponentialSquared = 4
        }

        public Mode mode;
        public float4 color;
        public float density;
        public float startDistance;
        public float endDistance;

        public bool Equals(Fog other)
        {
            if (mode != other.mode) return false;
            if (mode == Mode.None ) return true;
            if (math.any(color != other.color)) return false;
            if (density != other.density) return false;
            if (startDistance != other.startDistance) return false;
            if (endDistance != other.endDistance) return false;
            return true;
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(UpdateLightMatricesSystem))]
    [UpdateAfter(typeof(UpdateWorldBoundsSystem))]
    public class AssignLightingSetupTrivialSystem : SystemBase
    {
        // Easiest choice for trivial light assignment:
        // Assign all lights to all lit renderers!
        // If there are too many, throw an error
        // This is the same as the old RenderGraphBuilder.
        // New is that it does handle renderers or lights being added and removed. 
        // There is only ever one LightingSetup in the scene. 
        NativeList<SetupAndMask> m_lightingSetupPerMask;
        NativeULongSet m_uniqueMasksEntities;

        struct SetupAndMask {
            public Entity eSetup;
            public ulong entitiesMask;
            public bool changed;
        }

        protected override void OnCreate()
        {
            m_uniqueMasksEntities = new NativeULongSet(256, Allocator.Persistent);
            m_lightingSetupPerMask = new NativeList<SetupAndMask>(256, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            m_uniqueMasksEntities.Dispose();
            m_lightingSetupPerMask.Dispose();
        }

        protected override void OnUpdate()
        {
            // find the light masks needed 
            m_uniqueMasksEntities.Clear();
            m_uniqueMasksEntities.Add(ulong.MaxValue);
            var uniqueMasksEntities = m_uniqueMasksEntities;
            Entities.WithAll<LitMeshRenderer>().ForEach((Entity e, ref LightMask lm) => {
                uniqueMasksEntities.Add(lm.Value);
            }).Run();

            var masks = uniqueMasksEntities.ValuesAsArray();
            bool anyEntMasksChanged = false;
            for ( int i=0; i<masks.Length; i++ ) {
                if ( i>=m_lightingSetupPerMask.Length ) {
                    Entity eSetup = EntityManager.CreateEntity(ComponentType.ReadWrite<LightingSetup>());
                    anyEntMasksChanged = true;
                    m_lightingSetupPerMask.Add(new SetupAndMask {
                        eSetup = eSetup,
                        entitiesMask = masks[i],
                        changed = true
                    });
                } else {
                    if ( m_lightingSetupPerMask[i].entitiesMask != masks[i] ) {
                        anyEntMasksChanged = true;
                        Entity eSetup = m_lightingSetupPerMask[i].eSetup;
                        m_lightingSetupPerMask[i] = new SetupAndMask {
                            eSetup = eSetup,
                            entitiesMask = masks[i],
                            changed = true
                        };
                    }
                }
            }
            if (m_lightingSetupPerMask.Length > masks.Length)
                m_lightingSetupPerMask.ResizeUninitialized(masks.Length);

            for ( int i=0; i<m_lightingSetupPerMask.Length; i++ ) { 
                ulong entMask = m_lightingSetupPerMask[i].entitiesMask;
                // always add all lights to the the lighting setup. 
                // there are few enough we don't have to care about change tracking
                // throw an error, if there are too many 
                LightingSetup setup = default;
                setup.EntityMask = entMask;
                ComponentDataFromEntity<LightMask> lightMaskCDFE = GetComponentDataFromEntity<LightMask>(true);
                Entities.WithNone<DisableRendering>().WithAll<Light, DirectionalLight, CascadeShadowmappedLight>().WithAll<LocalToWorld>().ForEach((Entity e)=>{
                    if ( lightMaskCDFE.HasComponent(e) ) 
                        if ((entMask & lightMaskCDFE[e].Value) == 0) 
                            return;
                    Assert.IsTrue ( setup.CSMLight==Entity.Null, "More than one CascadeShadowmappedLight loaded. Using more than one CascadeShadowmappedLight is currently not supported.");
                    setup.CSMLight = e;
                }).Run();
                Entities.WithNone<DisableRendering>().WithAll<Light, ShadowmappedLight, LocalToWorld>().WithAny<SpotLight, DirectionalLight>().WithNone<CascadeShadowmappedLight>().ForEach((Entity e)=>{
                    if ( lightMaskCDFE.HasComponent(e) ) 
                        if ((entMask & lightMaskCDFE[e].Value) == 0) 
                            return;
                    Assert.IsTrue ( setup.MappedLight1==Entity.Null, "More than two shadow mapped lights loaded. Using more than two shadow mapped lights is currently not supported.");
                    if ( setup.MappedLight0==Entity.Null )
                        setup.MappedLight0 = e;
                    else
                        setup.MappedLight1 = e;
                }).Run();
                Entities.WithNone<DisableRendering>().WithAll<Light, LocalToWorld>().WithNone<SpotLight, ShadowmappedLight, CascadeShadowmappedLight>().ForEach((Entity e)=>{
                    if ( lightMaskCDFE.HasComponent(e) ) 
                        if ((entMask & lightMaskCDFE[e].Value) == 0) 
                            return;
                    if ( setup.PlainLights.Length<8 ) {
                        setup.PlainLights.Add(e);
                    } else {
                        Assert.IsTrue ( false, "Too many non-shadow mapped lights loaded. Using more than eight non-shadow mapped lights at once is currently not supported.");
                    }
                }).Run();
                float3 prevAmbient = new float3(); 
                Entities.WithoutBurst().WithAll<AmbientLight>().ForEach((Entity e, in Light l)=>{
                    if ( lightMaskCDFE.HasComponent(e) ) 
                        if ((entMask & lightMaskCDFE[e].Value) == 0) 
                            return;
                    float3 newAmbient = new float3(l.color * l.intensity);
                    if ( setup.AmbientLight!=Entity.Null ) {
                        // this should throw, but work around for multiple subscenes conversion for now
                        if ( math.any(prevAmbient != newAmbient)) {
                            RenderDebug.Log ( "Warning: Multiple, different, ambient lights in scene! Selecting the brightest one.");
                            if ( math.lengthsq(prevAmbient) < math.lengthsq(newAmbient) ) {
                                setup.AmbientLight = e;
                                prevAmbient = newAmbient;
                            }
                        }
                    } else { 
                        setup.AmbientLight = e;
                        prevAmbient = newAmbient;
                    }
                }).Run();
                Fog prevFog = new Fog(); 
                Entities.WithoutBurst().WithNone<DisableRendering>().WithAll<Fog>().ForEach((Entity e, in Fog f)=>{
                    if ( lightMaskCDFE.HasComponent(e) ) 
                        if ((entMask & lightMaskCDFE[e].Value) == 0) 
                            return;
                    if ( setup.Fog!=Entity.Null ) {
                        if ( !prevFog.Equals(f) ) {
                            RenderDebug.Log ( "Warning: Multiple, different, fog settings in scene! Picked the first enabled one encountered.");
                            // this should throw, but work around for multiple subscenes conversion for now
                            // Assert.IsTrue (false, "Too many fog components loaded. Using more than one fog component is currently not supported.");
                            if ( prevFog.mode == Fog.Mode.None && f.mode != Fog.Mode.None ) {
                                setup.Fog = e;
                                prevFog = f;
                            }
                        }
                    } else { 
                        setup.Fog = e;
                    }
                }).Run();
                var prevSetup = EntityManager.GetComponentData<LightingSetup>(m_lightingSetupPerMask[i].eSetup);
                if ( !prevSetup.Equals(setup) ) {
                    EntityManager.SetComponentData(m_lightingSetupPerMask[i].eSetup, setup);
                    var pL = m_lightingSetupPerMask[i];
                    pL.changed = true;
                    m_lightingSetupPerMask[i] = pL;
                    anyEntMasksChanged = true;
                }
            }

            var lspm = m_lightingSetupPerMask[uniqueMasksEntities.GetIndex(ulong.MaxValue)];
            if ( anyEntMasksChanged ) {
                // go through LitMeshRenderers and set the shared component to the new value                
                if ( lspm.changed ) {
                    Entities.WithStructuralChanges().WithoutBurst().WithNone<LightMask>().WithAny<LitMeshRenderer, LitParticleRenderer, SkinnedMeshRenderer>().WithAll<LightingRef>().ForEach((Entity e) => {
                        EntityManager.SetSharedComponentData(e, new LightingRef {  e = lspm.eSetup });
                    }).Run();
                }
                Entities.WithStructuralChanges().WithoutBurst().WithAny<LitMeshRenderer, LitParticleRenderer, SkinnedMeshRenderer>().WithAll<LightingRef>().ForEach((Entity e, ref LightMask lm) =>
                {
                    var lspmMasked = m_lightingSetupPerMask[uniqueMasksEntities.GetIndex(lm.Value)];
                    Assert.IsTrue(lspm.entitiesMask == lm.Value);
                    if ( lspmMasked.changed )
                        EntityManager.SetSharedComponentData(e, new LightingRef {  e = lspmMasked.eSetup });
                }).Run();
            }

            // go through LitMeshRenderers and add the shared component if they do not have it 
            Entities.WithStructuralChanges().WithoutBurst().WithAny<LitMeshRenderer, SkinnedMeshRenderer, LitParticleRenderer>().WithNone<LightingRef,LightMask>().ForEach((Entity e) => {
                EntityManager.AddSharedComponentData(e, new LightingRef {  e = lspm.eSetup });
            }).Run();
            Entities.WithStructuralChanges().WithoutBurst().WithAny<LitMeshRenderer, SkinnedMeshRenderer, LitParticleRenderer>().WithNone<LightingRef>().ForEach((Entity e, ref LightMask lm) => {
                EntityManager.AddSharedComponentData(e, new LightingRef {  e = lspm.eSetup });
                var lspmMasked = m_lightingSetupPerMask[uniqueMasksEntities.GetIndex(lm.Value)];
                Assert.IsTrue(lspm.entitiesMask == lm.Value);
                EntityManager.AddSharedComponentData(e, new LightingRef {  e = lspmMasked.eSetup });
            }).Run();

            // clean up changed mask
            int nchanged = 0;
            unsafe {
                SetupAndMask *ptr = (SetupAndMask*)m_lightingSetupPerMask.GetUnsafePtr();
                for ( int i=0; i<m_lightingSetupPerMask.Length; i++ ) {
                    if (ptr[i].changed) nchanged++;
                    ptr[i].changed = false;
                }
            }
            if (nchanged > 0)
                RenderDebug.LogFormatAlways("Changed {0} lighting setup(s).", nchanged);

        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(UpdateLightMatricesSystem))]
    [UpdateAfter(typeof(UpdateWorldBoundsSystem))]
    public class UpdateAutoMovingLightSystem : SystemBase
    {
        private AABB RotateBounds (ref float4x4 tx, ref AABB b)
        {
            WorldBounds wBounds;
            Culling.AxisAlignedToWorldBounds(in tx, in b, out wBounds);
            // now turn those bounds back to axis aligned.. 
            AABB aab;
            Culling.WorldBoundsToAxisAligned(in wBounds, out aab);
            return aab;
        }

        static bool ClipLinePlane(ref float3 p0, ref float3 p1, float4 plane)
        {
            bool p0inside = math.dot(plane.xyz, p0) >= -plane.w;
            bool p1inside = math.dot(plane.xyz, p1) >= -plane.w;
            if (!p0inside && !p1inside) 
                return false; // both outside
            if (p0inside && p1inside)
                return true; // both inside, no need to change p0 and p1 
            // clip 
            float3 dp = p1 - p0;
            float dp0 = math.dot(plane.xyz, p0);
            float dpd = math.dot(plane.xyz, dp);
            float t = -(plane.w + dp0) / dpd;
            if ( !(t>0.0f && t<1.0f) ) { // if dpd == 0, point on plane
                if (p0inside) p1 = p0;
                else p0 = p1;
                return true;
            }
            float3 p = p0 + t * dp;
            if (p0inside) p1 = p;
            else p0 = p;
            return true;
        }

        static unsafe int ClipLineFrustum(float3 p0, float3 p1, in Frustum f, float3 *dest)
        {
            for ( int i=0; i<f.PlanesCount; i++ ) {
                if (!ClipLinePlane(ref p0, ref p1, f.GetPlane(i)))
                    return 0;
            }
            dest[0] = p0;
            dest[1] = p1;
            return 2;
        }

        static AABB ClipAABBByFrustum(in AABB b, in Frustum f, in Camera cam, in float4x4 camTx) 
        {
            AABB r = default;

            float3 bMin = b.Min;
            float3 bMax = b.Max;
            unsafe
            {
                float3* insidePoints = stackalloc float3[48];
                int nInsidePoints = 0;
                // clip the 12 edge lines of the aab into the frustum, and add their end points
                // this is not optimal, but robust 
                for ( int i=0; i<Culling.EdgeTable.Length; i++ ) {
                    float3 p0 = Culling.SelectCoordsMinMax(bMin, bMax, Culling.EdgeTable[i]&7);
                    float3 p1 = Culling.SelectCoordsMinMax(bMin, bMax, Culling.EdgeTable[i]>>3);
                    nInsidePoints += ClipLineFrustum(p0, p1, in f, insidePoints + nInsidePoints);
                }
                // clip the 12 edge lines of the furstum into the aab, and add their end points 
                Frustum f2;
                ProjectionHelper.FrustumFromAABB(b, out f2);
                WorldBounds wb = UpdateCameraMatricesSystem.BoundsFromCamera(in cam);
                Culling.TransformWorldBounds(in camTx, ref wb);
                for ( int i=0; i<Culling.EdgeTable.Length; i++ ) {
                    float3 p0 = wb.GetVertex(Culling.EdgeTable[i]&7);
                    float3 p1 = wb.GetVertex(Culling.EdgeTable[i]>>3);
                    nInsidePoints += ClipLineFrustum(p0, p1, in f2, insidePoints + nInsidePoints);
                }
                if (nInsidePoints > 0) {
                    float3 bbMin = insidePoints[0];
                    float3 bbMax = bbMin;
                    for ( int i=1; i<nInsidePoints; i++ ) {
                        bbMin = math.min(insidePoints[i], bbMin);
                        bbMax = math.max(insidePoints[i], bbMax);
                    }
                    r.Center = (bbMax+bbMin)*.5f;
                    r.Extents = (bbMax-bbMin)*.5f;
                }
            }
            return r;
        }

        void AssignCascades(ref AutoMovingDirectionalLight amdl, ref LocalToWorld ltw, ref Rotation rx, ref CascadeShadowmappedLight csm, ref CascadeShadowmappedLightCache csmDest)
        {
            Assert.IsTrue(amdl.clipToCamera == csm.camera || amdl.clipToCamera == Entity.Null);
            Assert.IsTrue(csm.cascadeBlendWidth >= 0.0f && csm.cascadeBlendWidth <= 1.0f);
            Assert.IsTrue(0.0f < csm.cascadeScale.z && csm.cascadeScale.z < csm.cascadeScale.y && csm.cascadeScale.y < csm.cascadeScale.x && csm.cascadeScale.x < 1.0f);

            // this can happen if the camera for the csm gets destroyed - it can happen when unloading 
            if ( !EntityManager.HasComponent<LocalToWorld>(csm.camera) || !EntityManager.Exists(csm.camera) ) 
                return;

            var camTx = EntityManager.GetComponentData<LocalToWorld>(csm.camera);
            var invLight = math.inverse(ltw.Value);
            // transform camera to light space, that's where we want to have the most samples! 
            float3 camPos = math.transform(invLight, camTx.Value.c3.xyz);

            for (int cascadeIndex = 0; cascadeIndex < 4; cascadeIndex++) {
                float ratio = 1.0f;
                float2 useOffset = camPos.xy;
                switch (cascadeIndex) {
                    case 0:
                        //ratio = 1.0f;
                        useOffset = new float2(0);
                        break;
                    case 1:
                        ratio = csm.cascadeScale.x;
                        break;
                    case 2:
                        ratio = csm.cascadeScale.y;
                        break;
                    case 3: // highest res
                        ratio = csm.cascadeScale.z;
                        break;
                    default:
                        Assert.IsTrue(false);
                        break;
                }
                float invRatio = 1.0f / ratio;
                useOffset = useOffset * -invRatio;

                CascadeData cd = default;
                // this is used for RENDERING the cascade
                cd.proj = ProjectionHelper.ProjectionMatrixUnitOrthoOffset(useOffset, invRatio);
                cd.view = invLight;
                // this is used for SAMPLING the cascade
                cd.scale = invRatio;
                cd.offset = useOffset;
                ProjectionHelper.FrustumFromMatrices(cd.proj, cd.view, out cd.frustum);
                csmDest.SetCascadeData(cascadeIndex, cd);
            }
        }

        void AssignSimpleAutoBounds(ref AutoMovingDirectionalLight amdl, ref LocalToWorld ltw, ref Rotation rx, ref Translation tx, ref NonUniformScale sc) { 
            AABB bounds = amdl.bounds;
            if (amdl.clipToCamera!=Entity.Null && EntityManager.Exists(amdl.clipToCamera) ) {
                var camMatrices =  EntityManager.GetComponentData<CameraMatrices>(amdl.clipToCamera);
                var cam = EntityManager.GetComponentData<Camera>(amdl.clipToCamera);
                var camTx = EntityManager.GetComponentData<LocalToWorld>(amdl.clipToCamera);
                amdl.boundsClipped = ClipAABBByFrustum(in bounds, in camMatrices.frustum, in cam, in camTx.Value);
                //Assert.IsTrue(recvBounds.Contains(amdl.boundsClippedReceivers));
                bounds = amdl.boundsClipped;
            }

            // transform bounds into light space rotation
            float4x4 rotOnlyTx = new float4x4(rx.Value, new float3(0));
            float4x4 rotOnlyTxInv = new float4x4(math.inverse(rx.Value), new float3(0));

            AABB lsBounds = RotateBounds(ref rotOnlyTxInv, ref bounds);

            float3 posls;
            posls.x = lsBounds.Center.x;
            posls.y = lsBounds.Center.y;
            posls.z = lsBounds.Center.z - lsBounds.Extents.z;
            tx.Value = math.transform(rotOnlyTx, posls); // back to world space
            float size = math.max(lsBounds.Extents.x, lsBounds.Extents.y);
            sc.Value.x = size;
            sc.Value.y = size;
            sc.Value.z = lsBounds.Extents.z * 2.0f;

            // also write back to local to world, as it's going to get used later
            ltw.Value = math.mul ( new float4x4(rx.Value, tx.Value), float4x4.Scale(sc.Value) );
        }

        protected override void OnUpdate() 
        {
            Dependency.Complete();
#if DEBUG
            // debug check that csm lights are AutoMovingDirectionalLight
            Entities.WithoutBurst().WithNone<AutoMovingDirectionalLight>().WithAll<CascadeShadowmappedLight>().ForEach((Entity e) => {
                Assert.IsTrue(false, "Lights with CascadeShadowmappedLight must include AutoMovingDirectionalLight component for bounds." );
            }).Run();
#endif
            // add csm caches to lights
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            Entities.WithNone<CascadeShadowmappedLightCache>().WithAll<CascadeShadowmappedLight>().ForEach((Entity e) => 
            {
                ecb.AddComponent<CascadeShadowmappedLightCache>(e);
            }).Run();
            ecb.Playback(EntityManager);
            ecb.Dispose();

            var sysBounds = World.GetExistingSystem<UpdateWorldBoundsSystem>();
            Entities.WithoutBurst().WithAll<DirectionalLight>().ForEach((Entity eLight, ref AutoMovingDirectionalLight amdl,
                ref Light l, ref LocalToWorld ltw, ref Rotation rx, ref Translation tx, ref NonUniformScale sc) => 
            {
                Assert.IsTrue(!EntityManager.HasComponent<Parent>(eLight), "Auto moving directional lights can not have a parent transform" );
                if (amdl.autoBounds)
                    amdl.bounds =  sysBounds.m_wholeWorldBounds;
                // TODO: split into two loops, BUT can not have that many components in ForEach 
                l.clipZFar = 1.0f; 
                l.clipZNear = 0.0f;
                AssignSimpleAutoBounds(ref amdl, ref ltw, ref rx, ref tx, ref sc);
                if ( EntityManager.HasComponent<CascadeShadowmappedLight>(eLight)) {
                    var csm = EntityManager.GetComponentData<CascadeShadowmappedLight>(eLight);
                    var csmDest = EntityManager.GetComponentData<CascadeShadowmappedLightCache>(eLight);
                    AssignCascades(ref amdl, ref ltw, ref rx, ref csm, ref csmDest);
                    EntityManager.SetComponentData<CascadeShadowmappedLightCache>(eLight, csmDest);
                }
            }).Run();
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class UpdateLightMatricesSystem : SystemBase
    {
        protected override void OnUpdate() 
        {
            Dependency.Complete();
            // add matrices component if needed 
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            Entities.WithoutBurst().WithNone<LightMatrices>().WithAll<Light>().ForEach((Entity e) =>
            {
                ecb.AddComponent<LightMatrices>(e);
            }).Run();
            ecb.Playback(EntityManager);
            ecb.Dispose();
            
            // update 
            Entities.ForEach((ref Light c, ref LocalToWorld tx, ref LightMatrices m, ref SpotLight sl) =>
            { // spot light
                m.projection = ProjectionHelper.ProjectionMatrixPerspective(c.clipZNear, c.clipZFar, sl.fov, 1.0f);
                m.view = math.inverse(tx.Value);
                m.mvp = math.mul(m.projection, m.view);
                ProjectionHelper.FrustumFromMatrices(m.projection, m.view, out m.frustum);
            }).Run();
            Entities.ForEach((ref Light c, ref LocalToWorld tx, ref LightMatrices m, ref DirectionalLight dr) =>
            { // directional
                m.projection = ProjectionHelper.ProjectionMatrixOrtho(0.0f, 1.0f, 1.0f, 1.0f);
                m.view = math.inverse(tx.Value);
                m.mvp = math.mul(m.projection, m.view);
                ProjectionHelper.FrustumFromMatrices(m.projection, m.view, out m.frustum);
            }).Run();
            Entities.WithNone<DirectionalLight, SpotLight>().ForEach((ref Light c, ref LocalToWorld tx, ref LightMatrices m) =>
            { // point
                m.projection = float4x4.identity;
                m.view = math.inverse(tx.Value);
                m.mvp = math.mul(m.projection, m.view);
                // build furstum from bounds 
                ProjectionHelper.FrustumFromCube(tx.Value.c3.xyz, c.clipZFar, out m.frustum);
            }).Run();
        }
    }
}
