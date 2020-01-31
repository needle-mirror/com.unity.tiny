using System;
using System.Runtime.CompilerServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

// Note: Copied from the Animation package
namespace Unity.Tiny.Animation
{
    struct Keyframe
    {
        public float Time;
        public float Value;
        public float InTangent;
        public float OutTangent;
    }

    unsafe struct OffsetPtr<T> where T : unmanaged
    {
        internal T* m_Ptr;
        internal int m_Length;

        public OffsetPtr(T* ptr, int length)
        {
            m_Ptr = ptr;
            m_Length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Ptr == null)
                throw new System.NullReferenceException("Invalid offset pointer");
            if ((uint)index >= (uint)m_Length)
                throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range Length {1}", index, m_Length));
#endif

            return *(m_Ptr + index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, T value)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Ptr == null)
                throw new System.NullReferenceException("Invalid offset pointer");
            if ((uint)index >= (uint)m_Length)
                throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range Length {1}", index, m_Length));
#endif

            *(m_Ptr + index) = value;
        }
    }

    struct KeyframeCurveAccessor
    {
        internal OffsetPtr<Keyframe> m_KeyframeData;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Keyframe GetKeyframe(int index) => m_KeyframeData.Get(index);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetKeyframe(int index, Keyframe value) => m_KeyframeData.Set(index, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Keyframe* GetKeyframeUnsafePtr() => m_KeyframeData.m_Ptr;
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_KeyframeData.m_Length;
        }
    }

    struct KeyframeCurveBlob
    {
        public BlobArray<Keyframe> Keyframes;
    }

    struct KeyframeCurve: IDisposable
    {
        internal NativeArray<Keyframe> m_Keyframes;

        public KeyframeCurve(int capacity, Allocator allocator)
        {
            m_Keyframes = new NativeArray<Keyframe>(capacity, allocator);
        }

        public int Length => m_Keyframes.Length;

        public Keyframe this[int index]
        {
            get => m_Keyframes[index];
            set => m_Keyframes[index] = value;
        }

        public void Dispose() => m_Keyframes.Dispose();
    }

    static class KeyframeCurveProvider
    {
        unsafe public static KeyframeCurveAccessor Create(KeyframeCurve curve)
        {
            return new KeyframeCurveAccessor()
            {
                m_KeyframeData = new OffsetPtr<Keyframe>((Keyframe*)curve.m_Keyframes.GetUnsafePtr(), curve.Length)
            };
        }

        unsafe public static KeyframeCurveAccessor CreateReadOnly(KeyframeCurve curve)
        {
            return new KeyframeCurveAccessor()
            {
                m_KeyframeData = new OffsetPtr<Keyframe>((Keyframe*)curve.m_Keyframes.GetUnsafeReadOnlyPtr(), curve.Length)
            };
        }
        unsafe public static KeyframeCurveAccessor Create(BlobAssetReference<KeyframeCurveBlob> curve)
        {
            return new KeyframeCurveAccessor()
            {
                m_KeyframeData = new OffsetPtr<Keyframe>((Keyframe*)curve.Value.Keyframes.GetUnsafePtr(), curve.Value.Keyframes.Length)
            };
        }
    }
}
