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

                AudioClip tinyAudioClip = new AudioClip();
                tinyAudioClip.loadType = (audioClip.loadType == UnityEngine.AudioClipLoadType.CompressedInMemory) ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnPlay;
                tinyAudioClip.channels = audioClip.channels;
                tinyAudioClip.samples = audioClip.samples;
                tinyAudioClip.frequency = audioClip.frequency;

                DstEntityManager.AddComponentData<AudioClip>(entity, tinyAudioClip);
                DstEntityManager.AddComponent<AudioClipUsage>(entity);
                DstEntityManager.AddBuffer<AudioClipCompressed>(entity);
                DstEntityManager.AddBuffer<AudioClipUncompressed>(entity);                
                DstEntityManager.AddComponent<AudioClipLoadFromFile>(entity);

                var exportGuid = GetGuidForAssetExport(audioClip);
                DstEntityManager.AddComponent<AudioClipLoadFromFileAudioFile>(entity);
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
