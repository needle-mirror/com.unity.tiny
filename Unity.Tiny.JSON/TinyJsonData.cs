using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Tiny.JSON
{
    /**
     * Contains the buffers that store the data accessible from a TinyJsonObject
     * As well as internal methods to read, write, and remove data in the Json type buffers.
     */
    unsafe partial struct TinyJsonData : IDisposable
    {
        readonly Allocator m_Allocator;


        //pointed to by handle in json value ref
        internal NativeList<JsonKey> m_JsonKeyBuffer;
        internal NativeList<int> m_ValueRefFreeList;

        // the parent object map. this is updated when traversing nested objects
        internal UnsafeHashMap<FixedString128, JsonKeyHandle> m_RootObjMap;

        // these lists are referred to by JsonValueRefs, which point to a specific list at a specific index.
        // this allows for a flatter deserialized json representation even with deeply nested objects and arrays
        internal NativeList<bool> m_BoolBuffer;
        internal NativeList<int> m_IntBuffer;
        internal NativeList<float> m_FloatBuffer;
        internal NativeList<FixedString4096> m_StringBuffer; //todo: heap string(?)
        internal NativeList<UnsafeList<JsonKeyHandle>> m_ArrayBuffer;
        NativeList<UnsafeHashMap<FixedString128, JsonKeyHandle>> m_ObjectBuffer;


        /**
         * Initialize a mutable JSON container without deserializing existing data.
         */
        internal TinyJsonData(Allocator allocator)
        {
            var config = JsonDataConfiguration.Default;
            m_Allocator = allocator;
            m_BoolBuffer = new NativeList<bool>(config.boolCapacity, m_Allocator);
            m_IntBuffer = new NativeList<int>(config.intCapacity, m_Allocator);
            m_FloatBuffer = new NativeList<float>(config.floatCapacity, m_Allocator);
            m_StringBuffer = new NativeList<FixedString4096>(config.stringCapacity, m_Allocator);
            m_ArrayBuffer = new NativeList<UnsafeList<JsonKeyHandle>>(config.arrayCapacity, m_Allocator);
            m_ObjectBuffer = new NativeList<UnsafeHashMap<FixedString128, JsonKeyHandle>>(config.objectCapacity, m_Allocator);
            m_RootObjMap = new UnsafeHashMap<FixedString128, JsonKeyHandle>(config.fieldCapacity, m_Allocator);
            m_JsonKeyBuffer = new NativeList<JsonKey>(config.intCapacity, m_Allocator);
            m_ValueRefFreeList = new NativeList<int>(config.intCapacity, m_Allocator);
        }

        internal HeapString ToJson()
        {
            var buffer = new HeapString(Allocator.Persistent);
            SerializeObject(ref m_RootObjMap, ref buffer);
            return buffer;
        }

        public void Dispose()
        {
            m_JsonKeyBuffer.Dispose();
            m_ValueRefFreeList.Dispose();

            m_BoolBuffer.Dispose();
            m_IntBuffer.Dispose();
            m_FloatBuffer.Dispose();
            m_StringBuffer.Dispose();

            foreach (var list in m_ArrayBuffer)
            {
                list.Dispose();
            }

            m_ArrayBuffer.Dispose();

            foreach (var map in m_ObjectBuffer)
            {
                map.Dispose();
            }

            m_ObjectBuffer.Dispose();
            m_RootObjMap.Dispose();
        }

        internal void AddOrUpdateInt(FixedString128 key, TinyJsonObject value)
        {
            JsonKeyHandle jsonRefHandle;
            if (m_RootObjMap.ContainsKey(key))
            {
                jsonRefHandle = m_RootObjMap[key];
                if (JsonKeyType(jsonRefHandle) == JsonValueType.Int)
                {
                    // simple value swap if its the same type
                    m_IntBuffer[LocationInValueBuffer(jsonRefHandle)] = value.AsInt();
                    return;
                }

                // remove the old type completely and add a new one as normal
                RemoveValueFromBuffer(jsonRefHandle);
            }

            jsonRefHandle = new JsonKeyHandle(ref this, JsonValueType.Int, m_IntBuffer.Length);
            m_IntBuffer.Add(value.AsInt());
            m_RootObjMap[key] = jsonRefHandle;
        }

        /**
         * Adds the object to the correct buffer associated with the provided key.
         */
        internal void AddOrUpdateField(FixedString128 key, TinyJsonObject value)
        {
            switch (value.Type)
            {
                case JsonValueType.Bool:
                    AddOrUpdateBool(key, value);
                    break;
                case JsonValueType.Int:
                    AddOrUpdateInt(key, value);
                    break;
                case JsonValueType.Float:
                    AddOrUpdateFloat(key, value);
                    break;
                case JsonValueType.String:
                    AddOrUpdateString(key, value);
                    break;
                case JsonValueType.Array:
                    var tinyJsonArray = value.AsArray();
                    AddOrUpdateArray(key, ref tinyJsonArray);
                    break;
                case JsonValueType.Object:
                    AddOrUpdateObject(key, ref value.m_Data);
                    break;
                default:
                    InvalidJsonTypeException();
                    return;
            }
        }

        internal void AddOrUpdateString(FixedString128 key, TinyJsonObject value)
        {
            JsonKeyHandle jsonRefHandle;
            if (m_RootObjMap.ContainsKey(key))
            {
                jsonRefHandle = m_RootObjMap[key];
                if (JsonKeyType(jsonRefHandle) == JsonValueType.String)
                {
                    UpdateStringBufferFromObjectBasedOnVariant(value, jsonRefHandle);

                    return;
                }

                // remove the old type completely and add a new one as normal
                RemoveValueFromBuffer(jsonRefHandle);
            }

            jsonRefHandle = new JsonKeyHandle(ref this, JsonValueType.String, m_StringBuffer.Length);
            if (!value.m_StringVal.IsEmpty)
            {
                m_StringBuffer.Add(value.m_StringVal[0]);
                value.m_StringVal.Dispose();
            }
            else
            {
                m_StringBuffer.Add(value.m_Data.m_StringBuffer[value.m_Data.LocationInValueBuffer(value.m_StringRefHandle)]);
            }

            value.m_StringVal.Dispose();
            m_RootObjMap[key] = jsonRefHandle;
        }

        // A tiny json object of type string can either:
        // have its data uninitialized, but stringVal initialized with the string
        // or have its data initialized, and its stringVal is empty but its stringValueRef is initialized to its
        // location within the data buffer of the object
        void UpdateStringBufferFromObjectBasedOnVariant(TinyJsonObject value, JsonKeyHandle jsonRefHandle)
        {
            if (!value.m_StringVal.IsEmpty)
            {
                m_StringBuffer[LocationInValueBuffer(jsonRefHandle)] = value.m_StringVal[0];
                value.m_StringVal.Dispose();
            }
            else
            {
                m_StringBuffer[LocationInValueBuffer(jsonRefHandle)] = value.m_Data.m_StringBuffer[value.m_Data.LocationInValueBuffer(value.m_StringRefHandle)];
            }
        }

        internal void AddOrUpdateFloat(FixedString128 key, TinyJsonObject value)
        {
            JsonKeyHandle jsonRefHandle;
            if (m_RootObjMap.ContainsKey(key))
            {
                jsonRefHandle = m_RootObjMap[key];
                if (JsonKeyType(jsonRefHandle) == JsonValueType.Float)
                {
                    // simple value swap if its the same type
                    m_FloatBuffer[LocationInValueBuffer(jsonRefHandle)] = value.AsFloat();
                    return;
                }

                // remove the old type completely and add a new one as normal
                RemoveValueFromBuffer(jsonRefHandle);
            }

            jsonRefHandle = new JsonKeyHandle(ref this, JsonValueType.Float, m_FloatBuffer.Length);
            m_FloatBuffer.Add(value.AsFloat());
            m_RootObjMap[key] = jsonRefHandle;
        }

        internal void AddOrUpdateBool(FixedString128 key, TinyJsonObject value)
        {
            JsonKeyHandle jsonRefHandle;
            if (m_RootObjMap.ContainsKey(key))
            {
                jsonRefHandle = m_RootObjMap[key];
                if (JsonKeyType(jsonRefHandle) == JsonValueType.Bool)
                {
                    // simple value swap if its the same type
                    m_BoolBuffer[LocationInValueBuffer(jsonRefHandle)] = value.AsBool();
                    return;
                }

                // remove the old type completely and add a new one as normal
                RemoveValueFromBuffer(jsonRefHandle);
            }

            jsonRefHandle = new JsonKeyHandle(ref this, JsonValueType.Bool, m_BoolBuffer.Length);
            m_BoolBuffer.Add(value.AsBool());
            m_RootObjMap[key] = jsonRefHandle;
        }

        internal void AddOrUpdateArray(FixedString128 key, ref TinyJsonArray source)
        {
            var newArray = AddOrReplaceArrayField(key);
            CheckNotSameDataAllocation(source.m_Data, this);
            newArray.DeepCopyArrayValues(ref source);
        }

        internal void AddOrUpdateObject(FixedString128 key, ref TinyJsonData source)
        {
            var newObj = CreateObjectField(key);
            newObj.m_Data.DeepCopyFields(ref source);
        }

        /**
         * When a value is deleted, calling RemoveAtSwapBack() on the value buffer
         * for the corresponding type invalidates the location index stored in
         * the JsonValueRef pointing to the previous end of the buffer
         *
         * Search for the latter JsonValueRef, and override its location field
         * with the location contained in the ref pointed to by the handle we are about to free.
         */
        void UpdateStaleReference(JsonKeyHandle refHandleToFree,
            int indexReferencedByStaleRef)
        {
            var refBuffer = m_JsonKeyBuffer;

            // "delete" old reference
            m_ValueRefFreeList.Add(refHandleToFree.handle);
            ((JsonKey*)refBuffer.GetUnsafePtr() + refHandleToFree.handle)->IsFreed = true;

            // no swapping is needed because no value became stale: if we are deleting the last reference in the buffer
            // or the element at the end of the buffer
            if (LocationInValueBuffer(refHandleToFree) == indexReferencedByStaleRef || refBuffer.Length < 2)
            {
                return;
            }

            // find the stale reference and update its location to be the same as the deleted handle
            // (since the json type was moved to the deleted handle's location)
            for (int i = 0; i < refBuffer.Length; i++)
            {
                if (!refBuffer[i].IsFreed && refBuffer[i].HasTypeAndLocation(JsonKeyType(refHandleToFree), indexReferencedByStaleRef))
                {
                    ((JsonKey*)refBuffer.GetUnsafePtr() + i)->Location = LocationInValueBuffer(refHandleToFree);
                    return;
                }
            }

            JsonValueReferenceNotFoundException();
        }

        internal void RemoveValueFromBuffer(JsonKeyHandle jsonRefHandle)
        {
            // the JsonValueRef location at the end of a type buffer will be invalidated when removing from that buffer
            // due to calling RemoveAtSwapBack()
            // so scan through the value refs to find the one pointing to the end of the affected buffer and update
            // its location
            int locationInStaleRef;
            switch (JsonKeyType(jsonRefHandle))
            {
                case JsonValueType.Bool:
                    locationInStaleRef = m_BoolBuffer.Length - 1;
                    UpdateStaleReference(jsonRefHandle, locationInStaleRef);
                    m_BoolBuffer.RemoveAtSwapBack(LocationInValueBuffer(jsonRefHandle));
                    break;
                case JsonValueType.Int:
                    locationInStaleRef = m_IntBuffer.Length - 1;
                    UpdateStaleReference(jsonRefHandle, locationInStaleRef);
                    m_IntBuffer.RemoveAtSwapBack(LocationInValueBuffer(jsonRefHandle));
                    break;
                case JsonValueType.Float:
                    locationInStaleRef = m_FloatBuffer.Length - 1;
                    UpdateStaleReference(jsonRefHandle, locationInStaleRef);
                    m_FloatBuffer.RemoveAtSwapBack(LocationInValueBuffer(jsonRefHandle));
                    break;
                case JsonValueType.String:
                    locationInStaleRef = m_StringBuffer.Length - 1;
                    UpdateStaleReference(jsonRefHandle, locationInStaleRef);
                    m_StringBuffer.RemoveAtSwapBack(LocationInValueBuffer(jsonRefHandle));
                    break;
                case JsonValueType.Array:
                    // first remove all values in the array
                    var array = m_ArrayBuffer[LocationInValueBuffer(jsonRefHandle)];
                    for (int i = 0; i < array.Length; i++)
                    {
                        RemoveValueFromBuffer(array[i]);
                    }

                    // before removing the array itself
                    locationInStaleRef = m_ArrayBuffer.Length - 1;
                    UpdateStaleReference(jsonRefHandle, locationInStaleRef);
                    m_ArrayBuffer.RemoveAtSwapBack(LocationInValueBuffer(jsonRefHandle));
                    array.Dispose();
                    break;
                case JsonValueType.Object:
                    // first remove all values in the object
                    var unsafeHashMap = m_ObjectBuffer[LocationInValueBuffer(jsonRefHandle)];
                    foreach (var keyValue in unsafeHashMap)
                    {
                        RemoveValueFromBuffer(keyValue.Value);
                    }

                    // before removing the object itself
                    locationInStaleRef = m_ObjectBuffer.Length - 1;
                    UpdateStaleReference(jsonRefHandle, locationInStaleRef);
                    m_ObjectBuffer.RemoveAtSwapBack(LocationInValueBuffer(jsonRefHandle));
                    unsafeHashMap.Dispose();
                    break;
                default:
                    InvalidJsonTypeException();
                    return;
            }
        }

        internal TinyJsonObject GetObjectFromKey(FixedString128 key)
        {
            var jsonValueRef = m_RootObjMap[key];
            return CreateObjectFromJsonHandle(jsonValueRef);
        }

        internal TinyJsonObject CreateObjectFromJsonHandle(JsonKeyHandle jsonKeyHandle)
        {
            TinyJsonObject value = new TinyJsonObject { m_Data = this, Type = JsonKeyType(jsonKeyHandle) };
            switch (JsonKeyType(jsonKeyHandle))
            {
                case JsonValueType.Bool:
                    value.m_BoolVal = m_BoolBuffer[LocationInValueBuffer(jsonKeyHandle)];
                    break;
                case JsonValueType.Int:
                    value.m_IntVal = m_IntBuffer[LocationInValueBuffer(jsonKeyHandle)];
                    break;
                case JsonValueType.Float:
                    value.m_FloatVal = m_FloatBuffer[LocationInValueBuffer(jsonKeyHandle)];
                    break;
                case JsonValueType.String:
                    value.m_StringRefHandle = jsonKeyHandle;
                    break;
                case JsonValueType.Array:
                    value.m_ArrayRefHandle = jsonKeyHandle;
                    break;
                case JsonValueType.Object:
                    value.m_Data.m_RootObjMap = m_ObjectBuffer[LocationInValueBuffer(jsonKeyHandle)];
                    break;
            }

            return value;
        }

        internal void DeepCopyFields(ref TinyJsonData source)
        {
            TinyJsonObject.CheckNotSameObject(source, this);

            foreach (var keyValue in source.m_RootObjMap)
            {
                var objectCopy = source.CreateObjectFromJsonHandle(keyValue.Value); // read from source
                AddOrUpdateField(keyValue.Key, objectCopy); // write to destination
            }
        }

        /**
         * Creates a new, empty array associated with the key. Replaces the key if it exists.
         */
        internal TinyJsonArray AddOrReplaceArrayField(FixedString128 key)
        {
            // delete old key
            if (m_RootObjMap.ContainsKey(key))
            {
                var handle = m_RootObjMap[key];
                RemoveValueFromBuffer(handle);
            }

            // create new array
            var jsonValRef = CreateNestedArray(out var array);
            m_RootObjMap[key] = jsonValRef;
            return array;
        }

        internal JsonKeyHandle CreateNestedArray(out TinyJsonArray array)
        {
            var jsonHandle = new JsonKeyHandle (ref this, JsonValueType.Array, m_ArrayBuffer.Length);
            var arrayValue = new UnsafeList<JsonKeyHandle>(10, m_Allocator);
            m_ArrayBuffer.Add(arrayValue);
            array = CreateObjectFromJsonHandle(jsonHandle);
            return jsonHandle;
        }

        /** Creates a new, empty object field or replaces an old one, deleting its data */
        internal TinyJsonObject CreateObjectField(FixedString128 key)
        {
            // delete old object
            if (m_RootObjMap.ContainsKey(key))
            {
                var objectHandle = m_RootObjMap[key];
                RemoveValueFromBuffer(objectHandle);
            }

            // create new one
            var jsonValRef = CreateNestedObject(out var value);
            m_RootObjMap[key] = jsonValRef;
            return value;
        }

        internal JsonKeyHandle CreateNestedObject(out TinyJsonObject value)
        {
            var handle = new JsonKeyHandle(ref this, JsonValueType.Object, m_ObjectBuffer.Length);
            var objMap = new UnsafeHashMap<FixedString128, JsonKeyHandle>(10, m_Allocator);
            m_ObjectBuffer.Add(objMap);
            value = new TinyJsonObject
            {
                m_Data = this, Type = JsonValueType.Object
            };
            value.m_Data.m_RootObjMap = objMap;
            return handle;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static void CheckNotSameDataAllocation(in TinyJsonData a, in TinyJsonData b)
        {
            if (a.m_BoolBuffer.GetUnsafePtr() == b.m_BoolBuffer.GetUnsafePtr())
            {
                throw new Exception("Attempted to deep copy a JSON type into itself. Try making a copy");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static void InvalidJsonTypeException()
        {
            throw new Exception("JSON Object type could not be resolved ");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static void JsonValueReferenceNotFoundException()
        {
            throw new Exception("Could not find json handle. Complain to @destin-estrela");
        }

        internal int LocationInValueBuffer(JsonKeyHandle jsonKeyHandle)
        {
            return m_JsonKeyBuffer[jsonKeyHandle.handle].Location;
        }

        internal JsonValueType JsonKeyType(JsonKeyHandle jsonKeyHandle)
        {
            return m_JsonKeyBuffer[jsonKeyHandle.handle].Type;
        }
    }

    public enum JsonValueType
    {
        Bool,
        Int,
        Float,
        String,
        Array,
        Object
    }

    /**
     * Contains the location data for finding a json value type.
     * The location is not fixed: Some operations may invalidate the JsonKey.
     */
    struct JsonKey //  data layout: [0000 0000 0000 0000 0000 0000 0000]: location [000]: type [0]: IsFreed
    {
        static readonly ushort k_TypeMask = 0x0e;
        static readonly uint k_InvTypeMask = (uint)~k_TypeMask;
        static readonly uint k_LocationMask = 0xfffffff0;
        static readonly ushort k_InvLocationMask = (ushort)~k_LocationMask;
        static readonly ushort k_FreedMask = 0x01;
        static readonly uint k_InvFreedMask = (uint)~k_FreedMask;
        const int k_TypeShift = 1;
        const int k_LocationShift = 4;
        public JsonValueType Type
        {
            get => (JsonValueType)((m_Data & k_TypeMask) >> k_TypeShift);
            set => m_Data = (m_Data & k_InvTypeMask) | (ushort)(((ushort)value << k_TypeShift) & k_TypeMask);
        }

        public int Location
        {
            get => (int)m_Data >> 4;
            set => m_Data = (m_Data & k_InvLocationMask) | ((uint)value << k_LocationShift);
        }

        public bool IsFreed
        {
            get => (m_Data & k_FreedMask) == 1;
            set => m_Data = (m_Data & k_InvFreedMask) | (ushort)(value ? 1 : 0);
        }

        uint m_Data;

        public bool HasTypeAndLocation(JsonValueType type, int location) =>
            Type == type
            && Location == location;
    }

    readonly struct JsonKeyHandle
    {
        internal readonly int handle;

        internal JsonKeyHandle(ref TinyJsonData data, JsonValueType type, int indexIntoBuffer)
        {
            var jsonKey = new JsonKey { Type = type, Location = indexIntoBuffer, IsFreed = false };

            var freeList = data.m_ValueRefFreeList;
            if (freeList.Length > 0)
            {
                handle = freeList[freeList.Length - 1];
                data.m_JsonKeyBuffer[handle] = jsonKey;
                freeList.RemoveAt(freeList.Length - 1);
            }
            else
            {
                data.m_JsonKeyBuffer.Add(jsonKey);
                handle = data.m_JsonKeyBuffer.Length - 1;
            }
        }
    }

    /// <summary>
    /// Parameters used to configure the <see cref="TinyJsonData"/>.
    /// Use to prevent unnecessary resizing.
    /// </summary>
    public struct JsonDataConfiguration
    {
        public int fieldCapacity;
        public int boolCapacity;
        public int intCapacity;
        public int floatCapacity;
        public int stringCapacity;
        public int objectCapacity;
        public int arrayCapacity;

        public static readonly JsonDataConfiguration Default = new JsonDataConfiguration
        {
            fieldCapacity = 50,
            boolCapacity = 50,
            intCapacity = 50,
            floatCapacity = 50,
            stringCapacity = 50,
            objectCapacity = 50,
            arrayCapacity = 50
        };
    }
}
