using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Serialization.Json;

namespace Unity.Tiny.JSON
{
    public struct TinyJsonArray : IEnumerable<TinyJsonObject>
    {
        public int Length => m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)].length;
        internal TinyJsonData m_Data;
        internal JsonKeyHandle m_ArrayRefHandle;

        internal TinyJsonArray(JsonKeyHandle refHandle, in TinyJsonData data)
        {
            m_ArrayRefHandle = refHandle;
            m_Data = data;
        }

        public TinyJsonObject this[int index]
        {
            get
            {
                if (index >= Length || index < 0)
                {
                    ThrowIndexOutOfRangeException(index, Length);
                }

                var handle = m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)][index];
                return m_Data.CreateObjectFromJsonHandle(handle);
            }
            set
            {
                // todo: could optimize by doing simple value replacement depending on type
                if (index >= Length || index < 0)
                {
                    ThrowIndexOutOfRangeException(index, Length);
                }

                if (value.Type == JsonValueType.Array || value.Type == JsonValueType.Object)
                {
                    TinyJsonData.CheckNotSameDataAllocation(m_Data, value.m_Data);
                }

                // delete old value
                var unsafeHandleList = m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)];
                var oldHandle = unsafeHandleList[index];
                m_Data.RemoveValueFromBuffer(oldHandle);

                // add new value
                var newHandle = AddObjectToBuffer(ref value);
                unsafeHandleList[index] = newHandle;
            }
        }

        internal void DeepCopyArrayValues(ref TinyJsonArray source)
        {
            var sourceList = source.m_Data.m_ArrayBuffer[source.m_Data.LocationInValueBuffer(source.m_ArrayRefHandle)];
            var destinationList = m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)];

            for (int i = 0; i < sourceList.Length; i++)
            {
                var objectCopy = source.m_Data.CreateObjectFromJsonHandle(sourceList[i]); // read from source
                var jsonRef = AddObjectToBuffer(ref objectCopy); // copy to array
                destinationList.Add(jsonRef);
            }

            m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)] = destinationList;
        }

        public void Append(TinyJsonObject value)
        {
            if (value.Type == JsonValueType.Array || value.Type == JsonValueType.Object)
            {
                TinyJsonData.CheckNotSameDataAllocation(m_Data, value.m_Data);
            }

            var handle = AddObjectToBuffer(ref value);
            var array = m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)];
            array.Add(handle);
            m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)] = array;
        }

        public TinyJsonObject AppendEmptyObject() // [{}]
        {
            var handle = m_Data.CreateNestedObject(out var jsonObject);
            var array = m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)];
            array.Add(handle);
            m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)] = array;
            return jsonObject;
        }

        public TinyJsonArray AppendEmptyArray() // [[]]
        {
            var handle = m_Data.CreateNestedArray(out var arrayObject);
            var array = m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)];
            array.Add(handle);
            m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)] = array;
            return arrayObject;
        }

        public void RemoveAt(int index)
        {
            if (index >= Length || index < 0)
            {
                ThrowIndexOutOfRangeException(index, Length);
            }

            var array = m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)];
            var handle = array[index];
            m_Data.RemoveValueFromBuffer(handle);
            array.RemoveAt(index);
            m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)] = array;
        }

        JsonKeyHandle AddObjectToBuffer(ref TinyJsonObject value)
        {
            JsonKeyHandle valRef;
            switch (value.Type)
            {
                case JsonValueType.Bool:
                    valRef = new JsonKeyHandle(ref m_Data, JsonValueType.Bool, m_Data.m_BoolBuffer.Length);
                    m_Data.m_BoolBuffer.Add(value.m_BoolVal);
                    break;
                case JsonValueType.Int:
                    valRef = new JsonKeyHandle(ref m_Data, JsonValueType.Int, m_Data.m_IntBuffer.Length);
                    m_Data.m_IntBuffer.Add(value.m_IntVal);
                    break;
                case JsonValueType.Float:
                    valRef = new JsonKeyHandle(ref m_Data, JsonValueType.Float, m_Data.m_FloatBuffer.Length);
                    m_Data.m_FloatBuffer.Add(value.m_FloatVal);
                    break;
                case JsonValueType.String:
                    // can either be in buffer already or a seperate heap allocation depending
                    // on if we are copying from another initialized tiny json object or a simple string variant
                    valRef = new JsonKeyHandle(ref m_Data, JsonValueType.String, m_Data.m_StringBuffer.Length);
                    if (!value.m_StringVal.IsEmpty)
                    {
                        m_Data.m_StringBuffer.Add(value.m_StringVal[0]);
                        value.m_StringVal.Dispose();
                    }
                    else
                    {
                        m_Data.m_StringBuffer.Add(value.m_Data.m_StringBuffer[value.m_Data.LocationInValueBuffer(value.m_StringRefHandle)]);
                    }
                    break;
                case JsonValueType.Array:
                    valRef = m_Data.CreateNestedArray(out var array);
                    var sourceArray = value.AsArray();
                    array.DeepCopyArrayValues(ref sourceArray);
                    break;
                case JsonValueType.Object:
                    valRef = m_Data.CreateNestedObject(out var newObj);
                    newObj.m_Data.DeepCopyFields(ref value.m_Data);
                    break;
                default:
                    TinyJsonData.InvalidJsonTypeException();
                    return default;
            }

            return valRef;
        }

        public struct Enumerator : IEnumerator<TinyJsonObject>
        {
            internal UnsafeList<JsonKeyHandle> list;
            internal TinyJsonData m_Data;
            internal int i;

            public void Dispose() { }

            public bool MoveNext()
            {
                i++;
                return i > -1 && i < list.Length;
            }

            public void Reset() => i = -1;

            public TinyJsonObject Current => m_Data.CreateObjectFromJsonHandle(list[i]);

            object IEnumerator.Current => new NotImplementedException();
        }

        public IEnumerator<TinyJsonObject> GetEnumerator()
        {
            return new Enumerator { m_Data = m_Data, list = m_Data.m_ArrayBuffer[m_Data.LocationInValueBuffer(m_ArrayRefHandle)], i = -1};
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        static void ThrowIndexOutOfRangeException(int index, int length)
        {
            throw new IndexOutOfRangeException($"Index {index} exceeds array size {length}.");
        }
    }
}
