using Unity.Collections;
using Unity.Entities;

// Note: Copied from the Animation package
namespace Unity.Tiny.Animation
{
    static class CurveBuilder
    {
        public static BlobAssetReference<KeyframeCurveBlob> ToBlobAssetRef(this KeyframeCurve curve)
        {
            if (curve.Length == 0)
            {
                return BlobAssetReference<KeyframeCurveBlob>.Null;
            }

            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var curveBlob = ref blobBuilder.ConstructRoot<KeyframeCurveBlob>();
            FillKeyframeCurveBlob(curve, ref blobBuilder, ref curveBlob);

            var outputClip = blobBuilder.CreateBlobAssetReference<KeyframeCurveBlob>(Allocator.Persistent);

            blobBuilder.Dispose();

            return outputClip;
        }

        private static void FillKeyframeCurveBlob(KeyframeCurve sourceCurve, ref BlobBuilder blobBuilder, ref KeyframeCurveBlob curveBlob)
        {
            var keyframes = blobBuilder.Allocate(ref curveBlob.Keyframes, sourceCurve.Length);

            float length = sourceCurve.Length;
            for (var i = 0; i < length; ++i)
            {
                keyframes[i] = sourceCurve[i];
            }
        }
    }
}
