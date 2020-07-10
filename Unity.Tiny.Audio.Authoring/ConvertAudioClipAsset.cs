using System.IO;
using Unity.Entities;
using Unity.Tiny;
using Unity.Tiny.Audio;

namespace Unity.TinyConversion
{
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    internal class ConvertAudioClipAsset : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.AudioClip audioClip) =>
            {
                var entity = GetPrimaryEntity(audioClip);
                DstEntityManager.AddComponent<AudioClip>(entity);
                DstEntityManager.AddComponent<AudioClipLoadFromFile>(entity);
                DstEntityManager.AddComponent<AudioClipLoadFromFileAudioFile>(entity);

                var exportGuid = GetGuidForAssetExport(audioClip);
                DstEntityManager.SetBufferFromString<AudioClipLoadFromFileAudioFile>(entity, "Data/" + exportGuid.ToString());
            });
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    [UpdateInGroup(typeof(GameObjectExportGroup))]
    internal class AudioClipAsset : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.AudioClip clip) =>
            {
                using (var writer = TryCreateAssetExportWriter(clip))
                {
                    ConversionUtils.ExportSource(writer, new DirectoryInfo("."), clip);
                }
            });
        }
    }
}
