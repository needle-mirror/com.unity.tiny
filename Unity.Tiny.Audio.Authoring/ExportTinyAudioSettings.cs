using System;
using Unity.Entities;
using Unity.Entities.Runtime;
using Unity.Entities.Runtime.Build;
using UnityEngine.Assertions;
using Unity.Tiny.Authoring;
using Unity.Tiny.Audio.Settings;
using Unity.Tiny.Audio;

namespace Unity.Tiny.Authoring
{
    [UpdateAfter(typeof(ConfigurationSystem))]
    [DisableAutoCreation]
    internal class ExportTinyAudioSettings : ConfigurationSystemBase
    {
        public override Type[] UsedComponents { get; } =
        {
            typeof(TinyAudioSettings)
        };

        protected override void OnUpdate()
        {
            using (var query = EntityManager.CreateEntityQuery(typeof(ConfigurationTag))) 
            {
                var singletonEntity = query.GetSingletonEntity();
                AudioConfig ac = AudioConfig.Default;
                if ((BuildContext != null) && (BuildContext.TryGetComponent<TinyAudioSettings>(out var settings)))
                {
                    ac.maxUncompressedAudioMemoryBytes = settings.MaxUncompressedAudioMemoryBytes; 
                }
                EntityManager.AddComponentData(singletonEntity, ac);
            }
        }
    }
}
