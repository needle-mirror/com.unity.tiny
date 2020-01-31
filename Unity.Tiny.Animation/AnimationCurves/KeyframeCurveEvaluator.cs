using Unity.Entities;
using Unity.Mathematics;

// Note: Copied from the Animation package
namespace Unity.Tiny.Animation
{
    static class KeyframeCurveEvaluator
    {
        public static float Evaluate(float time, ref KeyframeCurve curve)
        {
            return Evaluate(time, KeyframeCurveProvider.Create(curve));
        }

        public static float Evaluate(float time, BlobAssetReference<KeyframeCurveBlob> curve)
        {
            return Evaluate(time, KeyframeCurveProvider.Create(curve));
        }

        public static float Evaluate(float time, KeyframeCurveAccessor curve)
        {
            if (curve.Length == 1)
            {
                return curve.GetKeyframe(0).Value;
            }

            // Wrap time
            time = math.clamp(time, curve.GetKeyframe(0).Time, curve.GetKeyframe(curve.Length - 1).Time);
            FindIndicesForSampling(time, ref curve, out int lhs, out int rhs);

            var leftKey = curve.GetKeyframe(lhs);
            var rightKey = curve.GetKeyframe(rhs);
            var output = HermiteInterpolate(time, leftKey, rightKey);
            HandleSteppedCurve(leftKey, rightKey, ref output);

            return output;
        }

        static void HandleSteppedCurve(Keyframe lhs, Keyframe rhs, ref float output)
        {
            if (math.isinf(lhs.OutTangent) || math.isinf(rhs.InTangent))
                output = lhs.Value;
        }

        static void FindIndicesForSampling(float time, ref KeyframeCurveAccessor curve, out int lhs, out int rhs)
        {
            var actualSize = curve.Length;

            // Fall back to using binary search
            // upper bound (first value larger than curveT)
            var length = actualSize;
            int half;
            int middle;
            int first = 0;
            while (length > 0)
            {
                half = length >> 1;
                middle = first + half;

                if (time < curve.GetKeyframe(middle).Time)
                {
                    length = half;
                }
                else
                {
                    first = middle;
                    ++first;
                    length = length - half - 1;
                }
            }

            // If not within range, we pick the last element twice
            lhs = first - 1;
            rhs = math.min(actualSize - 1, first);
        }

        static float HermiteInterpolate(float time, Keyframe lhs, Keyframe rhs)
        {
            float dx = rhs.Time - lhs.Time;
            float m0;
            float m1;
            float t;
            if (dx != 0.0f)
            {
                t = (time - lhs.Time) / dx;
                m0 = lhs.OutTangent * dx;
                m1 = rhs.InTangent * dx;
            }
            else
            {
                t = 0.0f;
                m0 = 0;
                m1 = 0;
            }

            return HermiteInterpolate(t, lhs.Value, m0, m1, rhs.Value);
        }

        static float HermiteInterpolate(float t, float p0, float m0, float m1, float p1)
        {
            // Unrolled the equations to avoid precision issue.
            // (2 * t^3 -3 * t^2 +1) * p0 + (t^3 - 2 * t^2 + t) * m0 + (-2 * t^3 + 3 * t^2) * p1 + (t^3 - t^2) * m1

            var a = 2.0f * p0 + m0 - 2.0f * p1 + m1;
            var b = -3.0f * p0 - 2.0f * m0 + 3.0f * p1 - m1;
            var c = m0;
            var d = p0;

            return t * (t * (a * t + b) + c) + d;
        }
    }
}
