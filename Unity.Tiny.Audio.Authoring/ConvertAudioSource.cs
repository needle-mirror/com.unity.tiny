using Unity.Entities;
using Unity.Tiny.Audio;

namespace Unity.TinyConversion
{
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    class AudioClipDeclareAssets : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.AudioSource audioSource) =>
            {
                DeclareReferencedAsset(audioSource.clip);
                DeclareAssetDependency(audioSource.gameObject, audioSource.clip);
            });
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    internal class ConvertAudioSource : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.AudioSource audioSource) =>
            {
                var primaryEntity = GetPrimaryEntity(audioSource);
                DstEntityManager.AddComponentData(primaryEntity, new AudioSource
                {
                    clip = GetPrimaryEntity(audioSource.clip),
                    volume = audioSource.volume,
                    loop =  audioSource.loop
                });

                // Supplement the base AudioSource with either a 2d panning or 3d panning component.
                bool soundIs3d = (audioSource.spatialBlend > 0.0f) && ((audioSource.rolloffMode == UnityEngine.AudioRolloffMode.Linear) || (audioSource.rolloffMode == UnityEngine.AudioRolloffMode.Logarithmic));

                if (soundIs3d)
                {
                    DstEntityManager.AddComponentData(primaryEntity, new Audio3dPanning());

                    DstEntityManager.AddComponentData(primaryEntity, new AudioDistanceAttenuation()
                    {
                        rolloffMode = (AudioRolloffMode)audioSource.rolloffMode,
                        minDistance = audioSource.minDistance,
                        maxDistance = audioSource.maxDistance
                    });
                }
                else
                {
                    DstEntityManager.AddComponentData(primaryEntity, new Audio2dPanning()
                    {
                        pan = audioSource.panStereo
                    });
                }

                if ((audioSource.pitch > 0.0f) && (audioSource.pitch != 1.0f))
                {
                    DstEntityManager.AddComponentData(primaryEntity, new AudioPitch()
                    {
                        pitch = audioSource.pitch
                    });
                }

                if (audioSource.playOnAwake)
                    DstEntityManager.AddComponentData(primaryEntity, new AudioSourceStart());
            });
        }
    }
}
