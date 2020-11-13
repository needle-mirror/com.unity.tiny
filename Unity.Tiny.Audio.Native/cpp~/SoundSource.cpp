#include "SoundSource.h"
#include "NativeAudio.h"

#include <allocators.h>
#include <limits.h>
#include <string.h>

using namespace Unity::LowLevel;

#define CHECK_CLIP m_clip->addRef(); m_clip->releaseRef();

SoundSource::SoundSource(SoundClip* clip) :
    m_sampleBuffer(nullptr),
    m_sampleBufferSize(0),
    m_decoderInitialized(false)
{
    m_clip = clip;
    m_clip->addRef();

    m_uncompressedBufferFramePosStart = -1;
    m_uncompressedBufferSize = 1024*2*sizeof(uint16_t);
    m_uncompressedBuffer = (int16_t*)unsafeutility_malloc(m_uncompressedBufferSize, 16, Allocator::Persistent);

    m_sampleBufferSize = 1024*2*sizeof(float);
    m_sampleBuffer = (float*)unsafeutility_malloc(m_sampleBufferSize, 16, Allocator::Persistent);
    LOGE("SoundSource() %s", m_clip->FileName().c_str());
}

SoundSource::~SoundSource()
{
    LOGE("~SoundSource() %s", m_clip->FileName().c_str());
    m_clip->releaseRef();
    
    unsafeutility_free(m_uncompressedBuffer, Allocator::Persistent);
    unsafeutility_free(m_sampleBuffer, Allocator::Persistent);
}

void SoundSource::play()
{
    CHECK_CLIP
    if (m_status == NotYetStarted || m_status == Stopped) 
    {
        m_framePos = 0;
        m_framePosResample = 0.0;
        m_status = Playing;
    }
}

void SoundSource::stop()
{
    CHECK_CLIP
    m_status = Stopped;
}

const int16_t* SoundSource::updateFrames(uint32_t frameCount, uint32_t* delivered, int32_t framePos)
{
    const float SHRT_MAX_FLOAT = (float)SHRT_MAX;

    if (!m_decoderInitialized && m_clip && m_clip->getCompressedMemory())
    {
        ma_decoder_config config;
        memset(&config, 0, sizeof(config));
        config.format = ma_format_s16;
        config.channels = 2;
        config.sampleRate = 44100;
      
        ma_decode_memory_init(m_clip->getCompressedMemory(), m_clip->getCompressedMemorySize(), &config, &m_decoder, &m_config);

        m_decoderInitialized = true;
        m_uncompressedBufferFramePosStart = -1;
    }

    uint32_t previousFrameStart = 0;
    uint32_t previousFrameCount = 0;
    uint32_t uncompressedBufferSizeInSamples = m_uncompressedBufferSize / sizeof(int16_t);
    uint32_t uncompressedBufferSizeInFrames = uncompressedBufferSizeInSamples / 2;
    uint32_t uncompressedBufferFramePosEnd = m_uncompressedBufferFramePosStart + uncompressedBufferSizeInFrames;

    // If frames before our current position were requested (via framePos), then copy those frames to the front of the buffer.
    if ((framePos >= 0) && (framePos >= (int32_t)m_uncompressedBufferFramePosStart) && (framePos < (int32_t)uncompressedBufferFramePosEnd))
    {
        previousFrameStart = framePos - m_uncompressedBufferFramePosStart;
        previousFrameCount = uncompressedBufferFramePosEnd - framePos;

        for (uint32_t i = 0; i < previousFrameCount*2; i++)
            m_uncompressedBuffer[i] = m_uncompressedBuffer[previousFrameStart*2+i];

        frameCount -= previousFrameCount;
    }

    ma_uint64 frameCountOut = 0;
    short* pPCMFramesOut = nullptr;
    ma_decode_memory_frame(&m_decoder, &m_config, frameCount, &frameCountOut, (void**)&pPCMFramesOut);
    
    for (int i = 0; i < frameCountOut*2; i++)
        m_uncompressedBuffer[previousFrameCount*2+i] = (int16_t)pPCMFramesOut[i];

    *delivered = previousFrameCount + (uint32_t)frameCountOut;
    ma_free(pPCMFramesOut);

    if (m_uncompressedBufferFramePosStart < 0)
        m_uncompressedBufferFramePosStart = 0;
    else
        m_uncompressedBufferFramePosStart += (uint32_t)frameCountOut;

    return m_uncompressedBuffer;
}

const float* SoundSource::fetch(uint32_t frameCount, uint32_t* delivered, float pitch)
{
    CHECK_CLIP
    uint64_t read = 0;
    uint32_t write = 0;
    const float SHRT_MAX_FLOAT = (float)SHRT_MAX;

    *delivered = 0;

    if (frameCount > m_sampleBufferSize/(2*sizeof(float)))
        frameCount = m_sampleBufferSize/(2*sizeof(float));

    if (m_status != Playing)
        return nullptr;

    if ((m_framePosResample != (double)m_framePos) || (pitch != 1.0f))
        return fetchAndResample(frameCount, delivered, pitch);

    if (m_clip->frames() == nullptr)
    {
        const int16_t* uncompressedFrames = updateFrames(frameCount, delivered);
        for (uint32_t i = 0; i < *delivered*2; i++)
            m_sampleBuffer[i] = (float)uncompressedFrames[i] / SHRT_MAX_FLOAT;

        m_framePos += *delivered;
        m_framePosResample = (double)m_framePos;

        if ((*delivered < frameCount) && !loop()) 
            m_status = Stopped;

        return m_sampleBuffer;
    }

    bool done = false;
    while (!done)
    {
        uint64_t framesRemaining = (uint64_t)((m_clip->numFrames() - m_framePosResample) / pitch);
        const int16_t* src = m_clip->frames() + m_framePos * 2;

        if (frameCount <= framesRemaining) 
        {
            m_framePos += frameCount;
            read = frameCount;
        }
        else 
        {
            m_framePos += framesRemaining;
            read = framesRemaining;
        }

        m_framePosResample = (double)m_framePos;

        if (m_framePos >= m_clip->numFrames())
        {
            rewind();
            if (!m_loop)
            {
                m_status = Stopped;
                done = true;
            }
        }

        *delivered += (uint32_t)read;

        for (int i = 0; i < read*2; i++)
            m_sampleBuffer[write++] = (float)(src[i]) / SHRT_MAX_FLOAT;

        if (*delivered >= frameCount)
            done = true;
    }

    return m_sampleBuffer;
}

const float* SoundSource::fetchAndResample(uint32_t frameCount, uint32_t* delivered, float pitch)
{
    CHECK_CLIP
    const float SHRT_MAX_FLOAT = (float)SHRT_MAX;
    uint32_t numFrames = (uint32_t)(m_clip->numFrames());
    const int16_t* samples = m_clip->frames();
    double framePosResample = m_framePosResample;

    // If the clip is compressed-in-memory, the uncompressed samples do not yet exist in memory and
    // samples will be null. In this case, decompress the samples we need now.
    if (samples == nullptr)
    {
        uint32_t uncompressedFramesRequested = (uint32_t)((float)frameCount * pitch + 3.0f);
        uint32_t uncompressedFramesDelivered = 0;
        uint64_t framePosResampleWhole = (uint64_t)m_framePosResample;
        double framePosResampleFraction = m_framePosResample - (double)framePosResampleWhole;

        if (framePosResampleWhole >= 1)
            framePosResampleWhole--;

        framePosResample = 1 + framePosResampleFraction;

        samples = updateFrames(uncompressedFramesRequested, &uncompressedFramesDelivered, (int32_t)framePosResampleWhole);
        numFrames = uncompressedFramesDelivered;
    }
    
    uint32_t resampledFramesAvailable = (uint32_t)((numFrames - framePosResample) / pitch);
    uint32_t resampledFrameCount = (frameCount <= resampledFramesAvailable) ? frameCount : resampledFramesAvailable;

    for (uint32_t i = 0; i < resampledFrameCount; i++)
    {            
        uint32_t framePosResample1 = (uint32_t)framePosResample;
        uint32_t framePosResample0 = (framePosResample1 == 0) ? numFrames-1 : framePosResample1-1;
        uint32_t framePosResample2 = framePosResample1+1 >= numFrames ? framePosResample1+1-numFrames : framePosResample1+1;
        uint32_t framePosResample3 = framePosResample1+2 >= numFrames ? framePosResample1+2-numFrames : framePosResample1+2;

        for (uint32_t iSample = 0; iSample < 2; iSample++)
        {
            uint32_t samplePosTemp0 = framePosResample0*2 + iSample;
            uint32_t samplePosTemp1 = framePosResample1*2 + iSample;
            uint32_t samplePosTemp2 = framePosResample2*2 + iSample;
            uint32_t samplePosTemp3 = framePosResample3*2 + iSample;

            float mu = (float)(framePosResample - (double)framePosResample1);
            
            float y0 = (float)(samples[samplePosTemp0])/SHRT_MAX_FLOAT;
            float y1 = (float)(samples[samplePosTemp1])/SHRT_MAX_FLOAT;
            float y2 = (!m_loop && (samplePosTemp2 < samplePosTemp1)) ? 0.0f : (float)(samples[samplePosTemp2])/SHRT_MAX_FLOAT;
            float y3 = (!m_loop && (samplePosTemp3 < samplePosTemp1)) ? 0.0f : (float)(samples[samplePosTemp3])/SHRT_MAX_FLOAT;
            
            // Cubic hermite interpolation.
            float c0 = y1;
            float c1 = 0.5f * (y2 - y0);
            float c2 = y0 - (2.5f * y1) + (2.0f * y2) - (0.5f * y3);
            float c3 = (0.5f * (y3 - y0)) + (1.5f * (y1 - y2));
            float interpolatedValue = (((((c3 * mu) + c2) * mu) + c1) * mu) + c0;

            m_sampleBuffer[2*i + iSample] = interpolatedValue;
        }

        m_framePosResample += pitch;
        framePosResample += pitch;
    }    

    m_framePos = (uint64_t)m_framePosResample;

    if ((frameCount >= resampledFramesAvailable) && !loop()) 
        m_status = Stopped;

    *delivered = resampledFrameCount;
    return m_sampleBuffer;
}
