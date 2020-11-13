using System;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Baselib.LowLevel;
using static Unity.Baselib.LowLevel.Binding;

namespace Unity.Tiny.GameSave
{
    static public class GameSaveNativeCalls
    {
        private struct GameSaveFile
        {
            public FixedString128 filePath;
            public Baselib_FileIO_SyncFile fileHandle;
        }

        static private NativeList<GameSaveFile> gameSaveReadFiles;

        public static void Init()
        {
            gameSaveReadFiles = new NativeList<GameSaveFile>(Allocator.Persistent);
        }

        public static void Shutdown()
        {
            gameSaveReadFiles.Dispose();
        }

        public static unsafe bool WriteToDisk(FixedString128 gameSaveFilePath, MemoryBinaryWriter writer)
        {
            Baselib_ErrorState errorState = new Baselib_ErrorState();

            Baselib_FileIO_SyncFile gameSaveFileHandle = Baselib_FileIO_SyncOpen(gameSaveFilePath.GetUnsafePtr(), Baselib_FileIO_OpenFlags.CreateAlways | Baselib_FileIO_OpenFlags.Write, &errorState);
            if (errorState.code != Baselib_ErrorCode.Success)
                return false;
            
            Baselib_FileIO_SyncWrite(gameSaveFileHandle, 0, (IntPtr)writer.Data, (uint)writer.Length, &errorState);
            if (errorState.code != Baselib_ErrorCode.Success)
                return false;

            Baselib_FileIO_SyncFlush(gameSaveFileHandle, &errorState);
            if (errorState.code != Baselib_ErrorCode.Success)
                return false;

            // If we get this far, we'll consider the write a success because it should have been completed.
            Baselib_FileIO_SyncClose(gameSaveFileHandle, &errorState);
            return true;
        }

        public static unsafe bool ReadFromDisk(FixedString128 gameSaveFilePath)
        {
            Baselib_ErrorState errorState = new Baselib_ErrorState();
            Baselib_FileIO_SyncFile fileHandle = Baselib_FileIO_SyncOpen(gameSaveFilePath.GetUnsafePtr(), Baselib_FileIO_OpenFlags.Read, &errorState);
            if (errorState.code != Baselib_ErrorCode.Success)
                return false;

            GameSaveFile gameSaveFile;
            gameSaveFile.filePath = gameSaveFilePath;
            gameSaveFile.fileHandle = fileHandle;
            gameSaveReadFiles.Add(gameSaveFile);
            return true;
        }

        public static unsafe bool GetLength(FixedString128 gameSaveFilePath, int* length)
        {
            for (int i = 0; i < gameSaveReadFiles.Length; i++)
            {
                if (gameSaveReadFiles[i].filePath == gameSaveFilePath)
                {
                    Baselib_ErrorState errorState = new Baselib_ErrorState();
                    UInt64 fileSize = Baselib_FileIO_SyncGetFileSize(gameSaveReadFiles[i].fileHandle, &errorState);
                    if ((errorState.code == Baselib_ErrorCode.Success) && (fileSize <= Int32.MaxValue))
                    {
                        *length = (int)fileSize;
                        return true;
                    }
                }
            }

            return false;
        }

        public static unsafe bool PullCompletedReadBuffer(FixedString128 gameSaveFilePath, byte* buffer, int bufferLength)
        {
            for (int i = 0; i < gameSaveReadFiles.Length; i++)
            {
                if (gameSaveReadFiles[i].filePath == gameSaveFilePath)
                {
                    Baselib_ErrorState errorState = new Baselib_ErrorState();
                    bool result = false;

                    UInt64 fileSize = Baselib_FileIO_SyncGetFileSize(gameSaveReadFiles[i].fileHandle, &errorState);
                    if ((errorState.code == Baselib_ErrorCode.Success) && ((int)fileSize == bufferLength))
                    {
                        Baselib_FileIO_SyncRead(gameSaveReadFiles[i].fileHandle, 0, (IntPtr)buffer, fileSize, &errorState);
                        if (errorState.code == Baselib_ErrorCode.Success)
                            result = true;    
                    }

                    Baselib_FileIO_SyncClose(gameSaveReadFiles[i].fileHandle, &errorState);
                    gameSaveReadFiles.RemoveAtSwapBack(i);
                    return result;
                }
            }

            return false;
        }
    }
}
