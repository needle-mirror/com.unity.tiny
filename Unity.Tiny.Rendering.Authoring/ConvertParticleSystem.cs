using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Rendering;
using Unity.Transforms;
using Unity.Entities.Runtime.Build;
using Unity.Tiny.Particles;

namespace Unity.TinyConversion
{
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    [ConverterVersion("christine-johnson", 1)]
    class ParticleSystemDeclareAssets : GameObjectConversionSystem
    {
        protected override void OnUpdate() =>
            Entities.ForEach((UnityEngine.ParticleSystemRenderer uParticleSystemRenderer) =>
            {
                DeclareReferencedAsset(uParticleSystemRenderer.sharedMaterial);
                DeclareAssetDependency(uParticleSystemRenderer.gameObject, uParticleSystemRenderer.sharedMaterial);

                // NOTE: we depend on the output of the Unity shader importer so we don't have to recompute shader dependencies (e.g. include files)
                Settings.AssetImportContext.DependsOnArtifact(UnityEditor.AssetDatabase.GetAssetPath(uParticleSystemRenderer.sharedMaterial.shader));

                if (uParticleSystemRenderer.renderMode == ParticleSystemRenderMode.Mesh)
                {
                    if (uParticleSystemRenderer.mesh == null)
                        UnityEngine.Debug.LogWarning("Missing mesh in ParticleSystemRenderer on gameobject: " + uParticleSystemRenderer.gameObject.name);

                    DeclareReferencedAsset(uParticleSystemRenderer.mesh);
                    DeclareAssetDependency(uParticleSystemRenderer.gameObject, uParticleSystemRenderer.mesh);
                }
            });
    }

    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class ParticleSystemConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.ParticleSystem uParticleSystem) =>
            {
                var eParticleSystem = GetPrimaryEntity(uParticleSystem);
                AddTransforms(ref uParticleSystem, eParticleSystem);

                // General settings
                ParticleEmitter particleEmitter = new ParticleEmitter
                {
                    Duration = uParticleSystem.main.duration,
                    MaxParticles = (uint)uParticleSystem.main.maxParticles,
                    Lifetime = ConvertMinMaxCurve(uParticleSystem.main.startLifetime),
                    AttachToEmitter = uParticleSystem.main.simulationSpace == ParticleSystemSimulationSpace.Local,
                };

                DstEntityManager.AddComponentData(eParticleSystem, new EmitterInitialSpeed { Speed = ConvertMinMaxCurve(uParticleSystem.main.startSpeed) });

                if (uParticleSystem.main.loop)
                    DstEntityManager.AddComponentData(eParticleSystem, new Looping());

                DstEntityManager.AddComponentData(eParticleSystem, new StartDelay { Delay = ConvertMinMaxCurve(uParticleSystem.main.startDelay) });
                DstEntityManager.AddComponentData(eParticleSystem, ConvertMinMaxGradient(uParticleSystem.main.startColor));

                if (!uParticleSystem.useAutoRandomSeed)
                    DstEntityManager.AddComponentData(eParticleSystem, new RandomSeed { Value = uParticleSystem.randomSeed });

                // Emission settings
                if (uParticleSystem.emission.enabled)
                {
                    particleEmitter.EmitRate = ConvertMinMaxCurve(uParticleSystem.emission.rateOverTime);

                    if (uParticleSystem.emission.burstCount > 0)
                    {
                        UnityEngine.ParticleSystem.Burst[] bursts = new UnityEngine.ParticleSystem.Burst[uParticleSystem.emission.burstCount];
                        uParticleSystem.emission.GetBursts(bursts);
                        // TODO support multiple bursts with IBufferElementData or by creating a new entity for each burst emitter
                        //foreach (var burst in bursts)
                        var burst = bursts[0];
                        {
                            DstEntityManager.AddComponentData<BurstEmission>(eParticleSystem, new BurstEmission
                            {
                                Count = ConvertMinMaxCurve(burst.count),
                                Interval = burst.repeatInterval,
                                Cycles = burst.cycleCount
                                    // TODO probability
                                    // TODO time
                            });
                        }
                    }
                }

                DstEntityManager.AddComponentData<ParticleEmitter>(eParticleSystem, particleEmitter);

                // Shape settings
                AddEmitterSource(ref uParticleSystem, eParticleSystem);
                DstEntityManager.AddComponentData(eParticleSystem, new RandomizeDirection { Value = uParticleSystem.shape.randomDirectionAmount });
                DstEntityManager.AddComponentData(eParticleSystem, new RandomizePosition { Value = uParticleSystem.shape.randomPositionAmount });

                // Renderer settings
                ParticleSystemRenderer uParticleSystemRenderer = uParticleSystem.gameObject.GetComponent<ParticleSystemRenderer>();
                DstEntityManager.AddComponentData(eParticleSystem, new ParticleMaterial { Material = GetPrimaryEntity(uParticleSystemRenderer.sharedMaterial) });

                if (uParticleSystemRenderer.renderMode == ParticleSystemRenderMode.Mesh)
                    DstEntityManager.AddComponentData(eParticleSystem, new ParticleMesh { Mesh = GetPrimaryEntity(uParticleSystemRenderer.mesh) });
            });
        }

        private void AddTransforms(ref UnityEngine.ParticleSystem uParticleSystem, Entity eParticleSystem)
        {
            // TODO further investigate custom transform expected behavior
            /*if (uParticleSystem.main.simulationSpace == ParticleSystemSimulationSpace.Custom)
            {
                DstEntityManager.AddComponentData(eParticleSystem, new LocalToWorld { Value = uParticleSystem.main.customSimulationSpace.localToWorldMatrix });
                DstEntityManager.AddComponentData(eParticleSystem, new Translation { Value = uParticleSystem.main.customSimulationSpace.position });
                DstEntityManager.AddComponentData(eParticleSystem, new Rotation { Value = uParticleSystem.main.customSimulationSpace.rotation });
                if (uParticleSystem.main.customSimulationSpace.lossyScale != Vector3.one)
                    DstEntityManager.AddComponentData(eParticleSystem, new NonUniformScale { Value = uParticleSystem.main.customSimulationSpace.lossyScale });
            }*/

            // Emitter initial rotation
            if (uParticleSystem.main.startRotation3D)
            {
                DstEntityManager.AddComponentData<EmitterInitialNonUniformRotation>(eParticleSystem, new EmitterInitialNonUniformRotation
                {
                    AngleX = ConvertMinMaxCurve(uParticleSystem.main.startRotationX),
                    AngleY = ConvertMinMaxCurve(uParticleSystem.main.startRotationY),
                    AngleZ = ConvertMinMaxCurve(uParticleSystem.main.startRotationZ)
                });
            }
            else
            {
                DstEntityManager.AddComponentData<EmitterInitialRotation>(eParticleSystem, new EmitterInitialRotation
                {
                    Angle = ConvertMinMaxCurve(uParticleSystem.main.startRotation)
                });
            }

            // Emitter initial scale
            if (uParticleSystem.main.startSize3D)
            {
                DstEntityManager.AddComponentData<EmitterInitialNonUniformScale>(eParticleSystem, new EmitterInitialNonUniformScale
                {
                    ScaleX = ConvertMinMaxCurve(uParticleSystem.main.startSizeX),
                    ScaleY = ConvertMinMaxCurve(uParticleSystem.main.startSizeY),
                    ScaleZ = ConvertMinMaxCurve(uParticleSystem.main.startSizeZ)
                });
            }
            else
            {
                DstEntityManager.AddComponentData<EmitterInitialScale>(eParticleSystem, new EmitterInitialScale
                {
                    Scale = ConvertMinMaxCurve(uParticleSystem.main.startSize)
                });
            }
        }

        private void AddEmitterSource(ref UnityEngine.ParticleSystem uParticleSystem, Entity eParticleSystem)
        {
            if (!uParticleSystem.shape.enabled)
                return;

            switch (uParticleSystem.shape.shapeType)
            {
                case ParticleSystemShapeType.Cone:
                    DstEntityManager.AddComponentData(eParticleSystem, new EmitterConeSource
                    {
                        Radius = uParticleSystem.shape.radius,
                        Angle = uParticleSystem.shape.angle
                    });
                    break;
                case ParticleSystemShapeType.Circle:
                    DstEntityManager.AddComponentData(eParticleSystem, new EmitterCircleSource
                    {
                        Radius = uParticleSystem.shape.radius
                    });
                    break;
                case ParticleSystemShapeType.Rectangle:
                    DstEntityManager.AddComponentData(eParticleSystem, new EmitterRectangleSource());
                    break;
                case ParticleSystemShapeType.Sphere:
                    DstEntityManager.AddComponentData(eParticleSystem, new EmitterSphereSource
                    {
                        Radius = uParticleSystem.shape.radius
                    });
                    break;
                case ParticleSystemShapeType.Hemisphere:
                    DstEntityManager.AddComponentData(eParticleSystem, new EmitterHemisphereSource
                    {
                        Radius = uParticleSystem.shape.radius
                    });
                    break;
                default:
                    UnityEngine.Debug.LogWarning("ParticleSystemShapeType " + nameof(uParticleSystem.shape.shapeType) + " not supported.");
                    break;
            }
        }

        // TODO support curves
        private static Range ConvertMinMaxCurve(UnityEngine.ParticleSystem.MinMaxCurve curve)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new Range { Start = curve.constant, End = curve.constant };
                case ParticleSystemCurveMode.TwoConstants:
                    return new Range { Start = curve.constantMin, End = curve.constantMax };
                case ParticleSystemCurveMode.Curve:
                case ParticleSystemCurveMode.TwoCurves:
                    UnityEngine.Debug.LogWarning("ParticleSystemCurveMode " + nameof(curve.mode) + " not supported.");
                    return new Range();
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(curve.mode), curve.mode, null);
            }
        }

        // TODO support gradients
        private static InitialColor ConvertMinMaxGradient(UnityEngine.ParticleSystem.MinMaxGradient gradient)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                {
                    float4 color = new float4(gradient.color.r, gradient.color.g, gradient.color.b, gradient.color.a);
                    return new InitialColor { ColorMin = color, ColorMax = color };
                }
                case ParticleSystemGradientMode.TwoColors:
                    return new InitialColor
                    {
                        ColorMin = new float4(gradient.colorMin.r, gradient.colorMin.g, gradient.colorMin.b, gradient.colorMin.a),
                        ColorMax = new float4(gradient.colorMax.r, gradient.colorMax.g, gradient.colorMax.b, gradient.colorMax.a)
                    };
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.TwoGradients:
                case ParticleSystemGradientMode.RandomColor:
                {
                    UnityEngine.Debug.LogWarning("ParticleSystemGradientMode " + nameof(gradient.mode) + " not supported.");
                    float4 defaultColor = new float4(1);
                    return new InitialColor { ColorMin = defaultColor, ColorMax = defaultColor };
                }

                default:
                    throw new System.ArgumentOutOfRangeException(nameof(gradient.mode), gradient.mode, null);
            }
        }
    }

    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [UpdateBefore(typeof(MeshConversion))]
    [UpdateAfter(typeof(MaterialConversion))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    public class ParticleSystemRendererConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.ParticleSystemRenderer uParticleSystemRenderer) =>
            {
                var eParticleSystemRenderer = GetPrimaryEntity(uParticleSystemRenderer);
                UnityEngine.Mesh uMesh = uParticleSystemRenderer.mesh;
                if (uParticleSystemRenderer.renderMode == ParticleSystemRenderMode.Mesh && uMesh != null)
                {
                    var eMesh = GetPrimaryEntity(uMesh);
                    var eMaterial = GetPrimaryEntity(uParticleSystemRenderer.sharedMaterial);
                    if (DstEntityManager.HasComponent<LitMaterial>(eMaterial))
                    {
                        DstEntityManager.AddComponent<LitMeshRenderData>(eMesh);
                        DstEntityManager.RemoveComponent<SimpleMeshRenderData>(eMesh);
                    }
                    else if (DstEntityManager.HasComponent<SimpleMaterial>(eMaterial))
                    {
                        DstEntityManager.AddComponent<SimpleMeshRenderData>(eMesh);
                        DstEntityManager.RemoveComponent<LitMeshRenderData>(eMesh);
                    }
                }
            });
        }
    }
}
