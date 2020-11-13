using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Platforms;
using Unity.Tiny;

[assembly: InternalsVisibleTo("Unity.Tiny.GameSave.Tests")]

namespace Unity.Tiny.GameSave
{
    public enum GameSaveType
    {
        Unknown,
        Bool,
        Byte,
        Sbyte,
        Char,
        Short,
        Ushort,
        Int,
        Uint,
        Float,
        Double,
        Long,
        Ulong,
        FixedString64,
        FixedString128,
        ComponentData,
        SharedComponentData,
        BufferData,
        Struct,
        ByteArray,
        Count
    }

    public enum GameSaveResult
    {
        NotStarted,
        ReadInProgress,
        WriteInProgress,
        Success,
        ErrorNotFound,
        ErrorType,
        ErrorDataSize,
        ErrorReadFailed,
        ErrorMultipleRequestsToSameFile,
        ErrorWriteFailed
    }

    public struct GameSaveReadFromPersistentStorageRequest : IComponentData
    {
        public GameSaveReadFromPersistentStorageRequest(FixedString128 fp, int index)
        {
            filePath = fp;
            gameSaveIndex = index;
            result = GameSaveResult.NotStarted;
        }

        public FixedString128 filePath;
        public int gameSaveIndex;
        public GameSaveResult result;        
    }

    public struct GameSaveWriteToPersistentStorageRequest : IComponentData
    {
        public GameSaveWriteToPersistentStorageRequest(FixedString128 fp, int index)
        {
            filePath = fp;
            gameSaveIndex = index;
            result = GameSaveResult.NotStarted;
        }

        public FixedString128 filePath;
        public int gameSaveIndex;
        public GameSaveResult result;
    }

    public struct GameSaveReadFromBufferRequest : IComponentData
    {
        public int gameSaveIndex;
        public GameSaveResult result;
    }

    public struct GameSaveWriteToBufferRequest : IComponentData
    {
        public int gameSaveIndex;
        public GameSaveResult result;
    }

    public struct GameSaveBufferByte : IBufferElementData
    {
        public byte value;
    }

    internal unsafe struct GameSaveData
    {
        public int typeIndex;
        public int numElements;
        public ulong buffer;
        public int bufferSize;
    }

    internal unsafe struct GameSaveTypeInfo
    {
        // Type information.
        public GameSaveType gameSaveType;
        public ulong stableTypeHash;        
        public int size;

        // Fields within the type.
        public int numFields;
        public GameSaveFieldInfo* fields;
    }

    internal struct GameSaveTypeKey : IEquatable<GameSaveTypeKey>
    {
        public GameSaveType gameSaveType;
        public ulong stableTypeHash;     

        public bool Equals(GameSaveTypeKey otherKey)
        {
            return (gameSaveType == otherKey.gameSaveType) && (stableTypeHash == otherKey.stableTypeHash);
        }

        public override int GetHashCode()
        {
            return (stableTypeHash > 0) ? (int)stableTypeHash : (int)gameSaveType;
        }
    }

    internal unsafe struct GameSaveFieldInfo
    {
        public FixedString64 name;
        public int offset;
        public GameSaveType gameSaveType;
        public int typeIndex;
    }

    public unsafe class GameSaveSystem : SystemBase
    {
        // This is our persistent data and types. This information is all stored to disk.
        private NativeHashMap<FixedString64, GameSaveData>[] m_PersistentData;
        private byte*[] m_PersistentDataBytes;
        private int*[] m_PersistentDataLength;
        private NativeList<GameSaveTypeInfo>[] m_PersistentTypes;

        // This is a temporary lookup data structure that is built up as we run and then thrown away each time we shut down the app.
        // It allows us to find our persistent type information based on a type.
        private NativeHashMap<GameSaveTypeKey, int>[] m_TypeToPersistentTypeIndexMap;

        private int m_NumGameSavesMax;
        private int m_GameSaveIndex;
        private static int m_NativeBytesAllocated = 0;
        private int[] m_GameSaveTypeSizes;
        private const int m_MinimumGameSaveFileSizeInBytes = 8;

        public int GameSaveIndex 
        { 
            get
            {
                return m_GameSaveIndex; 
            }

            set
            {
                if ((m_GameSaveIndex >= 0) && (m_GameSaveIndex < m_NumGameSavesMax)) 
                    m_GameSaveIndex = value;
            }
        }

        internal int NativeBytesAllocated => m_NativeBytesAllocated;

        protected override unsafe void OnCreate()
        {
            m_NumGameSavesMax = 16;
            m_GameSaveIndex = 0;

            m_PersistentData = new NativeHashMap<FixedString64, GameSaveData>[m_NumGameSavesMax];
            m_PersistentDataBytes = new byte*[m_NumGameSavesMax];
            m_PersistentDataLength = new int*[m_NumGameSavesMax];

            m_PersistentTypes = new NativeList<GameSaveTypeInfo>[m_NumGameSavesMax];
            m_TypeToPersistentTypeIndexMap = new NativeHashMap<GameSaveTypeKey, int>[m_NumGameSavesMax];

            for (int i = 0; i < m_NumGameSavesMax; i++)
            {
                m_PersistentData[i] = new NativeHashMap<FixedString64, GameSaveData>(64, Allocator.Persistent);
                m_PersistentDataBytes[i] = null;
                m_PersistentDataLength[i] = (int*)UnsafeUtility.Malloc(sizeof(int), 4, Allocator.Persistent);
                UnsafeUtility.MemSet(m_PersistentDataLength[i], 0x00, sizeof(int));

                m_PersistentTypes[i] = new NativeList<GameSaveTypeInfo>(16, Allocator.Persistent);
                m_TypeToPersistentTypeIndexMap[i] = new NativeHashMap<GameSaveTypeKey, int>(16, Allocator.Persistent);
            }

            InitGameSaveTypeSizes();
            InitGameSaveTypeLookup();

            GameSaveNativeCalls.Init();
        }

        protected override unsafe void OnUpdate()
        {
            var mgr = EntityManager;
            NativeHashMap<FixedString64, GameSaveData>[] persistentData = m_PersistentData;
            byte*[] persistentDataBytes = m_PersistentDataBytes;
            int*[] persistentDataLength = m_PersistentDataLength;
            NativeList<GameSaveTypeInfo>[] persistentTypes = m_PersistentTypes;
            NativeHashMap<GameSaveTypeKey, int>[] typeToPersistentTypeIndexMap = m_TypeToPersistentTypeIndexMap;
            NativeList<FixedString128> gameSaveRequestsAllowed = new NativeList<FixedString128>(Allocator.Temp);
            int numGameSavesMax = m_NumGameSavesMax;

            // Error-checking: There should only be one read/write request to each game save file at a time. If there are multiple requests to the same file, 
            // only allow one of them to proceed and set any other request's result to an error condition.

            // First, find any in-progress read/write requests. Add those requests to the "allowed requests list".
            Entities
                .WithoutBurst()
                .ForEach((
                    ref GameSaveWriteToPersistentStorageRequest writeRequest) =>
            {
                if (writeRequest.result == GameSaveResult.WriteInProgress)
                    gameSaveRequestsAllowed.Add(writeRequest.filePath);
            }).Run();

            Entities
                .WithoutBurst()
                .ForEach((
                    ref GameSaveReadFromPersistentStorageRequest readRequest) =>
            {
                if (readRequest.result == GameSaveResult.ReadInProgress)
                    gameSaveRequestsAllowed.Add(readRequest.filePath);
            }).Run();

            // Second, find the first request for each game save file. Add those requests to the "allowed requests list" if there isn't already
            // an allowed request for that game save file. Error out otherwise.
            Entities
                .WithoutBurst()
                .ForEach((
                    ref GameSaveWriteToPersistentStorageRequest writeRequest) =>
            {
                if (writeRequest.result == GameSaveResult.NotStarted)
                {
                    bool requestToSameFile = false;
                    for (int i = 0; i < gameSaveRequestsAllowed.Length; i++)
                    {
                        if (gameSaveRequestsAllowed[i] == writeRequest.filePath)
                        {
                            writeRequest.result = GameSaveResult.ErrorMultipleRequestsToSameFile;
                            requestToSameFile = true;
                        }
                    }

                    if (!requestToSameFile)
                        gameSaveRequestsAllowed.Add(writeRequest.filePath);
                }
            }).Run();

            Entities
                .WithoutBurst()
                .ForEach((
                    ref GameSaveReadFromPersistentStorageRequest readRequest) =>
            {
                if (readRequest.result == GameSaveResult.NotStarted)
                {
                    bool requestToSameFile = false;
                    for (int i = 0; i < gameSaveRequestsAllowed.Length; i++)
                    {
                        if (gameSaveRequestsAllowed[i] == readRequest.filePath)
                        {
                            readRequest.result = GameSaveResult.ErrorMultipleRequestsToSameFile;
                            requestToSameFile = true;
                        }
                    }

                    if (!requestToSameFile)
                        gameSaveRequestsAllowed.Add(readRequest.filePath);
                }
            }).Run();

            Entities
                .WithoutBurst()
                .ForEach((
                    ref GameSaveWriteToPersistentStorageRequest writeRequest) =>
            {
                if (writeRequest.result == GameSaveResult.NotStarted)
                {
                    if ((writeRequest.gameSaveIndex >= 0) && (writeRequest.gameSaveIndex < numGameSavesMax))
                    {
                        writeRequest.result = GameSaveResult.WriteInProgress;
                        bool success = WriteGameSaveDataToDisk(writeRequest.filePath, ref persistentData[writeRequest.gameSaveIndex], ref persistentTypes[writeRequest.gameSaveIndex]);
                        writeRequest.result = success ? GameSaveResult.Success : GameSaveResult.ErrorWriteFailed;
                    }
                    else
                    {
                        writeRequest.result = GameSaveResult.ErrorWriteFailed;
                    }
                }
            }).Run();

            Entities
                .WithoutBurst()
                .WithStructuralChanges()
                .ForEach((
                    Entity e,
                    ref GameSaveWriteToBufferRequest writeRequest) =>
            {
                if (writeRequest.result == GameSaveResult.NotStarted)
                {
                    if ((writeRequest.gameSaveIndex >= 0) && (writeRequest.gameSaveIndex < numGameSavesMax))
                    {
                        writeRequest.result = GameSaveResult.WriteInProgress;
                        DynamicBuffer<GameSaveBufferByte> buffer = mgr.AddBuffer<GameSaveBufferByte>(e);
                        bool success = WriteGameSaveDataToBuffer(ref buffer, ref persistentData[writeRequest.gameSaveIndex], ref persistentTypes[writeRequest.gameSaveIndex]);
                        writeRequest.result = success ? GameSaveResult.Success : GameSaveResult.ErrorWriteFailed;
                    }
                    else
                    {
                        writeRequest.result = GameSaveResult.ErrorWriteFailed;
                    }
                }
            }).Run();         

            Entities
                .WithoutBurst()
                .ForEach((
                    Entity e,
                    ref GameSaveReadFromPersistentStorageRequest readRequest) =>
            {
                bool validGameSaveIndex = (readRequest.gameSaveIndex >= 0) && (readRequest.gameSaveIndex < numGameSavesMax);
                if (!validGameSaveIndex)
                    readRequest.result = GameSaveResult.ErrorReadFailed;

                if (readRequest.result == GameSaveResult.NotStarted)
                {
                    if (ReadGameSaveDataFromDisk(readRequest.filePath))
                        readRequest.result = GameSaveResult.ReadInProgress; 
                    else
                        readRequest.result = GameSaveResult.ErrorReadFailed;               
                }

                if (readRequest.result == GameSaveResult.ReadInProgress)
                {
                    int* dataLength = persistentDataLength[readRequest.gameSaveIndex];
                    *dataLength = 0;
                    bool result = ReadGameSaveLength(readRequest.filePath, dataLength);

                    if ((result == true) && (*dataLength == 0))
                    {
                        // Read is still in progress.
                    }
                    else if ((result == false) || (*dataLength < m_MinimumGameSaveFileSizeInBytes))
                    {
                        readRequest.result = GameSaveResult.ErrorReadFailed;
                    }
                    else if (*dataLength >= m_MinimumGameSaveFileSizeInBytes)
                    {
                        persistentDataBytes[readRequest.gameSaveIndex] = (byte*)UnsafeUtility.Malloc(*dataLength, 4, Allocator.Persistent);
                        UnsafeUtility.MemSet(persistentDataBytes[readRequest.gameSaveIndex], 0x00, *dataLength);
                        bool readCompleted = ReadGameSaveData(readRequest.filePath, persistentDataBytes[readRequest.gameSaveIndex], *dataLength);
                
                        if (readCompleted)
                        {
                            byte* gameSaveVersionBytes = persistentDataBytes[readRequest.gameSaveIndex];
                            if ((gameSaveVersionBytes[0] != 0) || (gameSaveVersionBytes[1] != 0) || (gameSaveVersionBytes[2] != 0) || (gameSaveVersionBytes[3] != 0))
                                readRequest.result = ReadGameSaveDataFromBuffer(persistentDataBytes[readRequest.gameSaveIndex], ref persistentData[readRequest.gameSaveIndex], ref persistentTypes[readRequest.gameSaveIndex], ref typeToPersistentTypeIndexMap[readRequest.gameSaveIndex]);                    
                        }
                        
                        if (readRequest.result != GameSaveResult.Success)
                            readRequest.result = GameSaveResult.ErrorReadFailed;

                        UnsafeUtility.Free(persistentDataBytes[readRequest.gameSaveIndex], Allocator.Persistent);
                        persistentDataBytes[readRequest.gameSaveIndex] = null;
                    }
                }
            }).Run();           

            Entities
                .WithoutBurst()
                .ForEach((
                    Entity e,
                    ref GameSaveReadFromBufferRequest readRequest) =>
            {
                if (readRequest.result == GameSaveResult.NotStarted)
                {
                    if ((readRequest.gameSaveIndex >= 0) && (readRequest.gameSaveIndex < numGameSavesMax))
                    {
                        readRequest.result = GameSaveResult.ReadInProgress;
                        DynamicBuffer<GameSaveBufferByte> buffer = mgr.GetBuffer<GameSaveBufferByte>(e);
                        if (buffer.Length >= m_MinimumGameSaveFileSizeInBytes)
                            readRequest.result = ReadGameSaveDataFromBuffer((byte*)buffer.GetUnsafeReadOnlyPtr(), ref persistentData[readRequest.gameSaveIndex], ref persistentTypes[readRequest.gameSaveIndex], ref typeToPersistentTypeIndexMap[readRequest.gameSaveIndex]);
                        else
                            readRequest.result = GameSaveResult.ErrorReadFailed;
                    }
                    else
                    {
                        readRequest.result = GameSaveResult.ErrorReadFailed;
                    }
                }
            }).Run();    
        }

        protected override void OnDestroy()
        {
            for (int i = 0; i < m_NumGameSavesMax; i++)
            {
                RemoveGameSaveDataFromMemory(i);
                m_PersistentData[i].Dispose();
                m_PersistentTypes[i].Dispose();
                m_TypeToPersistentTypeIndexMap[i].Dispose();
            }

            GameSaveNativeCalls.Shutdown();
        }

        private static unsafe void* GameSaveMalloc(int size)
        {
            m_NativeBytesAllocated += size;
            return Memory.Unmanaged.Allocate((long)size, 4, Allocator.Persistent);
        }

        private static unsafe void GameSaveFree(void* data, int size)
        {
            m_NativeBytesAllocated -= size;
            Memory.Unmanaged.Free(data, Allocator.Persistent);
        }

        private static unsafe void WriteBytes(ref GameSaveData gameSaveData, void* data, int length)
        {
            if (gameSaveData.bufferSize != length)
            {
                if (gameSaveData.bufferSize > IntPtr.Size)
                    GameSaveFree((void*)gameSaveData.buffer, gameSaveData.bufferSize);
    
                if (length > IntPtr.Size)
                    gameSaveData.buffer = (ulong)GameSaveMalloc(length);
    
                gameSaveData.bufferSize = length;
            }

            if (gameSaveData.bufferSize > IntPtr.Size)
            {
                UnsafeUtility.MemCpy((void*)gameSaveData.buffer, data, gameSaveData.bufferSize);
            }
            else
            {
                fixed (void* bufferPtr = &gameSaveData.buffer)
                    UnsafeUtility.MemCpy(bufferPtr, data, gameSaveData.bufferSize);
            }
        }

        private static unsafe void ReadBytes(ref GameSaveData gameSaveData, void* data)
        {
            if (gameSaveData.bufferSize > IntPtr.Size)
            {
                UnsafeUtility.MemCpy(data, (void*)gameSaveData.buffer, gameSaveData.bufferSize);
            }
            else
            {
                fixed (void* bufferPtr = &gameSaveData.buffer)
                    UnsafeUtility.MemCpy(data, bufferPtr, gameSaveData.bufferSize);
            }
        }

        private static unsafe void* GetBuffer(ref GameSaveData gameSaveData)
        {
            if (gameSaveData.bufferSize > IntPtr.Size)
            {
                return (void*)gameSaveData.buffer;
            }
            else
            {
                fixed (void* bufferPtr = &gameSaveData.buffer)
                    return bufferPtr;
            }
        }

        public unsafe GameSaveResult Read<T>(FixedString64 key, ref T value) where T : unmanaged
        {
            int typeIndex = AddTypeToPersistentTypes<T>();

            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                if (gameSaveData.typeIndex == typeIndex)
                {
                    // We found the requested data and the types match up. Copy the stored data into value and return success!
                    ReadBytes(ref gameSaveData, UnsafeUtility.AddressOf<T>(ref value));
                    return GameSaveResult.Success;
                }

                GameSaveTypeInfo gameSaveTypeInfo = m_PersistentTypes[m_GameSaveIndex][gameSaveData.typeIndex];
                GameSaveType gameSaveType = gameSaveTypeInfo.gameSaveType;
                if (IsECSComponent(gameSaveType))
                {
                    // We found the data, but the types don't match up. Go through a careful data migration procedure now.
                    if (ReadAndUpdateECSData<T>(ref key, ref gameSaveData, UnsafeUtility.AddressOf<T>(ref value)))
                        return GameSaveResult.Success;
                }

                return GameSaveResult.ErrorType;       
            }

            return GameSaveResult.ErrorNotFound;
        }

        public unsafe GameSaveResult Read<T>(FixedString64 key, out T value, in T defaultValue) where T : unmanaged
        {
            value = defaultValue;
            return Read<T>(key, ref value);
        }

        public unsafe GameSaveResult Write<T>(FixedString64 key, T value) where T : unmanaged
        {
            GameSaveData gameSaveData;
            bool keyExists = m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData);
            gameSaveData.typeIndex = AddTypeToPersistentTypes<T>();
            gameSaveData.numElements = 1;
            WriteBytes(ref gameSaveData, UnsafeUtility.AddressOf<T>(ref value), UnsafeUtility.SizeOf<T>());

            if (keyExists)
                m_PersistentData[m_GameSaveIndex][key] = gameSaveData;
            else
                m_PersistentData[m_GameSaveIndex].Add(key, gameSaveData);

            return GameSaveResult.Success;
        }

        private unsafe int AddTypeToPersistentTypes<T>() where T : unmanaged
        {
            GameSaveType gameSaveType = GetGameSaveType<T>();
            bool isECSType = IsECSType(gameSaveType);
            bool isStruct = gameSaveType == GameSaveType.Struct;
            ulong stableTypeHash = isECSType ? TypeManager.GetTypeInfo<T>().StableTypeHash : 0;

            // We only store primitive types and ECS types in our list of persistent types. We do store struct fields, but that's it.
            if (isStruct)
                return -1;

            GameSaveTypeKey gameSaveTypeKey;
            gameSaveTypeKey.gameSaveType = gameSaveType;
            gameSaveTypeKey.stableTypeHash = stableTypeHash;

            int typeIndex = -1;
            if (!m_TypeToPersistentTypeIndexMap[m_GameSaveIndex].TryGetValue(gameSaveTypeKey, out typeIndex))
            {
                // Create the new game save type info.
                GameSaveTypeInfo gameSaveTypeInfo;
                gameSaveTypeInfo.gameSaveType = gameSaveType;
                gameSaveTypeInfo.stableTypeHash = stableTypeHash;
                gameSaveTypeInfo.size = UnsafeUtility.SizeOf<T>();
                gameSaveTypeInfo.numFields = 0;
                gameSaveTypeInfo.fields = null;
                
                if (isECSType)
                {
                    // Add in the field info for this new game save type.
                    NativeArray<TypeManager.FieldInfo> typeManagerFieldInfos = TypeManager.GetFieldInfos(typeof(T));
                    if (typeManagerFieldInfos.Length > 0)
                    {
                        gameSaveTypeInfo.numFields = typeManagerFieldInfos.Length;
                        gameSaveTypeInfo.fields = (GameSaveFieldInfo*)GameSaveMalloc(typeManagerFieldInfos.Length*UnsafeUtility.SizeOf<GameSaveFieldInfo>());
                        for (int i = 0; i < gameSaveTypeInfo.numFields; i++)
                        {
                            GameSaveFieldInfo* fieldInfo = (GameSaveFieldInfo*)((byte*)gameSaveTypeInfo.fields + i*UnsafeUtility.SizeOf<GameSaveFieldInfo>());
                            GameSaveType fieldGameSaveType = GetGameSaveFieldType(typeManagerFieldInfos[i].FieldType);
                            fieldInfo->name = typeManagerFieldInfos[i].FieldName;
                            fieldInfo->offset = typeManagerFieldInfos[i].Offset;
                            fieldInfo->gameSaveType = fieldGameSaveType;
                            fieldInfo->typeIndex = (fieldGameSaveType == GameSaveType.Struct) ? AddFieldTypeToPersistentTypes(typeManagerFieldInfos[i].FieldType) : -1;
                        }
                    }
                    typeManagerFieldInfos.Dispose();
                }

                // Add the GameSaveTypeInfo to our list of types.
                typeIndex = m_PersistentTypes[m_GameSaveIndex].Length;
                m_PersistentTypes[m_GameSaveIndex].Add(gameSaveTypeInfo);

                // Add the mapping from type key to type index.
                m_TypeToPersistentTypeIndexMap[m_GameSaveIndex].TryAdd(gameSaveTypeKey, typeIndex);
            }
            
            return typeIndex;
        }

        private unsafe int AddFieldTypeToPersistentTypes(Type fieldType)
        {
            GameSaveType gameSaveType = GetGameSaveFieldType(fieldType);
            bool isStruct = (gameSaveType == GameSaveType.Struct);
            
            if (!isStruct)
                return -1;
            
            // Create the new game save type info. For struct fields, we don't actually care about the size since if we're using this type info, we're
            // just tracing down via matching names until we get to a basic type (or ECS fixed string type).
            GameSaveTypeInfo gameSaveTypeInfo;
            gameSaveTypeInfo.gameSaveType = gameSaveType;
            gameSaveTypeInfo.stableTypeHash = 0;
            gameSaveTypeInfo.size = 0;
            gameSaveTypeInfo.numFields = 0;
            gameSaveTypeInfo.fields = null;
        
            // Add in the field info for this new game save type.
            NativeArray<TypeManager.FieldInfo> typeManagerFieldInfos = TypeManager.GetFieldInfos(fieldType);
            if (typeManagerFieldInfos.Length > 0)
            {
                gameSaveTypeInfo.numFields = typeManagerFieldInfos.Length;
                gameSaveTypeInfo.fields = (GameSaveFieldInfo*)GameSaveMalloc(typeManagerFieldInfos.Length*UnsafeUtility.SizeOf<GameSaveFieldInfo>());
                for (int i = 0; i < gameSaveTypeInfo.numFields; i++)
                {
                    GameSaveFieldInfo* fieldInfo = (GameSaveFieldInfo*)((byte*)gameSaveTypeInfo.fields + i*UnsafeUtility.SizeOf<GameSaveFieldInfo>());
                    GameSaveType fieldGameSaveType = GetGameSaveFieldType(typeManagerFieldInfos[i].FieldType);
                    fieldInfo->name = typeManagerFieldInfos[i].FieldName;
                    fieldInfo->offset = typeManagerFieldInfos[i].Offset;
                    fieldInfo->gameSaveType = fieldGameSaveType;
                    fieldInfo->typeIndex = (fieldGameSaveType == GameSaveType.Struct) ? AddFieldTypeToPersistentTypes(typeManagerFieldInfos[i].FieldType) : -1;
                }
            }
            typeManagerFieldInfos.Dispose();

            // Add the GameSaveTypeInfo to our list of types.
            int typeIndex = m_PersistentTypes[m_GameSaveIndex].Length;
            m_PersistentTypes[m_GameSaveIndex].Add(gameSaveTypeInfo);
            return typeIndex;
        }

        private unsafe bool ReadAndUpdateECSData<T>(ref FixedString64 key, ref GameSaveData gameSaveData, void* value) where T : unmanaged
        {
            GameSaveTypeInfo gameSaveTypeInfo = m_PersistentTypes[m_GameSaveIndex][gameSaveData.typeIndex];
            GameSaveType gameSaveType = gameSaveTypeInfo.gameSaveType;
            if (!IsECSType(gameSaveType))
                return false;

            int oldTypeIndex = gameSaveData.typeIndex;
            GameSaveTypeInfo oldTypeInfo = m_PersistentTypes[m_GameSaveIndex][oldTypeIndex];

            // Get new type info.
            int newTypeIndex = AddTypeToPersistentTypes<T>();
            GameSaveTypeInfo newTypeInfo = m_PersistentTypes[m_GameSaveIndex][newTypeIndex];

            void* oldGameSaveData = GameSaveMalloc(gameSaveData.numElements * oldTypeInfo.size);
            ReadBytes(ref gameSaveData, oldGameSaveData);

            void* newGameSaveData = value;
            
            for (int iElement = 0; iElement < gameSaveData.numElements; iElement++)
            {
                for (int iNewField = 0; iNewField < newTypeInfo.numFields; iNewField++)
                {
                    GameSaveFieldInfo* newFieldInfo = (GameSaveFieldInfo*)((byte*)newTypeInfo.fields + iNewField*UnsafeUtility.SizeOf<GameSaveFieldInfo>());

                    bool fieldUpdated = false;
                    for (int iOldField = 0; iOldField < oldTypeInfo.numFields; iOldField++)
                    {
                        GameSaveFieldInfo* oldFieldInfo = (GameSaveFieldInfo*)((byte*)oldTypeInfo.fields + iOldField*UnsafeUtility.SizeOf<GameSaveFieldInfo>());
                        bool sameFieldName = (newFieldInfo->name == oldFieldInfo->name);
                        bool sameFieldType = (newFieldInfo->gameSaveType == oldFieldInfo->gameSaveType);
                        
                        if (sameFieldName && sameFieldType)
                        {
                            byte* newGameSaveDataPtr = (byte*)newGameSaveData + iElement*newTypeInfo.size + newFieldInfo->offset;
                            byte* oldGameSaveDataPtr = (byte*)oldGameSaveData + iElement*oldTypeInfo.size + oldFieldInfo->offset;

                            if (newFieldInfo->gameSaveType == GameSaveType.Struct)
                                UpdateStructData(ref key, ref newFieldInfo->name, newGameSaveDataPtr, oldGameSaveDataPtr, newFieldInfo->typeIndex, oldFieldInfo->typeIndex);
                            else
                                UnsafeUtility.MemCpy(newGameSaveDataPtr, oldGameSaveDataPtr, GetGameSaveTypeSize(newFieldInfo->gameSaveType));

                            fieldUpdated = true;
                            break;
                        }
                    }

                    if (!fieldUpdated)
                        Debug.LogWarning("When reading GameSave data " + key.Value + ", field " + newFieldInfo->name.Value + " was not found.");
                }
            }

            GameSaveFree(oldGameSaveData, gameSaveData.numElements * oldTypeInfo.size);

            return true;
        }

        private unsafe void UpdateStructData(ref FixedString64 key, ref FixedString64 structName, byte* newData, byte* oldData, int newTypeIndex, int oldTypeIndex)
        {
            GameSaveTypeInfo oldTypeInfo = m_PersistentTypes[m_GameSaveIndex][oldTypeIndex];
            GameSaveTypeInfo newTypeInfo = m_PersistentTypes[m_GameSaveIndex][newTypeIndex];
            
            for (int iNewField = 0; iNewField < newTypeInfo.numFields; iNewField++)
            {
                GameSaveFieldInfo* newFieldInfo = (GameSaveFieldInfo*)((byte*)newTypeInfo.fields + iNewField*UnsafeUtility.SizeOf<GameSaveFieldInfo>());

                bool fieldUpdated = false;
                for (int iOldField = 0; iOldField < oldTypeInfo.numFields; iOldField++)
                {
                    GameSaveFieldInfo* oldFieldInfo = (GameSaveFieldInfo*)((byte*)oldTypeInfo.fields + iOldField*UnsafeUtility.SizeOf<GameSaveFieldInfo>());
                    bool sameFieldName = (newFieldInfo->name == oldFieldInfo->name);
                    bool sameFieldType = (newFieldInfo->gameSaveType == oldFieldInfo->gameSaveType);
                    
                    if (sameFieldName && sameFieldType)
                    {
                        byte* newGameSaveDataPtr = (byte*)newData + newFieldInfo->offset;
                        byte* oldGameSaveDataPtr = (byte*)oldData + oldFieldInfo->offset;

                        if (newFieldInfo->gameSaveType == GameSaveType.Struct)
                            UpdateStructData(ref key, ref newFieldInfo->name, newGameSaveDataPtr, oldGameSaveDataPtr, newFieldInfo->typeIndex, oldFieldInfo->typeIndex);
                        else
                            UnsafeUtility.MemCpy(newGameSaveDataPtr, oldGameSaveDataPtr, GetGameSaveTypeSize(newFieldInfo->gameSaveType));

                        fieldUpdated = true;
                        break;
                    }
                }

                if (!fieldUpdated)
                    Debug.LogWarning("When reading GameSave data " + key.Value + ", in struct field " + structName.Value + ", field " + newFieldInfo->name.Value + " was not found.");
            }
        }

        public unsafe GameSaveResult ReadDynamicBuffer<T>(FixedString64 key, ref DynamicBuffer<T> value) where T : unmanaged
        {
            int typeIndex = AddTypeToPersistentTypes<T>();

            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                value.Length = gameSaveData.numElements;

                if (gameSaveData.typeIndex == typeIndex)
                {                    
                    ReadBytes(ref gameSaveData, value.GetUnsafePtr());
                    return GameSaveResult.Success;
                }

                GameSaveTypeInfo gameSaveTypeInfo = m_PersistentTypes[m_GameSaveIndex][gameSaveData.typeIndex];
                if (gameSaveTypeInfo.gameSaveType == GameSaveType.BufferData)
                {
                    // We found the data, but the types don't match up. Go through a careful data migration procedure now.
                    if (ReadAndUpdateECSData<T>(ref key, ref gameSaveData, value.GetUnsafePtr()))
                        return GameSaveResult.Success;
                }

                return GameSaveResult.ErrorType;       
            }

            return GameSaveResult.ErrorNotFound;            
        }

        public unsafe GameSaveResult WriteDynamicBuffer<T>(FixedString64 key, ref DynamicBuffer<T> value) where T : unmanaged
        {   
            GameSaveData gameSaveData;
            bool keyExists = m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData);
            gameSaveData.typeIndex = AddTypeToPersistentTypes<T>();
            gameSaveData.numElements = value.Length;
            WriteBytes(ref gameSaveData, value.GetUnsafeReadOnlyPtr(), value.Length * UnsafeUtility.SizeOf<T>());

            if (keyExists)
                m_PersistentData[m_GameSaveIndex][key] = gameSaveData;
            else
                m_PersistentData[m_GameSaveIndex].TryAdd(key, gameSaveData);

            return GameSaveResult.Success;
        }

        public unsafe GameSaveResult ReadNativeArray<T>(FixedString64 key, ref NativeArray<T> value, Allocator allocator = Allocator.Persistent) where T : unmanaged
        {
            int typeIndex = AddTypeToPersistentTypes<T>();

            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                if (value.Length != gameSaveData.numElements)
                {
                    value.Dispose();
                    value = new NativeArray<T>(gameSaveData.numElements, allocator);
                }

                if (gameSaveData.typeIndex == typeIndex)
                {
                    ReadBytes(ref gameSaveData, value.GetUnsafePtr());
                    return GameSaveResult.Success;
                }

                GameSaveTypeInfo gameSaveTypeInfo = m_PersistentTypes[m_GameSaveIndex][gameSaveData.typeIndex];
                if (IsECSType(gameSaveTypeInfo.gameSaveType))
                {
                    // We found the data, but the types don't match up. Go through a careful data migration procedure now.
                    if (ReadAndUpdateECSData<T>(ref key, ref gameSaveData, value.GetUnsafePtr()))
                        return GameSaveResult.Success;
                }

                return GameSaveResult.ErrorType;       
            }

            return GameSaveResult.ErrorNotFound;  
        }

        public unsafe GameSaveResult WriteNativeArray<T>(FixedString64 key, ref NativeArray<T> value) where T : unmanaged
        {
            GameSaveData gameSaveData;
            bool keyExists = m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData);
            gameSaveData.typeIndex = AddTypeToPersistentTypes<T>();
            gameSaveData.numElements = value.Length;
            WriteBytes(ref gameSaveData, value.GetUnsafeReadOnlyPtr(), value.Length * UnsafeUtility.SizeOf<T>());

            if (keyExists)
                m_PersistentData[m_GameSaveIndex][key] = gameSaveData;
            else
                m_PersistentData[m_GameSaveIndex].TryAdd(key, gameSaveData);

            return GameSaveResult.Success;
        }

        public unsafe GameSaveResult ReadBytes(FixedString64 key, void* data, int length)
        {
            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                if (gameSaveData.bufferSize != length)
                    return GameSaveResult.ErrorDataSize;

                ReadBytes(ref gameSaveData, data);
                return GameSaveResult.Success;
            }

            return GameSaveResult.ErrorNotFound;
        }

        public unsafe void WriteBytes(FixedString64 key, void* data, int length)
        {
            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                gameSaveData.typeIndex = -1;
                gameSaveData.numElements = 1;
                WriteBytes(ref gameSaveData, data, length);
                m_PersistentData[m_GameSaveIndex][key] = gameSaveData;
            }
            else
            {
                gameSaveData = new GameSaveData();
                gameSaveData.typeIndex = -1;
                gameSaveData.numElements = 1;          
                WriteBytes(ref gameSaveData, data, length);
                m_PersistentData[m_GameSaveIndex].TryAdd(key, gameSaveData);
            }
        }

        public unsafe void RemoveData(FixedString64 key)
        {
            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                if (gameSaveData.bufferSize > IntPtr.Size)
                    GameSaveFree((void*)gameSaveData.buffer, gameSaveData.bufferSize);
                gameSaveData.bufferSize = 0;
                m_PersistentData[m_GameSaveIndex].Remove(key);
            }            
        }

        // Find all keys/keys of Type T that are currently stored in the game save system.
        public NativeList<FixedString64> GetKeys<T>() where T : unmanaged
        {
            NativeList<FixedString64> keysToReturn = new NativeList<FixedString64>(Allocator.Persistent);
            GameSaveType gameSaveType = GetGameSaveType<T>();
            ulong stableTypeHash = IsECSType(gameSaveType) ? TypeManager.GetTypeInfo<T>().StableTypeHash : 0;
           
            NativeArray<FixedString64> keyArray = m_PersistentData[m_GameSaveIndex].GetKeyArray(Allocator.Temp);
            NativeArray<GameSaveData> valueArray = m_PersistentData[m_GameSaveIndex].GetValueArray(Allocator.Temp);
            for (int i = 0; i < valueArray.Length; i++)
            {
                GameSaveData gameSaveData = valueArray[i];
                GameSaveTypeInfo gameSaveTypeInfo = m_PersistentTypes[m_GameSaveIndex][gameSaveData.typeIndex];
                if ((gameSaveTypeInfo.gameSaveType == gameSaveType) && (gameSaveTypeInfo.stableTypeHash == stableTypeHash))
                    keysToReturn.Add(keyArray[i]);
            }

            return keysToReturn;
        }

        public unsafe int GetSize(FixedString64 key)
        {
            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
                return gameSaveData.bufferSize;
        
            return 0;    
        }      

        public unsafe int GetNumElements(FixedString64 key)
        {
            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
                return gameSaveData.numElements;
        
            return 0;    
        }

        public unsafe GameSaveType GetType(FixedString64 key)
        {
            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                int typeIndex = gameSaveData.typeIndex;
                if ((typeIndex >= 0) && (typeIndex < m_PersistentTypes[m_GameSaveIndex].Length))
                    return m_PersistentTypes[m_GameSaveIndex][typeIndex].gameSaveType;
            }
        
            return GameSaveType.Unknown;    
        }  

        public unsafe void RemoveGameSaveDataFromMemory(int gameSaveIndex = 0)
        {
            if ((gameSaveIndex >= 0) && (gameSaveIndex < m_NumGameSavesMax))
                RemoveGameSaveDataFromMemory(ref m_PersistentData[gameSaveIndex]);

            if ((gameSaveIndex >= 0) && (gameSaveIndex < m_NumGameSavesMax))
                RemoveGameSaveTypesFromMemory(ref m_PersistentTypes[gameSaveIndex], ref m_TypeToPersistentTypeIndexMap[gameSaveIndex]);
        }

        private static unsafe void RemoveGameSaveDataFromMemory(ref NativeHashMap<FixedString64, GameSaveData> persistentData)
        {
            NativeArray<GameSaveData> values = persistentData.GetValueArray(Allocator.Temp);

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].bufferSize > IntPtr.Size)
                    GameSaveFree((void*)values[i].buffer, values[i].bufferSize);
            }

            persistentData.Clear();
        }

        private static unsafe void RemoveGameSaveTypesFromMemory(ref NativeList<GameSaveTypeInfo> persistentTypes, ref NativeHashMap<GameSaveTypeKey, int> typeToPersistentTypeIndexMap)
        {
            for (int i = 0; i < persistentTypes.Length; i++)
            {
                GameSaveTypeInfo gameSaveTypeInfo = persistentTypes[i];
                if (gameSaveTypeInfo.numFields > 0)
                    GameSaveFree(gameSaveTypeInfo.fields, gameSaveTypeInfo.numFields*UnsafeUtility.SizeOf<GameSaveFieldInfo>());
            }

            persistentTypes.Clear();
            typeToPersistentTypeIndexMap.Clear();
        }

        private static unsafe bool ReadGameSaveDataFromDisk(FixedString128 gameSaveFilePath)
        {
            if (gameSaveFilePath.Length == 0)
                return false;

            return GameSaveNativeCalls.ReadFromDisk(gameSaveFilePath);
        }

        private static unsafe bool ReadGameSaveLength(FixedString128 gameSaveFilePath, int* persistentDataLength)
        {
            if (gameSaveFilePath.Length == 0)
                return false;

            return GameSaveNativeCalls.GetLength(gameSaveFilePath, persistentDataLength);
        }

        private static unsafe bool ReadGameSaveData(FixedString128 gameSaveFilePath, byte* persistentDataBytes, int persistentDataBytesLength)
        {
            if (gameSaveFilePath.Length == 0)
                return false;
                
            return GameSaveNativeCalls.PullCompletedReadBuffer(gameSaveFilePath, persistentDataBytes, persistentDataBytesLength);
        }

        private static unsafe GameSaveResult ReadGameSaveDataFromBuffer(byte* buffer, ref NativeHashMap<FixedString64, GameSaveData> persistentData, ref NativeList<GameSaveTypeInfo> persistentTypes, ref NativeHashMap<GameSaveTypeKey, int> typeToPersistentTypeIndexMap)
        {
            RemoveGameSaveDataFromMemory(ref persistentData);

            MemoryBinaryReader reader = new MemoryBinaryReader(buffer);            
            ReadDataHashMap(reader, ref persistentData);
            ReadTypesList(reader, ref persistentTypes, ref typeToPersistentTypeIndexMap);
            reader.Dispose();

            return GameSaveResult.Success;
        }

        private static unsafe bool WriteGameSaveDataToDisk(FixedString128 gameSaveFilePath, ref NativeHashMap<FixedString64, GameSaveData> persistentData, ref NativeList<GameSaveTypeInfo> persistentTypes)
        {
            bool success = false;

            MemoryBinaryWriter writer = new MemoryBinaryWriter();
            WriteDataHashMap(writer, persistentData);
            WriteTypesList(writer, persistentTypes);
            if (writer.Length > 0)
                success = GameSaveNativeCalls.WriteToDisk(gameSaveFilePath, writer);

            writer.Dispose();
            return success;
        }

        private static unsafe bool WriteGameSaveDataToBuffer(ref DynamicBuffer<GameSaveBufferByte> buffer, ref NativeHashMap<FixedString64, GameSaveData> persistentData, ref NativeList<GameSaveTypeInfo> persistentTypes)
        {
            MemoryBinaryWriter writer = new MemoryBinaryWriter();
            WriteDataHashMap(writer, persistentData);
            WriteTypesList(writer, persistentTypes);

            buffer.Length = writer.Length;
            UnsafeUtility.MemCpy(buffer.GetUnsafePtr(), writer.Data, writer.Length);
            writer.Dispose();
            return true;
        }

        private static unsafe void ReadDataHashMap(MemoryBinaryReader reader, ref NativeHashMap<FixedString64, GameSaveData> hashMap)
        {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                FixedString64 key;
                reader.ReadBytes(&key, sizeof(FixedString64));

                GameSaveData gameSaveData = new GameSaveData();
                gameSaveData.typeIndex = reader.ReadInt();
                gameSaveData.numElements = reader.ReadInt();
                gameSaveData.bufferSize = reader.ReadInt();
                if (gameSaveData.bufferSize > IntPtr.Size)
                {
                    gameSaveData.buffer = (ulong)GameSaveMalloc(gameSaveData.bufferSize);
                    reader.ReadBytes((void*)gameSaveData.buffer, gameSaveData.bufferSize);
                }
                else
                {
                    reader.ReadBytes(&gameSaveData.buffer, gameSaveData.bufferSize);
                }

                if (!hashMap.TryAdd(key, gameSaveData))
                {
                    if (gameSaveData.bufferSize > IntPtr.Size)
                        GameSaveFree((void*)gameSaveData.buffer, gameSaveData.bufferSize);
                }
            }
        }

        private static unsafe void WriteDataHashMap(MemoryBinaryWriter writer, NativeHashMap<FixedString64, GameSaveData> hashMap)
        {
            int count = hashMap.Count();
            writer.Write(count);

            if (count > 0)
            {
                NativeArray<FixedString64> keys = hashMap.GetKeyArray(Allocator.Temp);
                NativeArray<GameSaveData> values = hashMap.GetValueArray(Allocator.Temp);
                for (int i = 0; i < count; i++)
                {
                    FixedString64 key = keys[i];
                    GameSaveData gameSaveData = values[i];
                    
                    writer.WriteBytes(&key, sizeof(FixedString64));
                    writer.Write(gameSaveData.typeIndex);
                    writer.Write(gameSaveData.numElements);
                    writer.Write(gameSaveData.bufferSize);

                    if (gameSaveData.bufferSize > IntPtr.Size)
                        writer.WriteBytes((void*)gameSaveData.buffer, gameSaveData.bufferSize);
                    else
                        writer.WriteBytes(&gameSaveData.buffer, gameSaveData.bufferSize);
                }
            }
        }

        private static unsafe void ReadTypesList(MemoryBinaryReader reader, ref NativeList<GameSaveTypeInfo> typesList, ref NativeHashMap<GameSaveTypeKey, int> typeToPersistentTypeIndexMap)
        {
            int length = reader.ReadInt();
            for (int i = 0; i < length; i++)
            {
                GameSaveTypeInfo gameSaveTypeInfo;
                gameSaveTypeInfo.gameSaveType = (GameSaveType)reader.ReadInt();
                gameSaveTypeInfo.stableTypeHash = 0;
                if (IsECSType(gameSaveTypeInfo.gameSaveType))
                    gameSaveTypeInfo.stableTypeHash = reader.ReadULong();
                gameSaveTypeInfo.size = reader.ReadInt();
                gameSaveTypeInfo.numFields = reader.ReadInt();
                gameSaveTypeInfo.fields = (GameSaveFieldInfo*)GameSaveMalloc(gameSaveTypeInfo.numFields*UnsafeUtility.SizeOf<GameSaveFieldInfo>());
                       
                for (int iField = 0; iField < gameSaveTypeInfo.numFields; iField++)
                {
                    GameSaveFieldInfo* fieldInfo = (GameSaveFieldInfo*)((byte*)gameSaveTypeInfo.fields + iField*UnsafeUtility.SizeOf<GameSaveFieldInfo>());
                    reader.ReadBytes(&fieldInfo->name, sizeof(FixedString64));
                    fieldInfo->offset = reader.ReadInt();
                    fieldInfo->gameSaveType = (GameSaveType)reader.ReadInt();        
                    fieldInfo->typeIndex = reader.ReadInt(); 
                }

                typesList.Add(gameSaveTypeInfo);

                GameSaveTypeKey gameSaveTypeKey;
                gameSaveTypeKey.gameSaveType = gameSaveTypeInfo.gameSaveType;
                gameSaveTypeKey.stableTypeHash = gameSaveTypeInfo.stableTypeHash;
                typeToPersistentTypeIndexMap.TryAdd(gameSaveTypeKey, i);
            }
        }

        private static unsafe void WriteTypesList(MemoryBinaryWriter writer, NativeList<GameSaveTypeInfo> typeList)
        {
            int length = typeList.Length;
            writer.Write(length);

            for (int i = 0; i < length; i++)
            {
                GameSaveTypeInfo gameSaveTypeInfo = typeList[i];              
                writer.Write((int)gameSaveTypeInfo.gameSaveType);
                if (IsECSType(gameSaveTypeInfo.gameSaveType))
                    writer.Write(gameSaveTypeInfo.stableTypeHash);
                writer.Write(gameSaveTypeInfo.size);
                writer.Write(gameSaveTypeInfo.numFields);
                for (int iField = 0; iField < gameSaveTypeInfo.numFields; iField++)
                {
                    GameSaveFieldInfo* fieldInfo = (GameSaveFieldInfo*)((byte*)gameSaveTypeInfo.fields + iField*UnsafeUtility.SizeOf<GameSaveFieldInfo>());
                    writer.WriteBytes(&fieldInfo->name, sizeof(FixedString64));
                    writer.Write(fieldInfo->offset);
                    writer.Write((int)fieldInfo->gameSaveType);
                    writer.Write(fieldInfo->typeIndex);
                }
            }
        }

        private static bool IsECSType<T>()
        {
            bool isComponent = typeof(IComponentData).IsAssignableFrom(typeof(T));
            bool isSharedComponent = typeof(ISharedComponentData).IsAssignableFrom(typeof(T));
            bool isBufferData = typeof(IBufferElementData).IsAssignableFrom(typeof(T));

            return isComponent || isSharedComponent || isBufferData;
        }

        private static bool IsECSType(GameSaveType gameSaveType)
        {
            return ((gameSaveType == GameSaveType.ComponentData) || 
                (gameSaveType == GameSaveType.SharedComponentData) ||
                (gameSaveType == GameSaveType.BufferData));
        }

        private static bool IsECSComponent(GameSaveType gameSaveType)
        {
            return ((gameSaveType == GameSaveType.ComponentData) || 
                (gameSaveType == GameSaveType.SharedComponentData));
        }

        private static GameSaveType GetGameSaveType<T>() where T : unmanaged
         {
            if (typeof(T).IsEnum)
            {
                // These checks make sure we get the correct size for the enum, but don't differentiate between signed 
                // and unsigned types because I don't think there is a way for us to learn this information. It doesn't 
                // affect the data returned, but the slightly incorrect type will be returned from GameSaveSystem::GetType.
                if (UnsafeUtility.SizeOf<T>() == sizeof(byte))
                    return GameSaveType.Byte;
                else if (UnsafeUtility.SizeOf<T>() == sizeof(short))
                    return GameSaveType.Short;
                else if (UnsafeUtility.SizeOf<T>() == sizeof(int))
                    return GameSaveType.Int;
                else if (UnsafeUtility.SizeOf<T>() == sizeof(long))
                    return GameSaveType.Long;
            }
            else if (typeof(T).IsPrimitive)
                return GameSaveTypeLookup<T>.gameSaveType;
            else if (typeof(T) == typeof(FixedString64))
                return GameSaveType.FixedString64;
            else if (typeof(T) == typeof(FixedString128))
                return GameSaveType.FixedString128;
            else if (typeof(IComponentData).IsAssignableFrom(typeof(T)))
                return GameSaveType.ComponentData;
            else if (typeof(ISharedComponentData).IsAssignableFrom(typeof(T)))
                return GameSaveType.SharedComponentData;
            else if (typeof(IBufferElementData).IsAssignableFrom(typeof(T)))
                return GameSaveType.BufferData;
            else if (typeof(T).IsValueType)
                return GameSaveType.Struct;

            return GameSaveType.Unknown;
         }
        private static GameSaveType GetGameSaveFieldType(Type t)
        {
            // Fields that are enums are actually stored as structs that have one field of the enum's underlying type,
            // so we don't have to handle enums here.

            if (t == typeof(bool))
                return GameSaveType.Bool;
            else if (t == typeof(byte))
                return GameSaveType.Byte;
            else if (t == typeof(sbyte)) 
                return GameSaveType.Sbyte;
            else if (t == typeof(char)) 
                return GameSaveType.Char;
            else if (t == typeof(short)) 
                return GameSaveType.Short;
            else if (t == typeof(ushort)) 
                return GameSaveType.Ushort;
            else if (t == typeof(int)) 
                return GameSaveType.Int;
            else if (t == typeof(uint)) 
                return GameSaveType.Uint;
            else if (t == typeof(float)) 
                return GameSaveType.Float;
            else if (t == typeof(double)) 
                return GameSaveType.Double;
            else if (t == typeof(long)) 
                return GameSaveType.Long;
            else if (t == typeof(ulong)) 
                return GameSaveType.Ulong;
            else if (t == typeof(FixedString64)) 
                return GameSaveType.FixedString64;
            else if (t == typeof(FixedString128)) 
                return GameSaveType.FixedString128;
            else if (t.IsValueType && !t.IsPrimitive)
                return GameSaveType.Struct;
               
            return GameSaveType.Unknown;
        }

        private int GetGameSaveTypeSize(GameSaveType gameSaveType)
        {
            return m_GameSaveTypeSizes[(int)gameSaveType];
        }

        private void InitGameSaveTypeSizes()
        {
            m_GameSaveTypeSizes = new int[(int)GameSaveType.Count];
            m_GameSaveTypeSizes[(int)GameSaveType.Bool] = sizeof(bool);
            m_GameSaveTypeSizes[(int)GameSaveType.Byte] = sizeof(byte);
            m_GameSaveTypeSizes[(int)GameSaveType.Sbyte] = sizeof(sbyte);
            m_GameSaveTypeSizes[(int)GameSaveType.Char] = sizeof(char);
            m_GameSaveTypeSizes[(int)GameSaveType.Short] = sizeof(short);
            m_GameSaveTypeSizes[(int)GameSaveType.Ushort] = sizeof(ushort);
            m_GameSaveTypeSizes[(int)GameSaveType.Int] = sizeof(int);
            m_GameSaveTypeSizes[(int)GameSaveType.Uint] = sizeof(uint);
            m_GameSaveTypeSizes[(int)GameSaveType.Float] = sizeof(float);
            m_GameSaveTypeSizes[(int)GameSaveType.Double] = sizeof(double);
            m_GameSaveTypeSizes[(int)GameSaveType.Long] = sizeof(long);
            m_GameSaveTypeSizes[(int)GameSaveType.Ulong] = sizeof(ulong);
            m_GameSaveTypeSizes[(int)GameSaveType.FixedString64] = UnsafeUtility.SizeOf<FixedString64>();
            m_GameSaveTypeSizes[(int)GameSaveType.FixedString128] = UnsafeUtility.SizeOf<FixedString128>();
        }

        private struct GameSaveTypeLookup<T>
        {
            public static GameSaveType gameSaveType;
        }
        
        private static void InitGameSaveTypeLookup()
        {
            GameSaveTypeLookup<bool>.gameSaveType = GameSaveType.Bool;
            GameSaveTypeLookup<byte>.gameSaveType = GameSaveType.Byte;
            GameSaveTypeLookup<sbyte>.gameSaveType = GameSaveType.Sbyte;
            GameSaveTypeLookup<char>.gameSaveType = GameSaveType.Char;
            GameSaveTypeLookup<short>.gameSaveType = GameSaveType.Short;
            GameSaveTypeLookup<ushort>.gameSaveType = GameSaveType.Ushort;
            GameSaveTypeLookup<int>.gameSaveType = GameSaveType.Int;
            GameSaveTypeLookup<uint>.gameSaveType = GameSaveType.Uint;
            GameSaveTypeLookup<float>.gameSaveType = GameSaveType.Float;
            GameSaveTypeLookup<double>.gameSaveType = GameSaveType.Double;
            GameSaveTypeLookup<long>.gameSaveType = GameSaveType.Long;
            GameSaveTypeLookup<ulong>.gameSaveType = GameSaveType.Ulong;
            GameSaveTypeLookup<FixedString64>.gameSaveType = GameSaveType.FixedString64;
            GameSaveTypeLookup<FixedString128>.gameSaveType = GameSaveType.FixedString128;
        }
    }
}
