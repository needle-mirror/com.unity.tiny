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

static baselib::Lock soundSourcePropertyMutex;

// Need a mutex to protect access to the SoundSources that are used by the callback. The SoundClips are
// refCounted, so they are safe.
static baselib::Lock soundSourceSampleMutex;

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

DOTS_EXPORT(void)
soundSourcePropertyMutexLock()
{
    soundSourcePropertyMutex.Acquire();
}

DOTS_EXPORT(void)
soundSourcePropertyMutexUnlock()
{
    soundSourcePropertyMutex.Release();
}

DOTS_EXPORT(void)
soundSourceSampleMutexLock()
{
    soundSourceSampleMutex.Acquire();
}

DOTS_EXPORT(void)
soundSourceSampleMutexUnlock()
{
    soundSourceSampleMutex.Release();
}

void flushMemory()
{
    std::vector<std::map<uint32_t, SoundClip*>::iterator> clipDeleteList;
    std::vector<std::map<uint32_t, SoundSource*>::iterator> sourceDeleteList;

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
}

void freeAllSourcesAndClips()
{
    BaselibLock propertyMutex(soundSourcePropertyMutex);
    BaselibLock sampleMutex(soundSourceSampleMutex);

    for (auto it = sourceMap.begin(); it != sourceMap.end(); ++it)
    {
        SoundSource* source = it->second;
        source->stop();
    }

    for (auto it = clipMap.begin(); it != clipMap.end(); ++it)
    {
        SoundClip* clip = it->second;
        clip->queueDeletion();
    }

    flushMemory();

    sourceIDPool = 0;
    clipIDPool = 0;
}

DOTS_EXPORT(void)
freeAudio(uint32_t clipID)
{
    if (!audioInitialized) return;

    LOGE("freeAudio(%d)", clipID);

    soundSourcePropertyMutex.Acquire();
    auto it = clipMap.find(clipID);
    if (it != clipMap.end()) {
        SoundClip* clip = it->second;
        clip->queueDeletion();
    }
    else {
        LOGE("freeAudio(%d) not found.", clipID);
    }
    soundSourcePropertyMutex.Release();
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
startLoadFromDisk(const char* path)
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

DOTS_EXPORT(uint32_t)
startLoadFromMemory(void* compressedBuffer, int compressedBufferSize)
{
    if (!audioInitialized) return 0;

    ++clipIDPool;
    clipMap[clipIDPool] = new SoundClip(compressedBuffer, compressedBufferSize);
    return clipIDPool;
}

// Testing
DOTS_EXPORT(int32_t)
numSourcesAllocated()
{
    BaselibLock propertyMutex(soundSourcePropertyMutex);
    BaselibLock sampleMutex(soundSourceSampleMutex);

    flushMemory();    
    LOGE("numSourcesAllocated=%d", (int)sourceMap.size());
    return (int)sourceMap.size();
}

// Testing
DOTS_EXPORT(int32_t)
numClipsAllocated()
{
    BaselibLock propertyMutex(soundSourcePropertyMutex);
    BaselibLock sampleMutex(soundSourceSampleMutex);
    flushMemory();

    LOGE("numClipsAllocated=%d", (int)clipMap.size());
    return (int)clipMap.size();
}

// Testing
DOTS_EXPORT(int32_t)
sourcePoolID()
{
    BaselibLock propertyMutex(soundSourcePropertyMutex);
    BaselibLock sampleMutex(soundSourceSampleMutex);
    flushMemory();

    LOGE("sourcePoolID=%d", (int)sourcePoolID);
    return sourceIDPool;
}

DOTS_EXPORT(int32_t)
clipPoolID()
{
    BaselibLock propertyMutex(soundSourcePropertyMutex);
    BaselibLock sampleMutex(soundSourceSampleMutex);
    flushMemory();

    LOGE("clipPoolID=%d", (int)clipPoolID);
    return clipIDPool;
}

DOTS_EXPORT(int)
checkLoading(uint32_t id)
{
    if (!audioInitialized) return SoundClip::SoundClipStatus::FAIL;

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

DOTS_EXPORT(void)
setVolume(uint32_t sourceID, float volume)
{
    if (!audioInitialized) return;

    auto it = sourceMap.find(sourceID);
    if (it == sourceMap.end()) {
        LOGE("setVolume() sourceID=%d failed.", sourceID);
    }
    else {
        it->second->setVolume(volume);
    }
}

DOTS_EXPORT(void)
setPan(uint32_t sourceID, float pan)
{
    if (!audioInitialized) return;

    auto it = sourceMap.find(sourceID);
    if (it == sourceMap.end()) {
        LOGE("setPan() sourceID=%d failed.", sourceID);
    }
    else {
        it->second->setPan(pan);
    }
}

DOTS_EXPORT(void)
setPitch(uint32_t sourceID, float pitch)
{
    if (!audioInitialized) return;

    auto it = sourceMap.find(sourceID);
    if (it == sourceMap.end()) {
        LOGE("setPitch() sourceID=%d failed.", sourceID);
    }
    else {
        it->second->setPitch(pitch);
    }
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

struct SoundSourcePlaying
{
    SoundSource* source;
    float coeffL;
    float coeffR;
    float pitch;
};

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
    const int soundSourcesPlayingMax = 128;
    SoundSourcePlaying soundSourcesPlaying[soundSourcesPlayingMax];
    int numSoundSourcesPlaying = 0;

    ASSERT(bytesPerSample == 2);
    ASSERT(bytesPerFrame == 4);
    ASSERT(mixBufferSize >= frameCount*2*sizeof(float));

    if (audioPaused)
        return;

    if ((mixBuffer == nullptr) || (mixBufferSize < frameCount*2*sizeof(float)))
        return;

    soundSourcePropertyMutex.Acquire();
    int sourceIndex = 0;
    for (auto it = sourceMap.begin(); it != sourceMap.end(); ++it) 
    {
        SoundSource* source = it->second;
        if (source->isPlaying())
        {
            float volume = audioMuted ? 0.0f : source->volume();
            float pan = source->pan();
            float pitch = source->pitch();

            // when pan is at center, setting both channels to .7 instead of .5 sounds more natural
            // this is an approximation to sqrt(2) = 45 degree angle on unit circle, and for now
            // we'll linearly interpolate to the extremes rather than rotate
            soundSourcesPlaying[sourceIndex].source = source;
            soundSourcesPlaying[sourceIndex].coeffL = (.7f - (pan > 0 ? pan * .7f : pan * .3f)) * volume;
            soundSourcesPlaying[sourceIndex].coeffR = (.7f + (pan < 0 ? pan * .7f : pan * .3f)) * volume;
            soundSourcesPlaying[sourceIndex].pitch = pitch;
            
            sourceIndex++;
            if (sourceIndex >= soundSourcesPlayingMax)
                break;
        }
    }
    numSoundSourcesPlaying = sourceIndex;
    soundSourcePropertyMutex.Release();

    for (uint32_t i = 0; i < frameCount*2; i++)
        mixBuffer[i] = 0.0f;

    soundSourceSampleMutex.Acquire();
    for (int i = 0; i < numSoundSourcesPlaying; i++) 
    {
        SoundSource* source = soundSourcesPlaying[i].source;
        bool done = false;
        uint32_t totalFrames = 0;
        float* target = mixBuffer;
        float coeffL = soundSourcesPlaying[i].coeffL;
        float coeffR = soundSourcesPlaying[i].coeffR;
        float pitch = soundSourcesPlaying[i].pitch;

        int numFailedFetches = 0;
        while (!done)
        {
            uint32_t decodedFrames = 0;
            uint32_t requestedFrames = frameCount - totalFrames;

            const float* src = source->fetch(requestedFrames, &decodedFrames, pitch);
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

            if (decodedFrames == 0)
                numFailedFetches++;
            else
                numFailedFetches = 0;

            done = true;
            if (source->loop() && (totalFrames < frameCount) && (numFailedFetches < 2)) 
            {
                source->rewind();
                done = false;
            }
        }
    }
    soundSourceSampleMutex.Release();

    // Find the maximum sample in this buffer.
    float maxSampleInBuffer = 1.0f;
    for (uint32_t i = 0; i < frameCount*2; i++)
    {
        maxSampleInBuffer = mixBuffer[i] > maxSampleInBuffer ? mixBuffer[i] : maxSampleInBuffer;
        maxSampleInBuffer = mixBuffer[i] < -1.0f*maxSampleInBuffer ? -1.0f*mixBuffer[i] : maxSampleInBuffer;
    }

    // Check if we need to increase our global max sample, based on the values in our most recent buffer.
    maxSample = maxSampleInBuffer > maxSample ? maxSampleInBuffer : maxSample;

    // Apply our float-to-short conversion and limiter factors together.
    float conversionAndLimiterFactor = maxSample > 1.0f ? SHRT_MAX_FLOAT/maxSample : SHRT_MAX_FLOAT;
    int16_t* pSamplesShort = (int16_t*)pSamples;
    for (uint32_t i = 0; i < frameCount*2; i++)
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

    soundSourcePropertyMutex.Acquire();
    soundSourceSampleMutex.Acquire();
    flushMemory();
    soundSourceSampleMutex.Release();
    soundSourcePropertyMutex.Release();

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
#endif

DOTS_EXPORT(uint32_t)
getUncompressedMemorySize(uint32_t clipID)
{
    if (!audioInitialized) return 0;

    LOGE("getUncompressedMemorySize(%d)", clipID);
    auto it = clipMap.find(clipID);
    if (it != clipMap.end()) {
        SoundClip* clip = it->second;
        return (uint32_t)(ma_get_bytes_per_frame(maConfig.playback.format, maConfig.playback.channels) * clip->numFrames());
    }

    LOGE("getUncompressedMemorySize(%d) not found.", clipID);
    return 0;
}

DOTS_EXPORT(uint32_t)
getCompressedMemorySize(uint32_t clipID)
{
    if (!audioInitialized) return 0;

    LOGE("getCompressedMemorySize(%d)", clipID);
    auto it = clipMap.find(clipID);
    if (it != clipMap.end()) {
        SoundClip* clip = it->second;
        return (uint32_t)clip->getCompressedMemorySize();
    }

    LOGE("getCompressedMemorySize(%d) not found.", clipID);
    return 0;
}

DOTS_EXPORT(int16_t*)
getUncompressedMemory(uint32_t clipID)
{
    if (!audioInitialized) return 0;

    LOGE("getUncompressedMemory(%d)", clipID);
    auto it = clipMap.find(clipID);
    if (it != clipMap.end()) {
        SoundClip* clip = it->second;
        return (int16_t*)clip->frames();
    }

    LOGE("getUncompressedMemory(%d) not found.", clipID);
    return nullptr;
}

DOTS_EXPORT(void)
setUncompressedMemory(uint32_t clipID, int16_t* uncompressedMemory, uint32_t uncompressedSizeFrames)
{
    if (!audioInitialized) return;

    LOGE("setUncompressedMemory(%d)", clipID);
    auto it = clipMap.find(clipID);
    if (it != clipMap.end()) {
        SoundClip* clip = it->second;
        clip->setFrames(uncompressedMemory, uncompressedSizeFrames);
    }
    else {
        LOGE("setUncompressedMemory(%d) not found.", clipID);
    }
}

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
    freeAllSourcesAndClips();

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
playSource(uint32_t clipID, float volume, float pan, int loop)
{
    if (!audioInitialized) return 0;

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
        sourceMap[++sourceIDPool] = source;
        LOGE("SoundSource %d created", sourceIDPool);
        return sourceIDPool;
    }
    source->stop();
    return 0;
}

DOTS_EXPORT(int)
isPlaying(uint32_t sourceID)
{
    if (!audioInitialized) return 0;

    auto it = sourceMap.find(sourceID);
    if (it == sourceMap.end()) {
        // This isn't an error; the lifetime of an Audio object on the C#
        // side doesn't match the object here. If it's deleted, it just isn't playing.
        return 0;
    }
    const SoundSource* source = it->second;
    return (source->getStatus() == SoundSource::SoundStatus::NotYetStarted ||
        source->getStatus() == SoundSource::SoundStatus::Playing) ? 1 : 0;
}

DOTS_EXPORT(int)
stopSource(uint32_t sourceID)
{
    if (!audioInitialized) return 0;

    auto it = sourceMap.find(sourceID);
    if (it == sourceMap.end()) {
        return 0;
    }

    LOGE("stopSource() source=%d", sourceID);

    SoundSource* source = it->second;
    source->stop();
    return 1;
}
