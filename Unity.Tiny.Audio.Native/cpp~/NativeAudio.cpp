#include "NativeAudio.h"
#include "SoundClip.h"
#include "SoundSource.h"
#include <allocators.h>
#include <baselibext.h>

// Using baselib for now since we need realloc and we currently don't support it in Unity::LowLevel
#include <Baselib.h>
#include <C/Baselib_Timer.h>

#include <stdlib.h>
#include <stdio.h>
#include <assert.h>
#include <limits.h>

#include <string>
#include <map>
#include <vector>

#include <Unity/Runtime.h>

#define DR_FLAC_IMPLEMENTATION
#include "./miniaudio/extras/dr_flac.h"     /* Enables FLAC decoding. */
#define DR_MP3_IMPLEMENTATION
#include "./miniaudio/extras/dr_mp3.h"      /* Enables MP3 decoding. */
#define DR_WAV_IMPLEMENTATION
#include "./miniaudio/extras/dr_wav.h"      /* Enables WAV decoding. */
#define STB_VORBIS_HEADER_ONLY
#include "./miniaudio/extras/stb_vorbis.c"  /* Enables OGG decoding. */

#define MA_MALLOC(sz)           unsafeutility_malloc(sz,16,Unity::LowLevel::Allocator::Persistent)
#define MA_REALLOC(p, sz)       unsafeutility_realloc(p,sz,16,Unity::LowLevel::Allocator::Persistent)
#define MA_FREE(p)              unsafeutility_free(p,Unity::LowLevel::Allocator::Persistent)
#define MINIAUDIO_IMPLEMENTATION
#include "./miniaudio/miniaudio.h"

using namespace Unity::LowLevel;

// Need a mutex to protect access to the SoundSources 
// that are used by the callback. The SoundClips are
// refCounted; so they are safe.
static baselib::Lock soundSourceMutex;
static uint32_t clipIDPool = 0;
static std::map<uint32_t, SoundClip*> clipMap;
static uint32_t sourceIDPool = 0;
static std::map<uint32_t, SoundSource*> sourceMap;

static ma_device_config maConfig;
static ma_device* maDevice;
struct UserData
{
    void* dummy;
};
static UserData userData;

static bool audioInitialized = false;
static bool audioPaused = false;
static bool audioMuted = false;
static uint64_t audioOutputTimeInFrames = 0;
static float *mixBuffer = nullptr;
static float maxSample = 0.9f;
static uint32_t numFramesSinceMaxSample = 0;
// Our mix buffer is 8K frames, 2 samples/frame (stereo), and each sample is a float ranging from -1.0f to 1.0f.
static const uint32_t mixBufferSize = 8192*2*sizeof(float);
static const float limiterHeadroom = 0.1f;
static const uint32_t limiterWindowInFrames = 22050;

void flushMemory()
{
    std::vector<std::map<uint32_t, SoundClip*>::iterator> clipDeleteList;
    std::vector<std::map<uint32_t, SoundSource*>::iterator> sourceDeleteList;

    for (auto it = clipMap.begin(); it != clipMap.end(); ++it) {
        SoundClip* clip = it->second;
        if (clip->isQueuedForDeletion() && clip->refCount() == 0) {
            clipDeleteList.push_back(it);
        }
    }
    for (int i = 0; i < (int)clipDeleteList.size(); ++i) {
        SoundClip* clip = clipDeleteList[i]->second;
        delete clip;
        clipMap.erase(clipDeleteList[i]);
    }

    BaselibLock lock(soundSourceMutex);
    for (auto it = sourceMap.begin(); it != sourceMap.end(); ++it) {
        SoundSource* source = it->second;
        if (source->readyToDelete()) {
            sourceDeleteList.push_back(it);
        }
    }

    for (int i = 0; i < (int)sourceDeleteList.size(); ++i) {
        SoundSource* source = sourceDeleteList[i]->second;
        delete source;
        LOGE("Deleting sound source.");
        sourceMap.erase(sourceDeleteList[i]);
    }
}

void freeAllSources()
{
    BaselibLock lock(soundSourceMutex);
    while (!sourceMap.empty()) {
        auto it = sourceMap.begin();
        SoundSource* source = it->second;
        source->stop();
        delete source;
        sourceMap.erase(it);
    }

    sourceIDPool = 0;
}

void freeAllClips()
{
    for (auto it = clipMap.begin(); it != clipMap.end(); ++it) {
        SoundClip* clip = it->second;
        clip->queueDeletion();
    }
    flushMemory();
    assert(clipMap.empty());

    clipIDPool = 0;
}

DOTS_EXPORT(void)
freeAudio(uint32_t clipID)
{
    if (!audioInitialized) return;

    LOGE("freeAudio(%d)", clipID);
    auto it = clipMap.find(clipID);
    if (it != clipMap.end()) {
        SoundClip* clip = it->second;
        clip->queueDeletion();
    }
    else {
        LOGE("freeAudio(%d) not found.", clipID);
    }
    flushMemory();
}

void* createTestWAV(const char* name, size_t* size)
{
    int nFrames = 0;
    int channels = 0;
    int bitsPerSample = 0;
    int frequency = 0;

    const char* p = strchr(name, '/');
    if (p && *p) {
        nFrames = atoi(p + 1);
        p = strchr(p + 1, '/');
        if (p && *p) {
            channels = atoi(p + 1);
            p = strchr(p + 1, '/');
            if (p && *p) {
                bitsPerSample = atoi(p + 1);
                p = strchr(p + 1, '/');
                if (p && *p)
                    frequency = atoi(p + 1);
            }
        }
    }
    return SoundClip::constructWAV(nFrames, channels, bitsPerSample, frequency, size);
}

#if defined(UNITY_ANDROID)
extern "C" void* loadAsset(const char* path, int* size, void* (*alloc)(size_t));
#endif

DOTS_EXPORT(uint32_t)
startLoad(const char* path)
{
    if (!audioInitialized) return 0;

    ++clipIDPool;

    if (strstr(path, "!audiotest!"))
    {
        size_t size = 0;
        void* mem = createTestWAV(path, &size);
        clipMap[clipIDPool] = new SoundClip(mem, size);
    }
    else
    {
#if defined(UNITY_ANDROID)
        // Don't let miniaudio handle IO. Load the asset upfront and
        // pass the audio buffer to miniaudio. If the filepath is incorrect
        // null is returned and the error will be reported when SoundClip is used
        // (startLoad() doesn't allow for failure in it's API) 
        int size = 0;
        void* data = loadAsset(path, &size, [](size_t bytes) -> void* { return unsafeutility_malloc(bytes, 16, Allocator::Persistent); });
        clipMap[clipIDPool] = new SoundClip(data, size);
#else
        clipMap[clipIDPool] = new SoundClip(std::string(path));
#endif
    }

    LOGE("startLoad(%s) id=%d", path, clipIDPool);
    return clipIDPool;
}

// Testing
DOTS_EXPORT(int32_t)
numSourcesAllocated()
{
    flushMemory();
    BaselibLock lock(soundSourceMutex);
    LOGE("numSourcesAllocated=%d", (int)sourceMap.size());
    return (int)sourceMap.size();
}

// Testing
DOTS_EXPORT(int32_t)
numClipsAllocated()
{
    flushMemory();
    BaselibLock lock(soundSourceMutex);
    LOGE("numClipsAllocated=%d", (int)clipMap.size());
    return (int)clipMap.size();
}

// Testing
DOTS_EXPORT(int32_t)
sourcePoolID()
{
    flushMemory();
    BaselibLock lock(soundSourceMutex);
    LOGE("sourcePoolID=%d", (int)sourcePoolID);
    return sourceIDPool;
}

DOTS_EXPORT(int32_t)
clipPoolID()
{
    flushMemory();
    LOGE("clipPoolID=%d", (int)clipPoolID);
    return clipIDPool;
}

DOTS_EXPORT(int)
checkLoading(uint32_t id)
{
    if (!audioInitialized) return SoundClip::SoundClipStatus::FAIL;
    flushMemory();

    auto it = clipMap.find(id);
    if (it == clipMap.end()) {
        LOGE("checkLoading(%d) not found", id);
        return SoundClip::SoundClipStatus::FAIL;
    }
    SoundClip* clip = it->second;
    return clip->checkLoad();
}

DOTS_EXPORT(void)
abortLoad(uint32_t id)
{
    if (!audioInitialized) return;
    LOGE("abortLoad(%d)", id);
    freeAudio(id);
}

DOTS_EXPORT(void)
finishedLoading(uint32_t id)
{
    if (!audioInitialized) return;
    LOGE("finishedLoading(%d)", id);
    // does nothing.
}

DOTS_EXPORT(void)
pauseAudio(bool _audioPaused)
{
    if (_audioPaused != audioPaused) {
        audioPaused = _audioPaused;
        LOGE("%s", audioPaused ? "*paused*" : "*un-paused");
    }

}

DOTS_EXPORT(bool)
hasDefaultDeviceChanged()
{
#if UNITY_MACOSX
    return maDevice ? maDevice->coreaudio.hasDefaultPlaybackDeviceChanged : false;
#else
    return false;
#endif
}

DOTS_EXPORT(uint64_t)
getAudioOutputTimeInFrames()
{
    return audioOutputTimeInFrames;
}

DOTS_EXPORT(bool)
setVolume(uint32_t sourceID, float volume)
{
    if (!audioInitialized) return 0;
    flushMemory();

    BaselibLock lock(soundSourceMutex);
    auto it = sourceMap.find(sourceID);
    if (it == sourceMap.end()) {
        LOGE("setVolume() sourceID=%d failed.", sourceID);
        return false;
    }

    it->second->setVolume(volume);
    return true;
}

DOTS_EXPORT(bool)
setPan(uint32_t sourceID, float pan)
{
    if (!audioInitialized) return 0;
    flushMemory();

    BaselibLock lock(soundSourceMutex);
    auto it = sourceMap.find(sourceID);
    if (it == sourceMap.end()) {
        LOGE("setPan() sourceID=%d failed.", sourceID);
        return false;
    }

    it->second->setPan(pan);
    return true;
}

DOTS_EXPORT(bool)
setPitch(uint32_t sourceID, float pitch)
{
    if (!audioInitialized) return 0;
    flushMemory();

    BaselibLock lock(soundSourceMutex);
    auto it = sourceMap.find(sourceID);
    if (it == sourceMap.end()) {
        LOGE("setPitch() sourceID=%d failed.", sourceID);
        return false;
    }

    it->second->setPitch(pitch);
    return true;
}

DOTS_EXPORT(void)
setIsMuted(bool muted)
{
    audioMuted = muted;
}

#ifdef ENABLE_PROFILER
static Baselib_Timer_Ticks callbackTicksLastEnd = 0;

static const int kCallbackCpuCount = 4;
static int callbackCpuIndex = 0;
static Baselib_Timer_Ticks callbackCpuPercent[kCallbackCpuCount] = { 0 };
#endif

// At 44,100 hz, stereo, 16-bit
// 44100 frames / second.
// Typical callback = 223 frames
// ~0.005 seconds = 5ms = 5000 microseconds of data
void sendFramesToDevice(ma_device* pDevice, void* pSamples, const void* pInput, ma_uint32 frameCount)
{
#ifdef ENABLE_PROFILER
    Baselib_Timer_Ticks start = Baselib_Timer_GetHighPrecisionTimerTicks();
#endif

    const uint32_t bytesPerSample = ma_get_bytes_per_sample(pDevice->playback.format);
    const uint32_t bytesPerFrame = ma_get_bytes_per_frame(pDevice->playback.format, pDevice->playback.channels);
    const float SHRT_MAX_FLOAT = (float)SHRT_MAX;

    ASSERT(bytesPerSample == 2);
    ASSERT(bytesPerFrame == 4);
    ASSERT(mixBufferSize >= frameCount*2*sizeof(float));

    if (audioPaused)
        return;

    if ((mixBuffer == nullptr) || (mixBufferSize < frameCount*2*sizeof(float)))
        return;

    for (ma_uint32 i = 0; i < frameCount*2; i++)
        mixBuffer[i] = 0.0f;

    UserData* pUser = (UserData*)(pDevice->pUserData);

    BaselibLock lock(soundSourceMutex);

    for (auto it = sourceMap.begin(); it != sourceMap.end(); ++it) 
    {
        SoundSource* source = it->second;
        if (source->isPlaying()) 
        {
            bool done = false;

            uint32_t totalFrames = 0;
            float* target = mixBuffer;

            // when pan is at center, setting both channels to .7 instead of .5 sounds more natural
            // this is an approximation to sqrt(2) = 45 degree angle on unit circle, and for now
            // we'll linearly interpolate to the extremes rather than rotate
            float volume = audioMuted ? 0.0f : source->volume();
            float pan = source->pan();
            float coeffL = (.7f - (pan > 0 ? pan * .7f : pan * .3f)) * volume;
            float coeffR = (.7f + (pan < 0 ? pan * .7f : pan * .3f)) * volume;

            while (!done)
            {
                uint32_t decodedFrames = 0;
                uint32_t requestedFrames = frameCount - totalFrames;

                const float* src = source->fetch(requestedFrames, &decodedFrames);
                totalFrames += decodedFrames;

                // Now 'buffer' is the source. Apply the volume and copy to 'pSamples'
                for (uint32_t i = 0; i < decodedFrames; ++i) 
                {   
                    *target += *src * coeffL;
                    ++target;
                    ++src;

                    *target += *src * coeffR;
                    ++target;
                    ++src;
                }

                done = true;
                if (source->loop() && totalFrames < frameCount) 
                {
                    done = false;
                    source->rewind();
                }
            }
        }
    }

    // Find the maximum sample in this buffer.
    float maxSampleInBuffer = 1.0f;
    for (ma_uint32 i = 0; i < frameCount*2; i++)
    {
        maxSampleInBuffer = mixBuffer[i] > maxSampleInBuffer ? mixBuffer[i] : maxSampleInBuffer;
        maxSampleInBuffer = mixBuffer[i] < -1.0f*maxSampleInBuffer ? -1.0f*mixBuffer[i] : maxSampleInBuffer;
    }

    // Check if we need to increase our global max sample, based on the values in our most recent buffer.
    maxSample = maxSampleInBuffer > maxSample ? maxSampleInBuffer : maxSample;

    // Apply our float-to-short conversion and limiter factors together.
    float conversionAndLimiterFactor = maxSample > 1.0f ? SHRT_MAX_FLOAT/maxSample : SHRT_MAX_FLOAT;
    int16_t* pSamplesShort = (int16_t*)pSamples;
    for (ma_uint32 i = 0; i < frameCount*2; i++)
        pSamplesShort[i] = (int16_t)(mixBuffer[i] * conversionAndLimiterFactor);

    // Tally up how many samples have passed since we were close to our max sample.
    if (maxSampleInBuffer < maxSample-limiterHeadroom)
        numFramesSinceMaxSample += frameCount;
    else
        numFramesSinceMaxSample = 0;

    // If maxSample is being limited (over 1.0), and we have not seen a mixed output sample near the max sample in
    // X frames, then start to reduce the limiter factor.
    if ((maxSample > 1.0f) && (numFramesSinceMaxSample >= limiterWindowInFrames))
    {
        maxSample -= limiterHeadroom;
        numFramesSinceMaxSample = 0;
    }

    audioOutputTimeInFrames += frameCount;

#ifdef ENABLE_PROFILER
    Baselib_Timer_Ticks end = Baselib_Timer_GetHighPrecisionTimerTicks();
    if (callbackTicksLastEnd != 0)
    {
        callbackCpuPercent[callbackCpuIndex] = (end - start) * 1000 / (end - callbackTicksLastEnd);
        callbackCpuIndex = (callbackCpuIndex + 1) % kCallbackCpuCount;
    }
    callbackTicksLastEnd = end;
#endif
}

#ifdef ENABLE_PROFILER
DOTS_EXPORT(float)
getCpuUsage()
{
    Baselib_Timer_Ticks total = 0;
    for (int i = 0; i < kCallbackCpuCount; i++)
        total += callbackCpuPercent[i];
    return (float)(total / kCallbackCpuCount) / 10.0f;
}

DOTS_EXPORT(int)
getRequiredMemory(uint32_t clipID)
{
    if (!audioInitialized) return 0;

    LOGE("getRequiredMemory(%d)", clipID);
    auto it = clipMap.find(clipID);
    if (it != clipMap.end()) {
        SoundClip* clip = it->second;
        return (int)(ma_get_bytes_per_frame(maConfig.playback.format, maConfig.playback.channels) * clip->numFrames());
    }

    LOGE("getRequiredMemory(%d) not found.", clipID);
    return 0;
}
#endif

DOTS_EXPORT(void)
initAudio() {
    if (!audioInitialized) {
        maConfig = ma_device_config_init(ma_device_type_playback);
        maConfig.playback.format = ma_format_s16;
        maConfig.playback.channels = 2;
        maConfig.sampleRate = 44100;
        maConfig.dataCallback = sendFramesToDevice;
        maConfig.pUserData = &userData;

        // must be aligned to whatever the struct is set to, as miniadio specifies an explicit alignment
        maDevice = (ma_device*)unsafeutility_malloc(sizeof(ma_device), alignof(ma_device), Allocator::Persistent);

        if (ma_device_init(NULL, &maConfig, maDevice) != MA_SUCCESS) {
            LOGE("Failed to init audio device.");
            return;
        }

        if (maConfig.playback.format != ma_format_s16) {
            LOGE("Failed to get signed-16 format.");
            return;
        }
        if (maConfig.playback.channels != 2) {
            LOGE("Failed to get stereo format.");
            return;
        }
        if (maConfig.sampleRate != 44100) {
            LOGE("Failed to get 44100 Hz.");
            return;
        }

        if (ma_device_start(maDevice) != MA_SUCCESS) {
            LOGE("Failed to start audio device.");
            return;
        }
    }

    if (mixBuffer == nullptr)
        mixBuffer = (float*)unsafeutility_malloc(mixBufferSize, 16, Allocator::Persistent);

    LOGE("initAudio() okay");
    audioInitialized = true;
}


DOTS_EXPORT(void)
destroyAudio() {
    freeAllSources();
    freeAllClips();
    if (audioInitialized) {
        ma_device_uninit(maDevice);
    }
    unsafeutility_free(maDevice, Allocator::Persistent);
    maDevice = 0;

    unsafeutility_free(mixBuffer, Allocator::Persistent);
    mixBuffer = nullptr;

    LOGE("destroyAudio() okay");
    audioInitialized = false;
}

DOTS_EXPORT(void)
reinitAudio()
{   
    LOGE("reinitAudio()");
    if (audioInitialized) {
        ma_device_uninit(maDevice);
    }
    unsafeutility_free(maDevice, Allocator::Persistent);
    maDevice = 0;
    audioInitialized = false;

    initAudio();
}

DOTS_EXPORT(uint32_t)
playSource(uint32_t clipID, float volume, float pan, bool loop)
{
    if (!audioInitialized) return 0;
    flushMemory();

    auto it = clipMap.find(clipID);
    if (it == clipMap.end()) {
        LOGE("playSource() clipID=%d failed.", clipID);
        return false;
    }

    SoundClip* clip = it->second;
    ASSERT(clip);

    SoundSource* source = new SoundSource(clip);

    source->setVolume(volume);
    source->setPan(pan);
    source->setLoop(loop);
    source->play();

    if (source->getStatus() == SoundSource::SoundStatus::Playing)
    {
        BaselibLock lock(soundSourceMutex);
        sourceMap[++sourceIDPool] = source;
        LOGE("SoundSource %d created", sourceIDPool);
        return sourceIDPool;
    }
    source->stop();
    delete source;
    return 0;
}


DOTS_EXPORT(bool)
isPlaying(uint32_t sourceID)
{
    if (!audioInitialized) return false;

    BaselibLock lock(soundSourceMutex);
    auto it = sourceMap.find(sourceID);
    if (it == sourceMap.end()) {
        // This isn't an error; the lifetime of an Audio object on the C#
        // side doesn't match the object here. If it's deleted, it just isn't playing.
        return false;
    }
    const SoundSource* source = it->second;
    return source->getStatus() == SoundSource::SoundStatus::NotYetStarted ||
        source->getStatus() == SoundSource::SoundStatus::Playing;
}


DOTS_EXPORT(bool)
stopSource(uint32_t sourceID)
{
    if (!audioInitialized) return false;

    BaselibLock lock(soundSourceMutex);
    auto it = sourceMap.find(sourceID);
    if (it == sourceMap.end()) {
        return false;
    }

    LOGE("stopSource() source=%d", sourceID);

    SoundSource* source = it->second;
    source->stop();
    LOGE("SoundSource %d deleted", sourceID);
    delete source;
    sourceMap.erase(it);
    return true;
}

