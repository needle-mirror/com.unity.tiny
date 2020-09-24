using Unity.Collections;
using System;

namespace Unity.Tiny.Rendering {
    // write only ulong hash set, useful for collecting all needed elements
    // when masking 
    internal struct NativeULongSet : IDisposable 
    {
        public NativeULongSet(int capacity, Allocator allocator)
        {
            hashMap = new NativeHashMap<ulong, int>(capacity, allocator);
            values = new NativeList<ulong>(capacity, allocator);
        }

        public void Add(ulong value)
        {
            if ( hashMap.TryAdd(value, values.Length) ) 
                values.Add(value);
        }

        public bool Contains(ulong value)
        {
            return hashMap.ContainsKey(value);
        }

        public void Clear()
        {
            hashMap.Clear();
            values.Clear();
        }

        public void Dispose()
        {
            hashMap.Dispose();
            values.Dispose();
        }

        public NativeList<ulong> ValuesAsArray()
        {
            return values;
        }

        public int GetIndex(ulong value)
        {
            return hashMap[value];
        }

        NativeList<ulong> values;
        NativeHashMap<ulong, int> hashMap;
    }
}
