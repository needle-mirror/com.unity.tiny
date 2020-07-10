#include "TinyIO.h"

#include <allocators.h>
#include <baselibext.h>

#include <assert.h>
#include <array>
#include <atomic>
#include <deque>
#include <string>

#if defined __EMSCRIPTEN__
    #include <emscripten.h>
    #include <emscripten/fetch.h>
#if SINGLE_FILE
    extern "C" bool js_fetch_embedded(const char* path, void** ppData, size_t* pLen);
#endif
#else
    #include <stdio.h>
    #include <stdlib.h>
    #include <sys/stat.h>
    #include "ThreadPool.h"

    using namespace ut::ThreadPool;
#endif

using namespace Unity::LowLevel;

namespace Unity { namespace Tiny { namespace IO
{
    struct Request
    {
        Request(int index = -1) : mIndex(index) {}

        uint64_t mJobId; // platform specific
        bool mbOwnPayload = true;
        void* mpPayload = nullptr;
        size_t mPayloadSize = 0;
        int mIndex;
        Status mStatus = Status::NotStarted;
        ErrorStatus mErrorStatus = ErrorStatus::None;
    }; 

    class RequestPool
    {
        static const int kGrowSizeExponent = 6; 
        static const int kGrowSize = 1 << kGrowSizeExponent;
        static_assert((kGrowSize & 0x1) == 0, "kGrowSize must be a power of 2");

    public:
        RequestPool()
        {
            mRequests.emplace_front();
            for (int i = 0; i < kGrowSize; ++i)
            {
                mRequests[0][i].mIndex = i;
                mFreeRequests.push_back(i);
            }
        }

        int GetRequestIndex()
        {
			BaselibLock lock(mLock);
            if (mFreeRequests.empty())
            {
                int origSize = (int) mRequests.size() * kGrowSize;
                int newSize = (int) origSize + kGrowSize;
                int index = (int) mRequests.size();

                mRequests.emplace_back();

                for (int i = 0; i < kGrowSize; ++i)
                {
                    int newRequestIndex = origSize + i;
                    mRequests[index][i].mIndex = newRequestIndex;
                    mFreeRequests.push_back(newRequestIndex);
                }
            }

            int requestIndex = mFreeRequests.front();
            assert(requestIndex >= 0);
            mFreeRequests.pop_front();

            return requestIndex;
        }

        Request& GetRequest(int index)
        {
            int dequeIndex = index >> kGrowSizeExponent;
            int arrayIndex = index & (kGrowSize-1);
            return mRequests[dequeIndex][arrayIndex];
        }

        void FreeRequest(int index)
        {
			BaselibLock lock(mLock);
            mFreeRequests.push_back(index);
        }

    private:
		baselib::Lock mLock;
        std::deque<std::array<Request, kGrowSize>> mRequests;
        std::deque<int> mFreeRequests;
    };

    static RequestPool sRequestPool;

    DOTS_EXPORT(int)
    GetStatus(int requestIndex)
    {
        Request& request = sRequestPool.GetRequest(requestIndex);

        Status status = request.mStatus;
        return (int) status;
    }

    DOTS_EXPORT(int)
    GetErrorStatus(int requestIndex)
    {
        Request& request = sRequestPool.GetRequest(requestIndex);

        return (int)request.mErrorStatus;
    }

    DOTS_EXPORT(void)
    GetData(int requestIndex, const char** data, int* len)
    {
        Request& request = sRequestPool.GetRequest(requestIndex);

        if (request.mStatus != Status::Success)
        {
            *data = nullptr;
            *len = 0;

            return;
        }

        *data = (const char*)request.mpPayload;
        *len = (int) request.mPayloadSize;
    }

#if defined __EMSCRIPTEN__
    // Fetch Callbacks
    static void OnSuccess(emscripten_fetch_t* pFetch)
    {
        int requestIndex = (int)pFetch->userData;
        Request& request = sRequestPool.GetRequest(requestIndex);
        request.mStatus = Status::Success;
        request.mpPayload = (void*) pFetch->data;
        request.mPayloadSize = pFetch->numBytes;
    }

    static void OnError(emscripten_fetch_t* pFetch)
    {
        int requestIndex = (int)pFetch->userData;
        Request& request = sRequestPool.GetRequest(requestIndex);

        request.mStatus = Status::Failure;
        request.mpPayload = nullptr;
        request.mPayloadSize = 0;

        switch (pFetch->status)
        {
            case 404:
                request.mErrorStatus = ErrorStatus::FileNotFound;
                break;
            default:
                request.mErrorStatus = ErrorStatus::Unknown;
                break;
        }
    }


    // Async API
    /////////////
    DOTS_EXPORT(int)
    RequestAsyncRead(const char* path, void* pData, int len)
    {
        int requestIndex = sRequestPool.GetRequestIndex();
        Request& request = sRequestPool.GetRequest(requestIndex);

        request.mStatus = Status::InProgress;
        request.mErrorStatus = ErrorStatus::None;

#if SINGLE_FILE
        if (js_fetch_embedded(path, &request.mpPayload, &request.mPayloadSize))
        {
            request.mStatus = Status::Success;
        }
        else
        {
            request.mStatus = Status::Failure;
            request.mErrorStatus = ErrorStatus::FileNotFound;
        }
#else
        emscripten_fetch_attr_t attr;
        emscripten_fetch_attr_init(&attr);

        strcpy(attr.requestMethod, "GET");
        attr.attributes = EMSCRIPTEN_FETCH_LOAD_TO_MEMORY;
        attr.onsuccess = OnSuccess;
        attr.onerror = OnError;
        attr.userData = (void*)requestIndex;

        request.mJobId = (uint64_t) emscripten_fetch(&attr, path);
#endif

        return requestIndex;
    }

    DOTS_EXPORT(void)
    Close(int requestIndex)
    {
        if (requestIndex < 0)
            return;

        Request& request = sRequestPool.GetRequest(requestIndex);
        
#if !SINGLE_FILE
        emscripten_fetch_close((emscripten_fetch_t*)request.mJobId);
#endif

        request.mpPayload = nullptr;
        request.mPayloadSize = 0;
        request.mStatus = Status::NotStarted;
        request.mErrorStatus = ErrorStatus::None;

        assert(request.mIndex == requestIndex);
        sRequestPool.FreeRequest(request.mIndex);
    }

#else
    // Async API
    /////////////

#if defined(UNITY_ANDROID)
    extern "C" void* loadAsset(const char *path, int *size, void* (*alloc)(size_t));

    DOTS_EXPORT(int)
    RequestAsyncRead(const char* path, void* pData, int len)
    {
        int requestIndex = sRequestPool.GetRequestIndex();
        Request& request = sRequestPool.GetRequest(requestIndex);

        request.mStatus = Status::InProgress;
        request.mErrorStatus = ErrorStatus::None;
        request.mbOwnPayload = len == 0;

        // Just do syncrounous IO on native for now
        int size;
        void* data = loadAsset(path, &size, [](size_t bytes) -> void* { return unsafeutility_malloc(bytes, 16, Allocator::Persistent); });
        // This is terrible, but need platforms changes to offer a buffer to be filled in a safe manner
        if (!request.mbOwnPayload)
            memcpy(pData, data, size < len ? size : len);

        if (data == NULL)
        {
            request.mStatus = Status::Failure;
            request.mErrorStatus = ErrorStatus::FileNotFound;
        }
        else
        {
            request.mpPayload = data;
            request.mPayloadSize = size;
            request.mStatus = Status::Success;
        }

        return requestIndex;
    }
#else

    class ReadJob : public Job {
    public:
    
        int mRequestIndex;
        std::string mPath;

        virtual bool Do()
        {
            Request& request = sRequestPool.GetRequest(mRequestIndex);

            // Artifically slow down IO
#if 0
            for (int i = 0; i < 20; i++) {
                std::this_thread::sleep_for(std::chrono::milliseconds(20));
                progress = i;
            }
#endif

            if (abort)
            {
                return true;
            }

            struct stat statBuf;
            int res = stat(mPath.c_str(), &statBuf);

            FILE* pFile = fopen(mPath.c_str(), "rb");
            if (!pFile || res != 0)
            {
                request.mStatus = Status::Failure;
                request.mErrorStatus = ErrorStatus::FileNotFound;
            }
            else
            {
                int size = statBuf.st_size;
                if (request.mbOwnPayload)
                {
                    request.mpPayload = unsafeutility_malloc(size, 16, Allocator::Persistent);
                    request.mPayloadSize = size;
                }
                else
                {
                    request.mPayloadSize = size < request.mPayloadSize ? size : request.mPayloadSize;
                }

                int bytesRead = (int) fread(request.mpPayload, 1, request.mPayloadSize, pFile);
                if (bytesRead != request.mPayloadSize)
                {
                    request.mStatus = Status::Failure;
                }
                else
                {
                    request.mStatus = Status::Success;
                }

                fclose(pFile);
            }

            return true;
        }
    };

    DOTS_EXPORT(int)
    RequestAsyncRead(const char* path, void* pData, int len)
    {
        int requestIndex = sRequestPool.GetRequestIndex();
        Request& request = sRequestPool.GetRequest(requestIndex);

        request.mStatus = Status::InProgress;
        request.mErrorStatus = ErrorStatus::None;

        std::unique_ptr<ReadJob> readJob(new ReadJob);
        readJob->mPath = path;
        readJob->mRequestIndex = requestIndex;

        request.mJobId = Pool::GetInstance()->Enqueue(std::move(readJob));
        request.mpPayload = pData;
        request.mPayloadSize = len;
        request.mbOwnPayload = len == 0;

        return requestIndex;
    }
#endif

    DOTS_EXPORT(void)
    Close(int requestIndex)
    {
        if (requestIndex < 0)
            return;

        Request& request = sRequestPool.GetRequest(requestIndex);

#if !defined(UNITY_ANDROID)
        auto job = Pool::GetInstance()->CheckAndRemove(request.mJobId);
        if(job && !job->GetReturnValue())
            Pool::GetInstance()->Abort(request.mJobId);
#endif

        if(request.mbOwnPayload)
            unsafeutility_free(request.mpPayload, Allocator::Persistent);

        request.mpPayload = nullptr;
        request.mPayloadSize = 0;
        request.mStatus = Status::NotStarted;
        request.mErrorStatus = ErrorStatus::None;

        assert(request.mIndex == requestIndex);
        sRequestPool.FreeRequest(request.mIndex);
    }
#endif
}}} // namespace Unity::Tiny::IO
