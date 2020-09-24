using System;
using Unity.Entities;
using Unity.Collections;

namespace Unity.Tiny
{
    public abstract class WindowSystem : SystemBase
    {
        public abstract void DebugReadbackImage(out int w, out int h, out NativeArray<byte> pixels);
        public abstract IntPtr GetPlatformWindowHandle();
        public virtual void SetOrientationMask(ScreenOrientation orientation) {}
        public virtual ScreenOrientation GetOrientationMask()
        {
            // returning actual orientation for platforms where orientation cannot be controlled
            return GetOrientation();
        }

        public ScreenOrientation GetOrientation()
        {
            var displayInfo = GetSingleton<DisplayInfo>();
            return displayInfo.orientation;
        }
    }
}
