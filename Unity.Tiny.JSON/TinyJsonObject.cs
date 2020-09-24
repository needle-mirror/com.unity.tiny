using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Tiny.JSON
{
    /**
     * A type agnostic, mutable JSON object representation.
     */
    public struct TinyJsonObject : IEnumerable<JsonKeyValue>
    {
        public int Count => m_Data.m_RootObjMap.Count();
        public JsonValueType Type;
        internal TinyJsonData m_Data;

        // these fields are used for implicit conversion when getting and setting values.
        internal bool m_BoolVal;
        internal float m_FloatVal;
        internal int m_IntVal;
        internal UnsafeList<FixedString4096> m_StringVal;

        internal JsonKeyHandle m_StringRefHandle;

        // If the type is an array, this is the index to the array in the array buffer
        internal JsonKeyHandle m_ArrayRefHandle;

        public TinyJsonArray CreateEmptyArrayField(FixedString128 key)
        {
            CheckIsCorrectType(JsonValueType.Object, Type);
            return m_Data.AddOrReplaceArrayField(key);
        }

        public TinyJsonObject CreateEmptyObjectField(FixedString128 key)
        {
            CheckIsCorrectType(JsonValueType.Object, Type);
            return m_Data.CreateObjectField(key);
        }

        public void RemoveField(FixedString128 key)
        {
            CheckIsCorrectType(JsonValueType.Object, Type);
            if (!m_Data.m_RootObjMap.ContainsKey(key))
            {
                ThrowKeyNotFoundException(key);
            }

            m_Data.RemoveValueFromBuffer(m_Data.m_RootObjMap[key]);
            m_Data.m_RootObjMap.Remove(key);
        }

        /** Explicit conversions for use within function parameters, etc */
        public bool AsBool()
        {
            CheckIsCorrectType(JsonValueType.Bool, Type);
            return m_BoolVal;
        }

        public int AsInt()
        {
            CheckIsCorrectType(JsonValueType.Int, Type);
            return m_IntVal;
        }

        public float AsFloat()
        {
            CheckIsNumberType(Type);
            if (Type == JsonValueType.Int)
            {
                return m_IntVal;
            }

            return m_FloatVal;
        }

        public FixedString4096 AsString()
        {
            CheckIsCorrectType(JsonValueType.String, Type);
            return m_Data.m_StringBuffer[m_Data.LocationInValueBuffer(m_StringRefHandle)];
        }

        public TinyJsonArray AsArray()
        {
            CheckIsCorrectType(JsonValueType.Array, Type);
            return new TinyJsonArray(m_ArrayRefHandle, in m_Data);
        }

        /**
         * These constructors are for implicit conversion from primitive types when getting and setting.
         */
        TinyJsonObject(int value)
        {
            this = default;
            Type = JsonValueType.Int;
            m_IntVal = value;
        }

        TinyJsonObject(float value)
        {
            this = default;
            Type = JsonValueType.Float;
            m_FloatVal = value;
        }

        TinyJsonObject(bool value)
        {
            this = default;
            Type = JsonValueType.Bool;
            m_BoolVal = value;
        }

        TinyJsonObject(FixedString4096 value)
        {
            this = default;
            Type = JsonValueType.String;
            m_StringVal = new UnsafeList<FixedString4096>(1, Allocator.Persistent) { value };
        }

        TinyJsonObject(TinyJsonArray array)
        {
            this = default;
            Type = JsonValueType.Array;
            m_Data = array.m_Data;
            m_ArrayRefHandle = array.m_ArrayRefHandle;
        }

        /**
         * Modifies, creates, or returns a value associated with the key.
         */
        public TinyJsonObject this[FixedString128 key]
        {
            get
            {
                CheckIsCorrectType(JsonValueType.Object, Type);
                if (!m_Data.m_RootObjMap.ContainsKey(key))
                {
                    ThrowKeyNotFoundException(key);
                }

                return m_Data.GetObjectFromKey(key);
            }
            set
            {
                CheckIsCorrectType(JsonValueType.Object, Type);
                m_Data.AddOrUpdateField(key, value);
            }
        }

        /**
         * Attempts to cast the object to an array to modify / return an array value.
         */
        public TinyJsonObject this[int index]
        {
            get => AsArray()[index];
            set
            {
                var tinyJsonArray = AsArray();
                tinyJsonArray[index] = value;
            }
        }

        /**
        * Implicit conversions to JsonProxyValue calls constructor with proper type or returns the correct
        * primitive field for easy getting / setting .
        */
        public static implicit operator TinyJsonObject(int value)
        {
            return new TinyJsonObject(value);
        }

        public static implicit operator TinyJsonObject(float value)
        {
            return new TinyJsonObject(value);
        }

        public static implicit operator TinyJsonObject(bool value)
        {
            return new TinyJsonObject(value);
        }

        public static implicit operator TinyJsonObject(string value)
        {
            return new TinyJsonObject(value);
        }

        public static implicit operator TinyJsonObject(FixedString4096 value)
        {
            return new TinyJsonObject(value);
        }

        public static implicit operator TinyJsonObject(TinyJsonArray value)
        {
            return new TinyJsonObject(value);
        }

        public static implicit operator int(TinyJsonObject value)
        {
            CheckIsCorrectType(JsonValueType.Int, value.Type);
            return value.m_IntVal;
        }

        public static implicit operator float(TinyJsonObject value)
        {
            CheckIsCorrectType(JsonValueType.Float, value.Type);
            return value.m_FloatVal;
        }

        public static implicit operator FixedString4096(TinyJsonObject value)
        {
            CheckIsCorrectType(JsonValueType.String, value.Type);
            var tinyJsonData = value.m_Data;
            return tinyJsonData.m_StringBuffer[tinyJsonData.LocationInValueBuffer(value.m_StringRefHandle)];
        }

        public static implicit operator bool(TinyJsonObject value)
        {
            CheckIsCorrectType(JsonValueType.Bool, value.Type);
            return value.m_BoolVal;
        }

        public static implicit operator TinyJsonArray(TinyJsonObject value)
        {
            CheckIsCorrectType(JsonValueType.Array, value.Type);
            return new TinyJsonArray(value.m_ArrayRefHandle, in value.m_Data);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckIsCorrectType(JsonValueType expected, JsonValueType actual)
        {
            if (expected != actual)
            {
                throw new Exception($"Attempted operation on invalid JSON type: Expected {expected}, Actual: {actual}");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckIsNumberType(JsonValueType actual)
        {
            if (actual != JsonValueType.Float && actual != JsonValueType.Int)
            {
                throw new Exception("Attempted number conversion operation on invalid JSON type");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static unsafe void CheckNotSameObject(in TinyJsonData source, in TinyJsonData destination)
        {
            if (source.m_BoolBuffer.GetUnsafePtr() == destination.m_BoolBuffer.GetUnsafePtr())
            {
                throw new Exception("Attempted to deep copy a JSON object into itself. Try making a copy");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void ThrowKeyNotFoundException(FixedString128 key)
        {
            throw new KeyNotFoundException($"Could not modify field in JSON object: Failed find key {key}");
        }

        public struct Enumerator : IEnumerator<JsonKeyValue>
        {
            internal UnsafeHashMap<FixedString128, JsonKeyHandle>.Enumerator m_Enumerator;
            internal TinyJsonData m_Data;

            public void Dispose() => m_Enumerator.Dispose();

            public bool MoveNext() => m_Enumerator.MoveNext();

            public void Reset() => m_Enumerator.Reset();

            public JsonKeyValue Current => new JsonKeyValue { Key = m_Enumerator.Current.Key, Value = m_Data.CreateObjectFromJsonHandle(m_Enumerator.Current.Value) };

            object IEnumerator.Current => new NotImplementedException();
        }

        public IEnumerator<JsonKeyValue> GetEnumerator()
        {
            CheckIsCorrectType(Type, JsonValueType.Object);
            return new Enumerator { m_Data = m_Data, m_Enumerator = m_Data.m_RootObjMap.GetEnumerator() };
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public struct JsonKeyValue
    {
        public FixedString128 Key;
        public TinyJsonObject Value;
    }

}
