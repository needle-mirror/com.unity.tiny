using System;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Platforms;
using Unity.Tiny;
using Unity.Baselib.LowLevel;
using static Unity.Baselib.LowLevel.Binding;

namespace Unity.Tiny.GameSave
{
    public enum GameSaveResult
    {
        NotStarted,
        InProgress,
        Success,
        ErrorNotFound,
        ErrorType,
        ErrorDataSize,
        ErrorReadFailed,
        ErrorWriteFailed
    }

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
        BufferData,
        ByteArray
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
        public GameSaveType gameSaveType;
        public ulong stableTypeHash;
        public int numElements;
        public ulong buffer;
        public int bufferSize;
    }

    public class GameSaveSystem : SystemBase
    {
        private NativeHashMap<FixedString64, GameSaveData>[] m_PersistentData;

        private int m_NumGameSavesMax;
        private int m_GameSaveIndex;
        private int m_UnityGameSaveCodeVersion = 1;
        private static int m_NativeBytesAllocated = 0;

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

        public int NativeBytesAllocated => m_NativeBytesAllocated;

        protected override void OnCreate()
        {
            m_NumGameSavesMax = 16;
            m_GameSaveIndex = 0;

            m_PersistentData = new NativeHashMap<FixedString64, GameSaveData>[m_NumGameSavesMax];
            for (int i = 0; i < m_NumGameSavesMax; i++)
                m_PersistentData[i] = new NativeHashMap<FixedString64, GameSaveData>(64, Allocator.Persistent);
        }

        protected override void OnUpdate()
        {
            var mgr = EntityManager;
            NativeHashMap<FixedString64, GameSaveData>[] persistentData = m_PersistentData;
            int unityGameSaveCodeVersion = m_UnityGameSaveCodeVersion;
            int numGameSavesMax = m_NumGameSavesMax;

            Entities
                .WithoutBurst()
                .ForEach((
                    ref GameSaveWriteToPersistentStorageRequest writeRequest) =>
            {
                if (writeRequest.result == GameSaveResult.NotStarted)
                {
                    if ((writeRequest.gameSaveIndex >= 0) && (writeRequest.gameSaveIndex < numGameSavesMax))
                    {
                        writeRequest.result = GameSaveResult.InProgress;
                        bool success = WriteGameSaveDataToDisk(writeRequest.filePath, unityGameSaveCodeVersion, ref persistentData[writeRequest.gameSaveIndex]);
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
                        writeRequest.result = GameSaveResult.InProgress;
                        DynamicBuffer<GameSaveBufferByte> buffer = mgr.AddBuffer<GameSaveBufferByte>(e);
                        bool success = WriteGameSaveDataToBuffer(ref buffer, unityGameSaveCodeVersion, ref persistentData[writeRequest.gameSaveIndex]);
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
                    ref GameSaveReadFromPersistentStorageRequest readRequest) =>
            {
                if (readRequest.result == GameSaveResult.NotStarted)
                {
                    if ((readRequest.gameSaveIndex >= 0) && (readRequest.gameSaveIndex < numGameSavesMax))
                    {
                        readRequest.result = GameSaveResult.InProgress;
                        readRequest.result = ReadGameSaveDataFromDisk(readRequest.filePath, unityGameSaveCodeVersion, ref persistentData[readRequest.gameSaveIndex]);
                    }
                    else
                    {
                        readRequest.result = GameSaveResult.ErrorReadFailed;
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
                        readRequest.result = GameSaveResult.InProgress;
                        DynamicBuffer<GameSaveBufferByte> buffer = mgr.GetBuffer<GameSaveBufferByte>(e);
                        readRequest.result = ReadGameSaveDataFromBuffer(ref buffer, unityGameSaveCodeVersion, ref persistentData[readRequest.gameSaveIndex]);
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
                RemoveGameSaveDataFromMemory(ref m_PersistentData[i]);
                m_PersistentData[i].Dispose();
            }
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

        private unsafe void WriteBytes(ref GameSaveData gameSaveData, void* data, int length)
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

        private unsafe void ReadBytes(ref GameSaveData gameSaveData, void* data)
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

        private unsafe GameSaveResult ErrorCheckGameSaveData<T>(ref GameSaveData gameSaveData, GameSaveType gameSaveType, ulong stableTypeHash) where T : unmanaged
        {
            if (gameSaveData.gameSaveType != gameSaveType)
                return GameSaveResult.ErrorType;

            if (gameSaveData.stableTypeHash != stableTypeHash)
                return GameSaveResult.ErrorType;

            int elementSize = gameSaveData.bufferSize / gameSaveData.numElements;
            if (elementSize != UnsafeUtility.SizeOf<T>())
                return GameSaveResult.ErrorDataSize;

            return GameSaveResult.Success;
        }

        public unsafe GameSaveResult ReadBool(FixedString64 key, ref bool value)
        {
            return ReadInternal<bool>(key, ref value, GameSaveType.Bool);
        }

        public unsafe GameSaveResult ReadBool(FixedString64 key, out bool value, bool defaultValue)
        {
            value = defaultValue;
            return ReadInternal<bool>(key, ref value, GameSaveType.Bool);
        }

        public unsafe GameSaveResult ReadByte(FixedString64 key, ref byte value)
        {
            return ReadInternal<byte>(key, ref value, GameSaveType.Byte);
        }


        public unsafe GameSaveResult ReadByte(FixedString64 key, out byte value, byte defaultValue)
        {
            value = defaultValue;
            return ReadInternal<byte>(key, ref value, GameSaveType.Byte);
        }

        public unsafe GameSaveResult ReadSbyte(FixedString64 key, ref sbyte value)
        {
            return ReadInternal<sbyte>(key, ref value, GameSaveType.Sbyte);
        }

        public unsafe GameSaveResult ReadSbyte(FixedString64 key, out sbyte value, sbyte defaultValue)
        {
            value = defaultValue;
            return ReadInternal<sbyte>(key, ref value, GameSaveType.Sbyte);
        }

        public unsafe GameSaveResult ReadChar(FixedString64 key, ref char value)
        {
            return ReadInternal<char>(key, ref value, GameSaveType.Char);
        }

        public unsafe GameSaveResult ReadChar(FixedString64 key, out char value, char defaultValue)
        {
            value = defaultValue;
            return ReadInternal<char>(key, ref value, GameSaveType.Char);
        }

        public unsafe GameSaveResult ReadShort(FixedString64 key, ref short value)
        {
            return ReadInternal<short>(key, ref value, GameSaveType.Short);
        }

        public unsafe GameSaveResult ReadShort(FixedString64 key, out short value, short defaultValue)
        {
            value = defaultValue;
            return ReadInternal<short>(key, ref value, GameSaveType.Short);
        }

        public unsafe GameSaveResult ReadUshort(FixedString64 key, ref ushort value)
        {
            return ReadInternal<ushort>(key, ref value, GameSaveType.Ushort);
        }

        public unsafe GameSaveResult ReadUshort(FixedString64 key, out ushort value, ushort defaultValue)
        {
            value = defaultValue;
            return ReadInternal<ushort>(key, ref value, GameSaveType.Ushort);
        }

        public unsafe GameSaveResult ReadInt(FixedString64 key, ref int value)
        {
            return ReadInternal<int>(key, ref value, GameSaveType.Int);
        }

        public unsafe GameSaveResult ReadInt(FixedString64 key, out int value, int defaultValue)
        {
            value = defaultValue;
            return ReadInternal<int>(key, ref value, GameSaveType.Int);
        }

        public unsafe GameSaveResult ReadUint(FixedString64 key, ref uint value)
        {
            return ReadInternal<uint>(key, ref value, GameSaveType.Uint);
        }

        public unsafe GameSaveResult ReadUint(FixedString64 key, out uint value, uint defaultValue)
        {
            value = defaultValue;
            return ReadInternal<uint>(key, ref value, GameSaveType.Uint);
        }

        public unsafe GameSaveResult ReadLong(FixedString64 key, ref long value)
        {
            return ReadInternal<long>(key, ref value, GameSaveType.Long);
        }

        public unsafe GameSaveResult ReadLong(FixedString64 key, out long value, long defaultValue)
        {
            value = defaultValue;
            return ReadInternal<long>(key, ref value, GameSaveType.Long);
        }

        public unsafe GameSaveResult ReadUlong(FixedString64 key, ref ulong value)
        {
            return ReadInternal<ulong>(key, ref value, GameSaveType.Ulong);
        }

        public unsafe GameSaveResult ReadUlong(FixedString64 key, out ulong value, ulong defaultValue)
        {
            value = defaultValue;
            return ReadInternal<ulong>(key, ref value, GameSaveType.Ulong);
        }

        public unsafe GameSaveResult ReadFloat(FixedString64 key, ref float value)
        {
            return ReadInternal<float>(key, ref value, GameSaveType.Float);
        }

        public unsafe GameSaveResult ReadFloat(FixedString64 key, out float value, float defaultValue)
        {
            value = defaultValue;
            return ReadInternal<float>(key, ref value, GameSaveType.Float);
        }

        public unsafe GameSaveResult ReadDouble(FixedString64 key, ref double value)
        {
            return ReadInternal<double>(key, ref value, GameSaveType.Double);
        }

        public unsafe GameSaveResult ReadDouble(FixedString64 key, out double value, double defaultValue)
        {
            value = defaultValue;
            return ReadInternal<double>(key, ref value, GameSaveType.Double);
        }

        public unsafe GameSaveResult ReadFixedString64(FixedString64 key, ref FixedString64 value)
        {
            return ReadInternal<FixedString64>(key, ref value, GameSaveType.FixedString64);
        }

        public unsafe GameSaveResult ReadFixedString64(FixedString64 key, out FixedString64 value, in FixedString64 defaultValue)
        {
            value = defaultValue;
            return ReadInternal<FixedString64>(key, ref value, GameSaveType.FixedString64);
        }

        public unsafe GameSaveResult ReadFixedString128(FixedString64 key, ref FixedString128 value)
        {
            return ReadInternal<FixedString128>(key, ref value, GameSaveType.FixedString128);
        }

        public unsafe GameSaveResult ReadFixedString128(FixedString64 key, out FixedString128 value, in FixedString128 defaultValue)
        {
            value = defaultValue;
            return ReadInternal<FixedString128>(key, ref value, GameSaveType.FixedString128);
        }

        public unsafe GameSaveResult ReadComponent<T>(FixedString64 key, ref T value) where T : unmanaged, IComponentData
        {
            return ReadInternal<T>(key, ref value, GameSaveType.ComponentData, TypeManager.GetTypeInfo<T>().StableTypeHash);
        }

        public unsafe GameSaveResult ReadComponent<T>(FixedString64 key, out T value, in T defaultValue) where T : unmanaged, IComponentData
        {
            value = defaultValue;
            return ReadInternal<T>(key, ref value, GameSaveType.ComponentData, TypeManager.GetTypeInfo<T>().StableTypeHash);
        }

        public unsafe GameSaveResult Read<T>(FixedString64 key, ref T value) where T : unmanaged
        {
            GameSaveType gameSaveType = GetGameSaveType<T>();

            if (gameSaveType == GameSaveType.Unknown)
                return ReadBytes(key, UnsafeUtility.AddressOf<T>(ref value), UnsafeUtility.SizeOf<T>());


            ulong stableTypeHash = (gameSaveType == GameSaveType.ComponentData) ? TypeManager.GetTypeInfo<T>().StableTypeHash : 0;
            return ReadInternal<T>(key, ref value, gameSaveType, stableTypeHash);
        }

        public unsafe GameSaveResult Read<T>(FixedString64 key, ref T value, in T defaultValue) where T : unmanaged
        {
            value = defaultValue;
            return Read<T>(key, ref value);
        }

        private unsafe GameSaveResult ReadInternal<T>(FixedString64 key, ref T value, GameSaveType gameSaveType, ulong stableTypeHash = 0) where T : unmanaged
        {
            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                GameSaveResult result = ErrorCheckGameSaveData<T>(ref gameSaveData, gameSaveType, stableTypeHash);
                if (result != GameSaveResult.Success)
                    return result;

                ReadBytes(ref gameSaveData, UnsafeUtility.AddressOf<T>(ref value));
                return GameSaveResult.Success;
            }

            return GameSaveResult.ErrorNotFound;
        }

        public unsafe void WriteBool(FixedString64 key, bool value)
        {
            WriteInternal(key, ref value, GameSaveType.Bool);
        }

        public unsafe void WriteByte(FixedString64 key, byte value)
        {
            WriteInternal(key, ref value, GameSaveType.Byte);
        }

        public unsafe void WriteSbyte(FixedString64 key, sbyte value)
        {
            WriteInternal(key, ref value, GameSaveType.Sbyte);
        }

        public unsafe void WriteChar(FixedString64 key, char value)
        {
            WriteInternal(key, ref value, GameSaveType.Char);
        }

        public unsafe void WriteShort(FixedString64 key, short value)
        {
            WriteInternal(key, ref value, GameSaveType.Short);
        }

        public unsafe void WriteUshort(FixedString64 key, ushort value)
        {
            WriteInternal(key, ref value, GameSaveType.Ushort);
        }

        public unsafe void WriteInt(FixedString64 key, int value)
        {
            WriteInternal(key, ref value, GameSaveType.Int);
        }

        public unsafe void WriteUint(FixedString64 key, uint value)
        {
            WriteInternal(key, ref value, GameSaveType.Uint);
        }

        public unsafe void WriteLong(FixedString64 key, long value)
        {
            WriteInternal(key, ref value, GameSaveType.Long);
        }

        public unsafe void WriteUlong(FixedString64 key, ulong value)
        {
            WriteInternal(key, ref value, GameSaveType.Ulong);
        }

        public unsafe void WriteFloat(FixedString64 key, float value)
        {
            WriteInternal(key, ref value, GameSaveType.Float);
        }

        public unsafe void WriteDouble(FixedString64 key, double value)
        {
            WriteInternal(key, ref value, GameSaveType.Double);
        }

        public unsafe void WriteFixedString64(FixedString64 key, FixedString64 value)
        {
            WriteInternal(key, ref value, GameSaveType.FixedString64);
        }

        public unsafe void WriteFixedString128(FixedString64 key, FixedString128 value)
        {
            WriteInternal(key, ref value, GameSaveType.FixedString128);
        }

        public unsafe void WriteComponent<T>(FixedString64 key, ref T value) where T : unmanaged, IComponentData
        {
            WriteInternal(key, ref value, GameSaveType.ComponentData, TypeManager.GetTypeInfo<T>().StableTypeHash);
        }

        public unsafe GameSaveResult Write<T>(FixedString64 key, T value) where T : unmanaged
        {
            GameSaveType gameSaveType = GetGameSaveType<T>();
            ulong stableTypeHash = (gameSaveType == GameSaveType.ComponentData) ? TypeManager.GetTypeInfo<T>().StableTypeHash : 0;

            if (gameSaveType == GameSaveType.Unknown)
            {
                void* valuePtr = UnsafeUtility.AddressOf<T>(ref value);
                WriteBytes(key, valuePtr, UnsafeUtility.SizeOf<T>());
            }
            else
                WriteInternal<T>(key, ref value, gameSaveType, stableTypeHash);

            return GameSaveResult.Success;
        }

        private unsafe void WriteInternal<T>(FixedString64 key, ref T value, GameSaveType gameSaveType, ulong stableTypeHash = 0) where T : unmanaged
        {
            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                gameSaveData.gameSaveType = gameSaveType;
                gameSaveData.stableTypeHash = stableTypeHash;
                gameSaveData.numElements = 1;
                WriteBytes(ref gameSaveData, UnsafeUtility.AddressOf<T>(ref value), UnsafeUtility.SizeOf<T>());
                m_PersistentData[m_GameSaveIndex][key] = gameSaveData;
            }
            else
            {
                gameSaveData = new GameSaveData();
                gameSaveData.gameSaveType = gameSaveType;
                gameSaveData.stableTypeHash = stableTypeHash;
                gameSaveData.numElements = 1;
                WriteBytes(ref gameSaveData, UnsafeUtility.AddressOf<T>(ref value), UnsafeUtility.SizeOf<T>());
                m_PersistentData[m_GameSaveIndex].TryAdd(key, gameSaveData);
            }
        }

        public unsafe GameSaveResult ReadDynamicBuffer<T>(FixedString64 key, ref DynamicBuffer<T> value) where T : unmanaged
        {
            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                ulong stableTypeHash = TypeManager.GetTypeInfo<T>().StableTypeHash;
                GameSaveResult result = ErrorCheckGameSaveData<T>(ref gameSaveData, GameSaveType.BufferData, stableTypeHash);
                if (result != GameSaveResult.Success)
                    return result;

                value.Length = gameSaveData.numElements;
                ReadBytes(ref gameSaveData, value.GetUnsafePtr());
                return GameSaveResult.Success;
            }

            return GameSaveResult.ErrorNotFound;
        }

        public unsafe void WriteDynamicBuffer<T>(FixedString64 key, ref DynamicBuffer<T> value) where T : unmanaged
        {
            ulong stableTypeHash = TypeManager.GetTypeInfo<T>().StableTypeHash;
            int dynamicBufferSize = value.Length * UnsafeUtility.SizeOf<T>();

            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                gameSaveData.gameSaveType = GameSaveType.BufferData;
                gameSaveData.stableTypeHash = stableTypeHash;
                gameSaveData.numElements = value.Length;
                WriteBytes(ref gameSaveData, value.GetUnsafeReadOnlyPtr(), dynamicBufferSize);
                m_PersistentData[m_GameSaveIndex][key] = gameSaveData;
            }
            else
            {
                gameSaveData = new GameSaveData();
                gameSaveData.gameSaveType = GameSaveType.BufferData;
                gameSaveData.stableTypeHash = stableTypeHash;
                gameSaveData.numElements = value.Length;
                WriteBytes(ref gameSaveData, value.GetUnsafeReadOnlyPtr(), dynamicBufferSize);
                m_PersistentData[m_GameSaveIndex].TryAdd(key, gameSaveData);
            }
        }

        public unsafe GameSaveResult ReadNativeArray<T>(FixedString64 key, ref NativeArray<T> value, Allocator allocator = Allocator.Persistent) where T : unmanaged
        {
            GameSaveData gameSaveData;
            GameSaveType gameSaveType = GetGameSaveType<T>();
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                ulong stableTypeHash = (gameSaveType == GameSaveType.ComponentData) ? TypeManager.GetTypeInfo<T>().StableTypeHash : 0;
                GameSaveResult result = ErrorCheckGameSaveData<T>(ref gameSaveData, gameSaveType, stableTypeHash);
                if (result != GameSaveResult.Success)
                    return result;

                if (value.Length != gameSaveData.numElements)
                {
                    value.Dispose();
                    value = new NativeArray<T>(gameSaveData.numElements, allocator);
                }
                ReadBytes(ref gameSaveData, value.GetUnsafePtr());
                return GameSaveResult.Success;
            }

            return GameSaveResult.ErrorNotFound;
        }

        public unsafe void WriteNativeArray<T>(FixedString64 key, ref NativeArray<T> value) where T : unmanaged
        {
            GameSaveType gameSaveType = GetGameSaveType<T>();
            int nativeArraySizeInBytes = value.Length * UnsafeUtility.SizeOf<T>();
            ulong stableTypeHash = (gameSaveType == GameSaveType.ComponentData) ? TypeManager.GetTypeInfo<T>().StableTypeHash : 0;

            GameSaveData gameSaveData;
            if (m_PersistentData[m_GameSaveIndex].TryGetValue(key, out gameSaveData))
            {
                gameSaveData.gameSaveType = gameSaveType;
                gameSaveData.stableTypeHash = stableTypeHash;
                gameSaveData.numElements = value.Length;
                WriteBytes(ref gameSaveData, value.GetUnsafeReadOnlyPtr(), nativeArraySizeInBytes);
                m_PersistentData[m_GameSaveIndex][key] = gameSaveData;
            }
            else
            {
                gameSaveData = new GameSaveData();
                gameSaveData.gameSaveType = gameSaveType;
                gameSaveData.stableTypeHash = stableTypeHash;
                gameSaveData.numElements = value.Length;
                WriteBytes(ref gameSaveData, value.GetUnsafeReadOnlyPtr(), nativeArraySizeInBytes);
                m_PersistentData[m_GameSaveIndex].TryAdd(key, gameSaveData);
            }
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
                gameSaveData.gameSaveType = GameSaveType.ByteArray;
                gameSaveData.numElements = 1;
                WriteBytes(ref gameSaveData, data, length);
                m_PersistentData[m_GameSaveIndex][key] = gameSaveData;
            }
            else
            {
                gameSaveData = new GameSaveData();
                gameSaveData.gameSaveType = GameSaveType.ByteArray;
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
        public NativeList<FixedString64> GetKeys<T>() where T : IComponentData
        {
            NativeList<FixedString64> keysToReturn = new NativeList<FixedString64>(Allocator.Persistent);
            ulong stableTypeHash = TypeManager.GetTypeInfo<T>().StableTypeHash;

            NativeArray<FixedString64> keyArray = m_PersistentData[m_GameSaveIndex].GetKeyArray(Allocator.Temp);
            NativeArray<GameSaveData> valueArray = m_PersistentData[m_GameSaveIndex].GetValueArray(Allocator.Temp);
            for (int i = 0; i < valueArray.Length; i++)
            {
                if ((valueArray[i].gameSaveType == GameSaveType.ComponentData) && (valueArray[i].stableTypeHash == stableTypeHash))
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
                return gameSaveData.gameSaveType;

            return GameSaveType.Unknown;
        }

        public unsafe void RemoveGameSaveDataFromMemory(int gameSaveIndex = 0)
        {
            if ((gameSaveIndex >= 0) && (gameSaveIndex < m_NumGameSavesMax))
                RemoveGameSaveDataFromMemory(ref m_PersistentData[gameSaveIndex]);
        }

        private static unsafe void RemoveGameSaveDataFromMemory(ref NativeHashMap<FixedString64, GameSaveData> persistentData)
        {
            NativeArray<GameSaveData> values = persistentData.GetValueArray(Allocator.Temp);

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].bufferSize > IntPtr.Size)
                    GameSaveFree((void*)values[i].buffer, values[i].bufferSize);
            }

            values.Dispose();
            persistentData.Clear();
        }

        private static unsafe GameSaveResult ReadGameSaveDataFromDisk(FixedString128 gameSaveFilePath, int unityGameSaveCodeVersion, ref NativeHashMap<FixedString64, GameSaveData> persistentData)
        {
            GameSaveResult result = GameSaveResult.ErrorReadFailed;

            Baselib_ErrorState errorState = new Baselib_ErrorState();
            Baselib_FileIO_SyncFile gameSaveFileHandle = Baselib_FileIO_SyncOpen(gameSaveFilePath.GetUnsafePtr(), Baselib_FileIO_OpenFlags.Read, &errorState);
            if (errorState.code == Baselib_ErrorCode.Success)
            {
                UInt64 fileSize = Baselib_FileIO_SyncGetFileSize(gameSaveFileHandle, &errorState);
                if (fileSize > Int32.MaxValue)
                {
                    Baselib_FileIO_SyncClose(gameSaveFileHandle, &errorState);
                    return GameSaveResult.ErrorReadFailed;
                }

                IntPtr buffer = (IntPtr)GameSaveMalloc((int)fileSize);
                UInt64 bytesRead = Baselib_FileIO_SyncRead(gameSaveFileHandle, 0, buffer, fileSize, &errorState);
                Baselib_FileIO_SyncClose(gameSaveFileHandle, &errorState);

                // Need to parse fileBytes and update our NativeHashMap.
                if (bytesRead > 0)
                {
                    MemoryBinaryReader reader = new MemoryBinaryReader((byte*)buffer);
                    int unityGameSaveFileVersionRead = reader.ReadInt();
                    if (unityGameSaveFileVersionRead == unityGameSaveCodeVersion)
                    {
                        RemoveGameSaveDataFromMemory(ref persistentData);
                        ReadHashMap(reader, persistentData);
                        result = GameSaveResult.Success;
                    }
                    else
                    {
                        // If we bump our Unity version, this is where we'll handle Unity-side data updates. We should come out of this
                        // having updated the game save file version to match the game save code version. This does not include the more
                        // common case where a user changes their data.
                    }

                    reader.Dispose();
                }

                GameSaveFree((void*)buffer, (int)fileSize);
            }

            return result;
        }

        private static unsafe GameSaveResult ReadGameSaveDataFromBuffer(ref DynamicBuffer<GameSaveBufferByte> buffer, int unityGameSaveCodeVersion, ref NativeHashMap<FixedString64, GameSaveData> persistentData)
        {
            GameSaveResult result = GameSaveResult.ErrorReadFailed;
            if (buffer.Length > 0)
            {
                MemoryBinaryReader reader = new MemoryBinaryReader((byte*)buffer.GetUnsafeReadOnlyPtr());
                int unityGameSaveFileVersionRead = reader.ReadInt();
                if (unityGameSaveFileVersionRead == unityGameSaveCodeVersion)
                {
                    RemoveGameSaveDataFromMemory(ref persistentData);
                    ReadHashMap(reader, persistentData);
                    result = GameSaveResult.Success;
                }
                else
                {
                    // If we bump our Unity version, this is where we'll handle Unity-side data updates. We should come out of this
                    // having updated the game save file version to match the game save code version. This does not include the more
                    // common case where a user changes their data.
                }

                reader.Dispose();
            }

            return result;
        }

        private static unsafe bool WriteGameSaveDataToDisk(FixedString128 gameSaveFilePath, int unityGameSaveCodeVersion, ref NativeHashMap<FixedString64, GameSaveData> persistentData)
        {
            bool fileWritten = false;

            // Need to write NativeHashMaps to fileBytes, so fileBytes can be written to disk.
            MemoryBinaryWriter writer = new MemoryBinaryWriter();
            writer.Write(unityGameSaveCodeVersion);
            WriteHashMap(writer, persistentData);

            Baselib_ErrorState errorState = new Baselib_ErrorState();
            Baselib_FileIO_SyncFile gameSaveFileHandle = Baselib_FileIO_SyncOpen(gameSaveFilePath.GetUnsafePtr(), Baselib_FileIO_OpenFlags.CreateAlways | Baselib_FileIO_OpenFlags.Write, &errorState);
            if (errorState.code == Baselib_ErrorCode.Success)
            {
                Baselib_FileIO_SyncWrite(gameSaveFileHandle, 0, (IntPtr)writer.Data, (uint)writer.Length, &errorState);
                Baselib_FileIO_SyncFlush(gameSaveFileHandle, &errorState);
                Baselib_FileIO_SyncClose(gameSaveFileHandle, &errorState);
                fileWritten = true;
            }

            writer.Dispose();
            return fileWritten;
        }

        private static unsafe bool WriteGameSaveDataToBuffer(ref DynamicBuffer<GameSaveBufferByte> buffer, int unityGameSaveCodeVersion, ref NativeHashMap<FixedString64, GameSaveData> persistentData)
        {
            // Need to write NativeHashMaps to fileBytes, so fileBytes can be written to disk.
            MemoryBinaryWriter writer = new MemoryBinaryWriter();
            writer.Write(unityGameSaveCodeVersion);
            WriteHashMap(writer, persistentData);

            buffer.Length = writer.Length;
            UnsafeUtility.MemCpy(buffer.GetUnsafePtr(), writer.Data, writer.Length);
            writer.Dispose();
            return true;
        }

        private GameSaveType GetGameSaveType<T>()
        {
            GameSaveType gameSaveType = GameSaveType.Unknown;

            gameSaveType = typeof(T) == typeof(bool) ? GameSaveType.Bool : gameSaveType;
            gameSaveType = typeof(T) == typeof(byte) ? GameSaveType.Byte : gameSaveType;
            gameSaveType = typeof(T) == typeof(char) ? GameSaveType.Char : gameSaveType;
            gameSaveType = typeof(T) == typeof(short) ? GameSaveType.Short : gameSaveType;
            gameSaveType = typeof(T) == typeof(ushort) ? GameSaveType.Ushort : gameSaveType;
            gameSaveType = typeof(T) == typeof(int) ? GameSaveType.Int : gameSaveType;
            gameSaveType = typeof(T) == typeof(uint) ? GameSaveType.Uint : gameSaveType;
            gameSaveType = typeof(T) == typeof(float) ? GameSaveType.Float : gameSaveType;
            gameSaveType = typeof(T) == typeof(double) ? GameSaveType.Double : gameSaveType;
            gameSaveType = typeof(T) == typeof(long) ? GameSaveType.Long : gameSaveType;
            gameSaveType = typeof(T) == typeof(ulong) ? GameSaveType.Ulong : gameSaveType;
            gameSaveType = typeof(T) == typeof(FixedString64) ? GameSaveType.FixedString64 : gameSaveType;
            gameSaveType = typeof(T) == typeof(FixedString128) ? GameSaveType.FixedString128 : gameSaveType;
            gameSaveType = typeof(IComponentData).IsAssignableFrom(typeof(T)) ? GameSaveType.ComponentData : gameSaveType;

            return gameSaveType;
        }

        private static unsafe void ReadHashMap(MemoryBinaryReader reader, NativeHashMap<FixedString64, GameSaveData> hashMap)
        {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                FixedString64 key;
                reader.ReadBytes(&key, sizeof(FixedString64));

                GameSaveData gameSaveData = new GameSaveData();
                gameSaveData.gameSaveType = (GameSaveType)reader.ReadInt();
                gameSaveData.stableTypeHash = 0;
                if ((gameSaveData.gameSaveType == GameSaveType.ComponentData) || (gameSaveData.gameSaveType == GameSaveType.BufferData))
                    gameSaveData.stableTypeHash = reader.ReadULong();
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

        private static unsafe void WriteHashMap(MemoryBinaryWriter writer, NativeHashMap<FixedString64, GameSaveData> hashMap)
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
                    writer.Write((int)gameSaveData.gameSaveType);
                    if ((gameSaveData.gameSaveType == GameSaveType.ComponentData) || (gameSaveData.gameSaveType == GameSaveType.BufferData))
                        writer.Write(gameSaveData.stableTypeHash);
                    writer.Write(gameSaveData.numElements);
                    writer.Write(gameSaveData.bufferSize);

                    if (gameSaveData.bufferSize > IntPtr.Size)
                        writer.WriteBytes((void*)gameSaveData.buffer, gameSaveData.bufferSize);
                    else
                        writer.WriteBytes(&gameSaveData.buffer, gameSaveData.bufferSize);
                }
            }
        }
    }
}
