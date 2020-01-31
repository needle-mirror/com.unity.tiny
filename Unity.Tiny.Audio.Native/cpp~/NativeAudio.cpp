#include "NativeAudio.h"
#include "SoundClip.h"
#include "SoundSource.h"
#include <allocators.h>
#include <baselibext.h>

// Using baselib for now since we need realloc and we currently don't support it in Unity::LowLevel
#include <Baselib.h>
#include <C/Baselib_Memory.h>

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

static void FreeWrapped(void* p)
{
    if (!p)
        return;
    Baselib_Memory_AlignedFree(p);
}

#define MA_MALLOC(sz)           Baselib_Memory_AlignedAllocate(sz,16)
#define MA_REALLOC(p, sz)       Baselib_Memory_AlignedReallocate(p,sz,16)
#define MA_FREE(p)              FreeWrapped(p)
#define MINIAUDIO_IMPLEMENTATION
#include "./miniaudio/miniaudio.h"

using namespace Unity::LowLevel;

// #define AUDIO_TIMER

// Need a mutex to protect access to the SoundSources 
// that are used by the callback. The SoundClips are
// refCounted; so they are safe.
baselib::Lock soundSourceMutex;

uint32_t clipIDPool = 0;
std::map<uint32_t, SoundClip*> clipMap;
uint32_t sourceIDPool = 0;
std::map<uint32_t, SoundSource*> sourceMap;

ma_device_config maConfig;
ma_device* maDevice;
struct UserData
{
    void* dummy;
};
UserData userData;
bool audioInitialized = false;
bool audioPaused = false;

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
}

void freeAllClips()
{
    for (auto it = clipMap.begin(); it != clipMap.end(); ++it) {
        SoundClip* clip = it->second;
        clip->queueDeletion();
    }
    flushMemory();
    assert(clipMap.empty());
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

#ifdef AUDIO_TIMER
uint32_t callbackLongest = 0,
         callbackShortest = 0xffffffff,
         callbackTotal = 0,
         nCallback = 0,
         headerLongest = 0,
         headerShortest = 0xffffffff,
         headerTotal = 0,
         nHeader = 0;
#endif

// At 44,100 hz, stereo, 16-bit
// 44100 frames / second.
// Typical callback = 223 frames
// ~0.005 seconds = 5ms = 5000 microseconds of data
void sendFramesToDevice(ma_device* pDevice, void* pSamples, const void* pInput, ma_uint32 frameCount)
{
#ifdef AUDIO_TIMER
    auto start = baselib::high_precision_clock::now();
#endif

    const uint32_t bytesPerSample = ma_get_bytes_per_sample(pDevice->playback.format);
    const uint32_t bytpesPerFrame = ma_get_bytes_per_frame(pDevice->playback.format, pDevice->playback.channels);
    ASSERT(bytesPerSample == 2);
    ASSERT(bytpesPerFrame == 4);

    uint32_t nSamples = frameCount * 2; // Stereo
    static const int MAX_CHANNELS = 16; // Caps the performance

    memset(pSamples, 0, bytpesPerFrame * frameCount);
    if (audioPaused) {
        return;
    }

    UserData* pUser = (UserData*)(pDevice->pUserData);

    BaselibLock lock(soundSourceMutex);
#ifdef AUDIO_TIMER
    auto start = baselib::high_precision_clock::now();
#endif

    int count = 0;
    for (auto it = sourceMap.begin(); it != sourceMap.end() && count < MAX_CHANNELS; ++it) {
        SoundSource* source = it->second;
        if (source->isPlaying() && source->volume()) {
            bool done = false;
            ++count;

            uint32_t totalFrames = 0;
            int16_t* target = (int16_t*)pSamples;

            // when pan is at center, setting both channels to .7 instead of .5 sounds more natural
            // this is an approximation to sqrt(2) = 45 degree angle on unit circle, and for now
            // we'll linearly interpolate to the extremes rather than rotate
            float volume = source->volume() * 1024.0f;
            float pan = source->pan();
            int32_t coeffL = int32_t((.7f - (pan > 0 ? pan * .7f : pan * .3f))* volume);
            int32_t coeffR = int32_t((.7f + (pan < 0 ? pan * .7f : pan * .3f)) * volume);

            while (!done) {
                uint32_t decodedFrames = 0;
                const int16_t* src = source->fetch(frameCount - totalFrames, &decodedFrames);
                totalFrames += decodedFrames;

                // Now 'buffer' is the source. Apply the volume and copy to 'pSamples'
                for (uint32_t i = 0; i < decodedFrames; ++i) {
                    int32_t val = *target + *src * coeffL / 1024;
                    if (val < SHRT_MIN) val = SHRT_MIN;
                    if (val > SHRT_MAX) val = SHRT_MAX;
                    *target = val;
                    ++target;
                    ++src;

                    val = *target + *src * coeffR / 1024;
                    if (val < SHRT_MIN) val = SHRT_MIN;
                    if (val > SHRT_MAX) val = SHRT_MAX;
                    *target = val;
                    ++target;
                    ++src;
                }

                done = true;
                if (source->loop() && totalFrames < frameCount) {
                    done = false;
                    source->rewind();
                }
            }
        }
    }
#ifdef AUDIO_TIMER
    auto start = baselib::high_precision_clock::now();

    uint32_t tMicros = (uint32_t)std::chrono::duration_cast<std::chrono::microseconds>(end - start).count();
    if (tMicros < callbackShortest) callbackShortest = tMicros;
    if (tMicros > callbackLongest) callbackLongest = tMicros;
    nCallback++;
    callbackTotal += tMicros;

    uint32_t tHeader = (uint32_t)std::chrono::duration_cast<std::chrono::microseconds>(header - start).count();
    if (tHeader < headerShortest) headerShortest = tHeader;
    if (tHeader > headerLongest) headerLongest = tHeader;
    nHeader++;
    headerTotal += tHeader;

    if (nCallback == 256) {
        printf("Callback: shortest=%d longest=%d ave=%d framecount=%d\n", callbackShortest, callbackLongest, callbackTotal / nCallback, frameCount);
        printf("  Header: shortest=%d longest=%d ave=%d\n", headerShortest, headerLongest, headerTotal / nHeader);
        fflush(stdout);
        callbackLongest = 0;
        callbackShortest = 0xffffffff;
        callbackTotal = 0;
        nCallback = 0;
        headerLongest = 0;
        headerShortest = 0xffffffff;
        headerTotal = 0;
        nHeader = 0;
    }
#endif

    // Always returns a full buffer written, since we start the buffer with silence.
    return;
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
    LOGE("destroyAudio() okay");
    audioInitialized = false;
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

