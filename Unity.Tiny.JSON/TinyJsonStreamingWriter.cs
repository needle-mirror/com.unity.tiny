using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Tiny.JSON
{
    /**
     * Writes JSON to a string in a stream style interface.
     */
    public struct TinyJsonStreamingWriter : IDisposable
    {
        HeapString m_Buffer;

        // Track state when navigating nested objects / arrays
        // 0+ represents an array and the index of the last item.
        const int k_EmptyObject = -2;
        const int k_NonEmptyObject = -1;
        const int k_EmptyArray = 0;
        SimpleStack m_Stack;
        bool m_WroteToString;

        public unsafe TinyJsonStreamingWriter(Allocator allocator, int expectedSize = 50, int expectedDepth = 5)
        {
            m_WroteToString = false;
            m_Stack = new SimpleStack(expectedDepth, allocator);
            m_Buffer = new HeapString(expectedSize, allocator);
            m_Buffer.Append(((FixedString128) "{ ").GetUnsafePtr(), ((FixedString128) "{ ").Length);
            m_Stack.Push(k_EmptyObject);
        }

        public unsafe void PopObject()
        {
            ValidateInObject();
            ValidateInNestedView();
            m_Buffer.Append(((FixedString128) " }").GetUnsafePtr(), ((FixedString128) " }").Length);
            m_Stack.Pop();
        }

        public unsafe void PopArray()
        {
            ValidateInArray();
            ValidateInNestedView();
            m_Buffer.Append(((FixedString128) "]").GetUnsafePtr(), ((FixedString128) "]").Length);
            m_Stack.Pop();
        }

        public unsafe void PushObjectField(FixedString128 key)
        {
            AddCommaIfRequired();
            ValidateInObject();
            FixedString128 str = $"\"{key}\": ";
            str.Append((FixedString32)"{ ");
            m_Buffer.Append(str.GetUnsafePtr(), str.Length);
            m_Stack.Push(k_EmptyObject);
        }

        public unsafe void PushArrayField(FixedString128 key)
        {
            AddCommaIfRequired();
            ValidateInObject();
            m_Stack.Push(k_EmptyArray);
            FixedString128 str = $"\"{key}\": [";
            m_Buffer.Append(str.GetUnsafePtr(), str.Length);
        }

        public unsafe void PushValueField(FixedString128 key, bool value)
        {
            AddCommaIfRequired();
            ValidateInObject();
            var boolString = (value ? (FixedString128)"true" : (FixedString128)"false");
            FixedString128 str = $"\"{key}\": {boolString}";

            m_Buffer.Append(str.GetUnsafePtr(), str.Length);
        }

        public unsafe void PushValueField(FixedString128 key, int value)
        {
            AddCommaIfRequired();
            ValidateInObject();
            FixedString128 str = $"\"{key}\": {value}";
            m_Buffer.Append(str.GetUnsafePtr(), str.Length);
        }

        public unsafe void PushValueField(FixedString128 key, float value)
        {
            AddCommaIfRequired();
            ValidateInObject();
            FixedString128 str = $"\"{key}\": {value}";
            m_Buffer.Append(str.GetUnsafePtr(), str.Length);
        }

        public unsafe void PushValueField(FixedString128 key, FixedString4096 value)
        {
            AddCommaIfRequired();
            ValidateInObject();
            FixedString128 str = $"\"{key}\": \"{value}\"";
            m_Buffer.Append(str.GetUnsafePtr(), str.Length);
        }

        public unsafe int PushValueToArray(bool value)
        {
            AddCommaIfRequired();
            ValidateInArray();
            int index = m_Stack.Peek();
            FixedString128 str = value ? (FixedString128)"true" : (FixedString128)"false";
            m_Buffer.Append(str.GetUnsafePtr(), str.Length);
            m_Stack.IncrementTop();
            return index;
        }

        public unsafe int PushValueToArray(int value)
        {
            AddCommaIfRequired();
            ValidateInArray();
            int index = m_Stack.Peek();
            m_Buffer.Append(((FixedString128) $"{value}").GetUnsafePtr(), ((FixedString128)$"{value}").Length);
            m_Stack.IncrementTop();
            return index;
        }

        public unsafe int PushValueToArray(float value)
        {
            AddCommaIfRequired();
            ValidateInArray();
            int index = m_Stack.Peek();
            m_Buffer.Append(((FixedString128) $"{value}").GetUnsafePtr(), ((FixedString128)$"{value}").Length);
            m_Stack.IncrementTop();
            return index;
        }

        public unsafe int PushValueToArray(FixedString4096 value)
        {
            AddCommaIfRequired();
            ValidateInArray();
            int index = m_Stack.Peek();
            FixedString4096 str = $"\"{value}\"";
            m_Buffer.Append(str.GetUnsafePtr(), str.Length);
            m_Stack.IncrementTop();
            return index;
        }

        public unsafe void PushArrayToArray()
        {
            AddCommaIfRequired();
            ValidateInArray();
            m_Stack.IncrementTop();
            m_Stack.Push(k_EmptyArray);
            m_Buffer.Append(((FixedString128) "[").GetUnsafePtr(), ((FixedString128) "[").Length);
        }

        public unsafe void PushObjectToArray()
        {
            AddCommaIfRequired();
            ValidateInArray();
            m_Stack.IncrementTop();
            m_Buffer.Append(((FixedString128) "{ ").GetUnsafePtr(), ((FixedString128) "{ ").Length);
            m_Stack.Push(k_EmptyObject);
        }

        // gives ownership of the allocation to the user
        public HeapString WriteToString()
        {
            PopObject();
            ValidateObjectComplete();
            m_WroteToString = true;
            return m_Buffer;
        }

        public TinyJsonInterface WriteToInterface(Allocator allocator)
        {
            PopObject();
            ValidateInObject();

            return new TinyJsonInterface(ConvertBufferToStr(), allocator);
        }

        public void Dispose()
        {
            m_Stack.list.Dispose();

            // only dispose if we haven't given ownership to the user
            if (!m_WroteToString)
            {
                m_Buffer.Dispose();
            }
        }

        unsafe string ConvertBufferToStr()
        {
            var cs = stackalloc char[m_Buffer.Length * 2];
            int length;
            Unicode.Utf8ToUtf16((byte*)m_Buffer.GetUnsafePtr(), m_Buffer.Length, cs, out length, m_Buffer.Length * 2);
            var str = new string(cs, 0, length);
            return str;
        }

        /**
         * Depending on state:
         * Append a comma before adding a new field if not the first field in an object.
         * Or append a comma if not the first element in an array
         */
        unsafe void AddCommaIfRequired()
        {
            if (!m_Stack.IsEmpty())
            {
                var curState = m_Stack.Peek();
                if (curState == k_NonEmptyObject || curState > k_EmptyArray)
                {
                    m_Buffer.Append(((FixedString32) ", ").GetUnsafePtr(), ((FixedString32) ", ").Length);
                }

                if (curState == k_EmptyObject)
                {
                    m_Stack.SetTop(k_NonEmptyObject);
                }
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void ValidateInArray()
        {
            var curState = m_Stack.Peek();
            if (curState == k_EmptyObject || curState == k_NonEmptyObject)
            {
                throw new Exception("Called an object method while in an array.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void ValidateInObject()
        {
            if (m_Stack.Peek() >= k_EmptyArray)
            {
                throw new Exception("Called an array method while in object.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void ValidateInNestedView()
        {
            if (m_Stack.list.Length < 1)
            {
                throw new Exception("Called PopObject() or PopArray() too many times.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void ValidateObjectComplete()
        {
            if (m_Stack.list.Length > 0)
            {
                throw new Exception("Tried to write an incomplete json object! Did you forget a PopObject() or PopArray() call?");
            }
        }

        /**
         * A helper class to track and update writer state.
         */
        struct SimpleStack : IDisposable
        {
            internal NativeList<Int32> list;

            internal SimpleStack(int size, Allocator allocator)
            {
                list = new NativeList<int>(size, allocator);
            }

            internal void Push(int value)
            {
                list.Add(value);
            }

            internal int Pop()
            {
                int ret = list[list.Length - 1];
                list.RemoveAt(list.Length - 1);
                return ret;
            }

            internal int Peek()
            {
                return list[list.Length - 1];
            }

            internal void IncrementTop()
            {
                list[list.Length - 1]++;
            }

            internal void SetTop(int val)
            {
                list[list.Length - 1] = val;
            }

            internal bool IsEmpty()
            {
                return list.Length == 0;
            }

            public void Dispose()
            {
                list.Dispose();
            }
        }
    }
}
