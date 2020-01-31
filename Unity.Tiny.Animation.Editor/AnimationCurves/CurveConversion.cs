using Unity.Collections;
using Unity.Entities;
using UnityEngine;

// Note: Copied from the Animation package
namespace Unity.Tiny.Animation.Editor
{
    static class CurveConversion
    {
        static Keyframe KeyframeConversion(UnityEngine.Keyframe inKey)
        {
            return new Keyframe
            {
                InTangent = inKey.inTangent,
                OutTangent = inKey.outTangent,
                Value = inKey.value,
                Time = inKey.time
            };
        }

        public static KeyframeCurve ToKeyframeCurve(this AnimationCurve curve)
        {
            // Allocate the keys
            var keyframeCurve = new KeyframeCurve(curve.length, Allocator.Persistent);

            var len = curve.length;
            for (int i = 0; i < len; i++)
            {
                keyframeCurve[i] = KeyframeConversion(curve.keys[i]);
            }

            return keyframeCurve;
        }

        public static BlobAssetReference<KeyframeCurveBlob> ToKeyframeCurveBlob(this AnimationCurve source)
        {
            var keyframeCurve = source.ToKeyframeCurve();
            var curveBlob = keyframeCurve.ToBlobAssetRef();

            keyframeCurve.Dispose();
            return curveBlob;
        }
    }
}

