using System;
using Unity.Collections;

namespace Unity.Tiny.JSON
{
    public struct TinyJsonInterface : IDisposable
    {
        public TinyJsonObject Object;
        TinyJsonData m_RootData;

        public TinyJsonInterface(string jsonString, Allocator allocator)
        {
            m_RootData = new TinyJsonData(jsonString, allocator);
            Object = new TinyJsonObject { Type = JsonValueType.Object, m_Data = m_RootData };
        }

        public TinyJsonInterface(Allocator allocator)
        {
            m_RootData = new TinyJsonData(allocator);
            Object = new TinyJsonObject { Type = JsonValueType.Object, m_Data = m_RootData };
        }

        public HeapString ToJson()
        {
            return m_RootData.ToJson();
        }

        public void Dispose()
        {
            m_RootData.Dispose();
        }
    }
}
