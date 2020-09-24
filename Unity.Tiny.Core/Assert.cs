using System;
using System.Diagnostics;
using Unity.Collections;

namespace Unity.Tiny.Assertions
{
    public static class Assert
    {
        [Conditional("DEBUG")]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void IsTrue(bool condition)
        {
            if (condition)
                return;

            throw new InvalidOperationException("Assertion failed.");
        }

        [Conditional("DEBUG")]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void IsTrue(bool condition, FixedString512 message)
        {
            if (condition)
                return;

            throw new InvalidOperationException(message.ToString());
        }
    }
}
