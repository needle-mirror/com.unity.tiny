using System;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Tiny.Input;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine.Networking.PlayerConnection;
#if UNITY_DOTSRUNTIME
using Bgfx;
#endif

namespace Unity.Tiny.Rendering
{
    // attach next to an entity with rotation
    public struct DemoSpinner : IComponentData
    {
        public quaternion spin;
    };

    public class DemoSpinnerSystem : SystemBase
    {
        protected bool m_paused;
        protected override void OnUpdate()
        {
            var input = World.GetExistingSystem<InputSystem>();
            if (input.GetKeyDown(KeyCode.Space))
                m_paused = !m_paused;

            float dt = (float)Time.DeltaTime;
            if (!m_paused)
            {
                // rotate stuff
                Entities.ForEach((ref DemoSpinner s, ref Rotation r) =>
                {
                    quaternion sp = s.spin;
                    sp.value.xyz *= dt;
                    r.Value = math.normalize(math.mul(r.Value, sp));
                }).Run();
            }
        }
    }

    // attach next to a Camera
    [UpdateBefore(typeof(SubmitSystemGroup))]
    [UpdateAfter(typeof(SimulationSystemGroup))]
    public struct CameraKeyControl : IComponentData
    {
        public float movespeed;
        public float mousemovespeed;
        public float mouserotspeed;
        public float fovspeed;

        public void Default()
        {
            movespeed = 10.0f;       // in worldunits/second
            mousemovespeed = 50.0f;  // in worldunits/screen
            mouserotspeed = 120.0f;   // in degrees/screen
            fovspeed = 40.0f;        // in degrees/second
        }
    };

    // attach next to a Light
    public struct LightFromCameraByKey : IComponentData
    {
        public KeyCode key;
    }

    public static class SharedCameraSyncInfo
    {
        public static readonly Guid syncCameraGuid = new Guid("baa9550975ea4acc9c8389a1cd777276");
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CameraSynchronizationMessage
    {
        public float3 position;
        public quaternion rotation;
        public float fovDegrees;
    }

#if UNITY_DOTSRUNTIME
    public class KeyControlsSystem : SystemBase
    {
        protected int m_nshots;
        public bool m_configAlwaysRun;
        public bool m_notFirst;
        int m_cfgIndex;

        private RenderGraphConfig GetNextConfig()
        {
            m_cfgIndex++;
            switch (m_cfgIndex) {
                case 0:
                    return new RenderGraphConfig { RenderBufferWidth = 640, RenderBufferHeight = 480, Mode = RenderGraphMode.FixedRenderBuffer };
                case 1:
                    return new RenderGraphConfig { RenderBufferWidth = 1920 , RenderBufferHeight = 1080, Mode = RenderGraphMode.FixedRenderBuffer };
                case 2:
                    return new RenderGraphConfig { RenderBufferMaxSize = 512,  Mode = RenderGraphMode.ScaledRenderBuffer };
                case 3:
                    return new RenderGraphConfig { Mode = RenderGraphMode.DirectToFrontBuffer };
                case 4:
                default:
                    m_cfgIndex = -1;
                    return new RenderGraphConfig { RenderBufferWidth = 1280, RenderBufferHeight = 720, Mode = RenderGraphMode.FixedRenderBuffer };

            }
        }

        protected void ControlCamera(Entity ecam)
        {
            // camera controls
            var input = World.GetExistingSystem<InputSystem>();
            float dt = (float)Time.DeltaTime;
            var di = GetSingleton<DisplayInfo>();
            float2 inpos = input.GetInputPosition();
            float2 deltaMouse = input.GetInputDelta() / new float2(di.width, di.height);

            if (!m_notFirst)
            {
                m_notFirst = true;
                return;
            }

            bool mb0 = input.GetMouseButton(0);
            bool mb1 = input.GetMouseButton(1);
            bool mb2 = input.GetMouseButton(2);

            if (input.IsTouchSupported())
            {
                if (input.TouchCount() > 0)
                {
                    Touch t0 = input.GetTouch(0);
                    deltaMouse.x = t0.deltaX / (float)di.width;
                    deltaMouse.y = t0.deltaY / (float)di.height;
                }
                if (input.TouchCount() == 1)
                    mb0 = true;
                else if (input.TouchCount() == 2)
                    mb1 = true;
            }

            var tPos = EntityManager.GetComponentData<Translation>(ecam);
            var tRot = EntityManager.GetComponentData<Rotation>(ecam);
            var cam = EntityManager.GetComponentData<Camera>(ecam);
            CameraKeyControl cc = default;
            if (EntityManager.HasComponent<CameraKeyControl>(ecam))
                cc = EntityManager.GetComponentData<CameraKeyControl>(ecam);
            else
                cc.Default();

            float3x3 rMat = new float3x3(tRot.Value);

            if (input.GetKey(KeyCode.UpArrow) || input.GetKey(KeyCode.W))
                tPos.Value += rMat.c2 * cc.movespeed * dt;
            if (input.GetKey(KeyCode.DownArrow) || input.GetKey(KeyCode.S))
                tPos.Value -= rMat.c2 * cc.movespeed * dt;
            if (input.GetKey(KeyCode.LeftArrow) || input.GetKey(KeyCode.A))
                tPos.Value -= rMat.c0 * cc.movespeed * dt;
            if (input.GetKey(KeyCode.RightArrow) || input.GetKey(KeyCode.D))
                tPos.Value += rMat.c0 * cc.movespeed * dt;
            if (input.GetKey(KeyCode.PageUp) || input.GetKey(KeyCode.R))
                cam.fov += cc.fovspeed * dt;
            if (input.GetKey(KeyCode.PageDown) || input.GetKey(KeyCode.L))
                cam.fov -= cc.fovspeed * dt;

            if (input.GetKey(KeyCode.Return))
            {
                tPos.Value = new float3(0, 0, -20.0f);
                tRot.Value = quaternion.identity;
                cam.fov = 60;
            }
            cam.fov = math.clamp(cam.fov, 0.1f, 179.0f);

            if (mb0)
            {
                var dyAxis = quaternion.EulerXYZ(new float3(0, deltaMouse.x * math.radians(cc.mouserotspeed), 0));
                var dxAxis = quaternion.EulerXYZ(new float3(-deltaMouse.y * math.radians(cc.mouserotspeed), 0, 0));
                tRot.Value = math.mul(tRot.Value, dyAxis);
                tRot.Value = math.mul(tRot.Value, dxAxis);
            }
            if (mb1)
            {
                tPos.Value += rMat.c0 * -deltaMouse.x * cc.mousemovespeed;
                tPos.Value += rMat.c1 * deltaMouse.y * cc.mousemovespeed;
            }
            if (input.GetMouseButton(2))
            {
                tPos.Value += rMat.c2 * deltaMouse.y * cc.mousemovespeed;
            }

            // write back
            EntityManager.SetComponentData<Translation>(ecam, tPos);
            EntityManager.SetComponentData<Rotation>(ecam, tRot);
            EntityManager.SetComponentData<Camera>(ecam, cam);
        }

        protected Entity FindCamera()
        {
            var ecam = Entity.Null;
            float bestdepth = 0;
            Entities.WithAll<Translation, Rotation, CameraKeyControl>().ForEach((Entity e, ref Camera cam) =>
            {
                if (ecam == Entity.Null || cam.depth > bestdepth)
                {
                    bestdepth = cam.depth;
                    ecam = e;
                }
            }).Run();
            if (ecam == Entity.Null)
            {
                Entities.WithAll<Translation, Rotation>().ForEach((Entity e, ref Camera cam) =>
                {
                    if (ecam == Entity.Null || cam.depth > bestdepth)
                    {
                        bestdepth = cam.depth;
                        ecam = e;
                    }
                }).Run();
            }
            return ecam;
        }

        protected unsafe override void OnUpdate()
        {
            var input = World.GetExistingSystem<InputSystem>();
            var renderer = World.GetExistingSystem<RendererBGFXSystem>();
            var rendererInstance = renderer.InstancePointer();

            bool anyShift = input.GetKey(KeyCode.LeftShift) || input.GetKey(KeyCode.RightShift);
            bool anyCtrl = input.GetKey(KeyCode.LeftControl) || input.GetKey(KeyCode.RightControl);
            bool anyAlt = input.GetKey(KeyCode.LeftAlt) || input.GetKey(KeyCode.RightAlt);

            if (!m_configAlwaysRun && !anyShift)
                return;

            // debug bgfx stuff
            if (input.GetKey(KeyCode.F2) || (input.GetKey(KeyCode.Alpha2) && anyAlt))
                rendererInstance->SetFlagThisFrame(bgfx.DebugFlags.Stats);

            if (input.GetKeyDown(KeyCode.F3) || (input.GetKeyDown(KeyCode.Alpha3) && anyAlt))
            {
                var di = GetSingleton<DisplayInfo>();
                if (di.colorSpace == ColorSpace.Gamma) di.colorSpace = ColorSpace.Linear;
                else di.colorSpace = ColorSpace.Gamma;
                SetSingleton(di);
                renderer.DestroyAllTextures();
                renderer.ReloadAllImages();
                Debug.LogFormatAlways("Color space is now {0}.", di.colorSpace == ColorSpace.Gamma ? "Gamma (no srgb sampling)" : "Linear");
            }

            if (input.GetKeyDown(KeyCode.F4) || (input.GetKeyDown(KeyCode.Alpha4) && anyAlt))
            {
                var di = GetSingleton<DisplayInfo>();
                di.disableVSync = !di.disableVSync;
                SetSingleton(di);
                Debug.LogFormatAlways("VSync is now {0}.", di.disableVSync ? "disabled" : "enabled");
            }

            if (input.GetKeyDown(KeyCode.F5) || (input.GetKeyDown(KeyCode.Alpha5) && anyAlt)) {
                RenderGraphConfig cfg = GetNextConfig();
                SetSingleton(cfg);
                Debug.LogFormatAlways("Target config is now {0}*{1} {2}.", cfg.RenderBufferHeight, cfg.RenderBufferWidth, cfg.Mode==RenderGraphMode.DirectToFrontBuffer?"direct":"buffer" );
            }

            rendererInstance->m_outputDebugSelect = new float4(0, 0, 0, 0);
            if (input.GetKey(KeyCode.Alpha1))
                rendererInstance->m_outputDebugSelect = new float4(1, 0, 0, 0);
            if (input.GetKey(KeyCode.Alpha2))
                rendererInstance->m_outputDebugSelect = new float4(0, 1, 0, 0);
            if (input.GetKey(KeyCode.Alpha3))
                rendererInstance->m_outputDebugSelect = new float4(0, 0, 1, 0);
            if (input.GetKey(KeyCode.Alpha4))
                rendererInstance->m_outputDebugSelect = new float4(0, 0, 0, 1);
            if (input.GetKeyDown(KeyCode.Z))
            {
                renderer.RequestScreenShot(FixedString.Format("screenshot{0}.tga", m_nshots++).ToString());
            }
            if (input.GetKeyDown(KeyCode.Escape))
            {
                Debug.LogFormatAlways("Reloading all textures.");
                // free all textures - this releases the bgfx textures and invalidates all cached bgfx state that might contain texture handles
                // note that this does not free system textures like single pixel white, default spotlight etc.
                renderer.DestroyAllTextures();
                // now force a reload on all image2d's from files
                renderer.ReloadAllImages();
                // once images are loaded, but don't have a texture, the texture will be uploaded and the cpu memory freed
            }
            if (renderer.HasScreenShot())
            {
                // TODO: save out 32bpp pixel data:
                Debug.LogFormat("Write screen shot to disk: {0}, {1}*{2}",
                    renderer.m_screenShotPath, renderer.m_screenShotWidth, renderer.m_screenShotHeight);
                renderer.ResetScreenShot();
            }

            // camera related stuff
            var ecam = FindCamera();
            if (ecam == Entity.Null)
                return;

            // sync scene camera with game camera position
            if (input.GetKeyDown(KeyCode.F))
            {
                SyncSceneViewToCamera(ref ecam);
            }

            ControlCamera(ecam);

            Entities.WithoutBurst().WithAll<Light>().ForEach(
                (Entity eLight, ref LightFromCameraByKey lk, ref Translation tPos, ref Rotation tRot) =>
                {
                    if (input.GetKeyDown(lk.key))
                    {
                        tPos = EntityManager.GetComponentData<Translation>(ecam);
                        tRot = EntityManager.GetComponentData<Rotation>(ecam);
                        Debug.LogFormat("Set light {0} to {1} {2}", eLight, tPos.Value, tRot.Value.value);
                    }
                }).Run();
        }

        /**
         * Sends a message to the editor to sync the scene camera with the provided camera entity.
         */
        unsafe void SyncSceneViewToCamera(ref Entity ecam)
        {

            var camInfo = new CameraSynchronizationMessage
            {
                position = EntityManager.GetComponentData<Translation>(ecam).Value,
                rotation = EntityManager.GetComponentData<Rotation>(ecam).Value,
                fovDegrees = EntityManager.GetComponentData<Camera>(ecam).fov
            };

            // copy camera info into byte array
            byte[] bytes = new byte[sizeof(CameraSynchronizationMessage)];
            fixed (byte* pOut = bytes)
            {
                UnsafeUtility.CopyStructureToPtr(ref camInfo, pOut);
            }

            PlayerConnection.instance.Send(SharedCameraSyncInfo.syncCameraGuid, bytes);
        }
    }
#endif
}
