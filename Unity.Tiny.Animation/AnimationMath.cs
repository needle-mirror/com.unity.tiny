using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Unity.Tiny.Animation
{
    static class AnimationMath
    {
        // Reimplementation of Mathf.Repeat()
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Repeat(float t, float length)
        {
            return math.clamp(t - math.floor(t / length) * length, 0.0f, length);
        }

        // Reimplementation of Mathf.PingPong()
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PingPong(float t, float length)
        {
            t = Repeat(t, length * 2f);
            return length - math.abs(t - length);
        }

        public static float NormalizeCycle(float cycle)
        {
            if (cycle >= 1.0f)
                return Repeat(cycle, 1.0f);

            if (cycle < 0.0f)
                return 1.0f - Repeat(math.abs(cycle), 1.0f);

            return cycle;
        }
    }
}
