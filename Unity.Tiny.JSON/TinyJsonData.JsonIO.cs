using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Serialization.Json;

namespace Unity.Tiny.JSON
{
    /**
     * Contains methods for parsing serialized json string data and reserializing buffer data.
     */
    unsafe partial struct TinyJsonData
    {
        /**
         * Deserialize the input string into a series of buffers
         */
        internal TinyJsonData(string jsonString, Allocator allocator)
        {
            var config = JsonDataConfiguration.Default;
            m_Allocator = allocator;
            m_BoolBuffer = new NativeList<bool>(config.boolCapacity, m_Allocator);
            m_IntBuffer = new NativeList<int>(config.intCapacity, m_Allocator);
            m_FloatBuffer = new NativeList<float>(config.floatCapacity, m_Allocator);
            m_StringBuffer = new NativeList<FixedString4096>(config.stringCapacity, m_Allocator);
            m_ArrayBuffer = new NativeList<UnsafeList<JsonKeyHandle>>(config.arrayCapacity, m_Allocator);
            m_ObjectBuffer = new NativeList<UnsafeHashMap<FixedString128, JsonKeyHandle>>(config.objectCapacity, m_Allocator);

            m_JsonKeyBuffer = new NativeList<JsonKey>(config.intCapacity, m_Allocator);
            m_ValueRefFreeList = new NativeList<int>(config.intCapacity, m_Allocator);

            // parse existing json into a packed binary stream
            SerializedObjectReader reader;
            fixed (char* ptr = jsonString)
            {
                reader = new SerializedObjectReader(ptr, jsonString.Length, SerializedObjectReaderConfiguration.Default);
            }

            // determine the type and copy each field-value pair into the mutable map / buffers
            m_RootObjMap = new UnsafeHashMap<FixedString128, JsonKeyHandle>(config.fieldCapacity, m_Allocator);
            m_JsonKeyBuffer.Add(new JsonKey { Type = JsonValueType.Object, Location = 0 });

            ReadObjectViewIntoMap(reader.ReadObject(), ref m_RootObjMap);
            reader.Dispose();
        }

        /**
         * Deserialize each field in an object
         */
        void ReadObjectViewIntoMap(SerializedObjectView objView, ref UnsafeHashMap<FixedString128, JsonKeyHandle> objMap)
        {
            var serializedViewEnum = objView.GetEnumerator();
            while (serializedViewEnum.MoveNext())
            {
                var view = serializedViewEnum.Current;
                var typeInfo = DeserializeValueView(view.Value());
                FixedString64 fs = default;
                fs.Append(view.Name().ToString());
                objMap.TryAdd(fs , typeInfo);
            }

            serializedViewEnum.Dispose();
        }

        /**
         * Determine the type of a value view and fully deserialize it into the correct buffer.
         */
        JsonKeyHandle DeserializeValueView(SerializedValueView valueView)
        {
            JsonKeyHandle jsonKeyHandle;
            switch (valueView.Type)
            {
                case TokenType.Primitive:
                    jsonKeyHandle = DeserializeToPrimitiveBuffer(valueView.AsPrimitiveView());
                    break;
                case TokenType.String:
                    jsonKeyHandle = DeserializeToStringBuffer(valueView.AsStringView());
                    break;
                case TokenType.Object:
                    jsonKeyHandle = DeserializeToObjectBuffer(valueView.AsObjectView());
                    break;
                case TokenType.Array:
                    jsonKeyHandle = DeserializeToArrayBuffer(valueView.AsArrayView());
                    break;
                default:
                    jsonKeyHandle = default;
                    break;
            }

            return jsonKeyHandle;
        }

        /**
         * Determine which primitive type the SerializedPrimitiveView is
         * and add the fully deserialized primitive to the correct buffer.
         */
        JsonKeyHandle DeserializeToPrimitiveBuffer(SerializedPrimitiveView primView)
        {
            JsonKeyHandle jsonRefHandle;
            if (primView.IsBoolean())
            {
                jsonRefHandle = new JsonKeyHandle(ref this, JsonValueType.Bool, m_BoolBuffer.Length);
                m_BoolBuffer.Add(primView.AsBoolean());
            }
            else if (primView.IsDecimal())
            {
                jsonRefHandle = new JsonKeyHandle(ref this, JsonValueType.Float, m_FloatBuffer.Length);
                m_FloatBuffer.Add(primView.AsFloat());
            }
            else if (primView.IsIntegral())
            {
                jsonRefHandle = new JsonKeyHandle(ref this, JsonValueType.Int, m_IntBuffer.Length);
                m_IntBuffer.Add((int)primView.AsInt64());
            }
            else
            {
                throw new ArgumentOutOfRangeException("Primitive type not supported"); // todo move to func
            }

            return jsonRefHandle;
        }

        /**
         * Deserialize a string view into the string buffer
         */
        JsonKeyHandle DeserializeToStringBuffer(SerializedStringView stringView)
        {
            FixedString4096 fs = default;
            fs.Append(stringView.ToString());
            m_StringBuffer.Add(fs);
            return new JsonKeyHandle(ref this, JsonValueType.String, m_StringBuffer.Length - 1);
        }

        /**
         * Deserialize an object view into the object buffer.
         */
        JsonKeyHandle DeserializeToObjectBuffer(SerializedObjectView objectView)
        {
            var newObjMap = new UnsafeHashMap<FixedString128, JsonKeyHandle>(50, m_Allocator);
            ReadObjectViewIntoMap(objectView, ref newObjMap);
            m_ObjectBuffer.Add(newObjMap);

            return new JsonKeyHandle(ref this, JsonValueType.Object, m_ObjectBuffer.Length - 1);
        }

        /**
         * Deserialize an array into the array buffer.
         */
        JsonKeyHandle DeserializeToArrayBuffer(SerializedArrayView arrayView)
        {
            // construct NativeList<JsonValueRef> then add to buffer
            var values = new UnsafeList<JsonKeyHandle>(10, m_Allocator);
            var arrayEnum = arrayView.GetEnumerator();
            while (arrayEnum.MoveNext())
            {
                var view = arrayEnum.Current;
                values.Add(DeserializeValueView(view));
            }

            arrayEnum.Dispose();

            m_ArrayBuffer.Add(values);
            return new JsonKeyHandle(ref this, JsonValueType.Array, m_ArrayBuffer.Length - 1);
        }

        /**
         * Serializes an object along with all of its fields back into JSON format.
         */
        void SerializeObject(ref UnsafeHashMap<FixedString128, JsonKeyHandle> objMap, ref HeapString buffer)
        {
            buffer.Append((FixedString32)"{");

            // print out fields in alphabetical order
            var keyList = objMap.GetKeyArray(Allocator.Temp);
            keyList.Sort();

            for (int i = 0; i < keyList.Length; i++)
            {
                buffer.AppendFormat((FixedString32)"\"{0}\": ", keyList[i]);
                SerializeValueView(objMap[keyList[i]], ref buffer);
                if (i < keyList.Length - 1) buffer.Append((FixedString32)", ");
            }

            buffer.Append((FixedString32)"}");
        }

        /**
         * Retrieves and converts the deserialized value into its serialized json representation.
         */
        void SerializeValueView(JsonKeyHandle info, ref HeapString buffer)
        {
            switch (JsonKeyType(info))
            {
                case JsonValueType.Bool:
                    buffer.Append(m_BoolBuffer.ElementAt(LocationInValueBuffer(info)) ? (FixedString32)"true" : (FixedString32)"false");
                    break;
                case JsonValueType.Int:
                    buffer.Append(m_IntBuffer.ElementAt(LocationInValueBuffer(info)));
                    break;
                case JsonValueType.Float:
                    buffer.Append(m_FloatBuffer.ElementAt(LocationInValueBuffer(info)));
                    break;
                case JsonValueType.String:
                    buffer.Append((FixedString32)"\"");
                    buffer.Append(m_StringBuffer.ElementAt(LocationInValueBuffer(info)));
                    buffer.Append((FixedString32)"\"");
                    break;
                case JsonValueType.Array:
                    SerializeArray(m_ArrayBuffer.ElementAt(LocationInValueBuffer(info)), ref buffer);
                    break;
                case JsonValueType.Object:
                    SerializeObject(ref m_ObjectBuffer.ElementAt(LocationInValueBuffer(info)), ref buffer);
                    break;
                default:
                    InvalidJsonTypeException();
                    break;
            }
        }

        /**
         * Serializes an array into JSON format.
         */
        void SerializeArray(in UnsafeList<JsonKeyHandle> array, ref HeapString buffer)
        {
            buffer.Append((FixedString32)"[");
            for (int i = 0; i < array.Length; i++)
            {
                SerializeValueView(array[i], ref buffer);
                if (i < array.Length - 1) buffer.Append((FixedString32)", ");
            }

            buffer.Append((FixedString32)"]");
        }
    }
}
