using System;
using Unity.Entities;
using Unity.Entities.Runtime;
using Unity.Entities.Runtime.Build;
using Unity.Tiny.Rendering;
using Unity.Tiny.Rendering.Settings;
using UnityEngine.Assertions;

namespace Unity.Tiny.Authoring
{
    [UpdateAfter(typeof(ConfigurationSystem))]
    [DisableAutoCreation]
    internal class ExportTinyRenderingSettings : ConfigurationSystemBase
    {
        public override Type[] UsedComponents { get; } =
        {
            typeof(TinyRenderingSettings)
        };

        protected override void OnUpdate()
        {
            using (var query = EntityManager.CreateEntityQuery(typeof(ConfigurationTag)))
            {
                int num = query.CalculateEntityCount();
                Assert.IsTrue(num != 0);
                var singletonEntity = query.GetSingletonEntity();
                DisplayInfo di = DisplayInfo.Default;
                RenderGraphConfig rc = RenderGraphConfig.Default;
                di.colorSpace = UnityEditor.PlayerSettings.colorSpace == UnityEngine.ColorSpace.Gamma ? ColorSpace.Gamma : ColorSpace.Linear;
                if (BuildContext != null)
                {
                    var settings = BuildContext.GetComponentOrDefault<TinyRenderingSettings>();
                    di.width = settings.WindowSize.x;
                    di.height = settings.WindowSize.y;
                    di.autoSizeToFrame = settings.AutoResizeFrame;
                    di.disableVSync = settings.DisableVsync;
                    di.gpuSkinning = settings.GPUSkinning;
                    rc.RenderBufferWidth = settings.RenderResolution.x;
                    rc.RenderBufferHeight = settings.RenderResolution.y;
                    rc.RenderBufferMaxSize = settings.MaxResolution;
                    rc.Mode = settings.RenderGraphMode;
                }
                EntityManager.AddComponentData(singletonEntity, di);
                EntityManager.AddComponentData(singletonEntity, rc);
            }
        }
    }
}
