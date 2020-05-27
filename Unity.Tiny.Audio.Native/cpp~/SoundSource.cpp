#include "SoundSource.h"
#include "NativeAudio.h"

#include <allocators.h>
#include <limits.h>
#include <string.h>

using namespace Unity::LowLevel;

#define CHECK_CLIP m_clip->addRef(); m_clip->releaseRef();

SoundSource::SoundSource(SoundClip* clip) :
    m_sampleBuffer(nullptr),
    m_sampleBufferSize(0)
{
    m_clip = clip;
    m_clip->addRef();

    m_sampleBufferSize = 1024*2*sizeof(float);
    m_sampleBuffer = (float*)unsafeutility_malloc(m_sampleBufferSize, 16, Allocator::Persistent);
    LOGE("SoundSource() %s", m_clip->FileName().c_str());
}


SoundSource::~SoundSource()
{
    LOGE("~SoundSource() %s", m_clip->FileName().c_str());
    m_clip->releaseRef();
    
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

const float* SoundSource::fetch(uint32_t frameCount, uint32_t* delivered)
{
    CHECK_CLIP
    uint64_t read = 0;
    uint32_t write = 0;
    const float SHRT_MAX_FLOAT = (float)SHRT_MAX;

    if (frameCount > m_sampleBufferSize/(2*sizeof(float)))
        frameCount = m_sampleBufferSize/(2*sizeof(float));

    if ((m_status != Playing) || !m_clip->okay()) 
    {
        *delivered = 0;
        return nullptr;
    }

    if ((m_framePosResample != (double)m_framePos) || (m_pitch != 1.0f))
        return fetchAndResample(frameCount, delivered);

    uint64_t framesRemaining = (uint64_t)((m_clip->numFrames() - m_framePosResample) / m_pitch);
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

    if (m_framePos == m_clip->numFrames() && !loop()) 
        m_status = Stopped;

    *delivered = (uint32_t)read;

    for (int i = 0; i < read*2; i++)
        m_sampleBuffer[write++] = (float)(src[i]) / SHRT_MAX_FLOAT;

    return m_sampleBuffer;
}

const float* SoundSource::fetchAndResample(uint32_t frameCount, uint32_t* delivered)
{
    CHECK_CLIP
    const float SHRT_MAX_FLOAT = (float)SHRT_MAX;
    uint64_t numFrames = m_clip->numFrames();
    const int16_t* samples = m_clip->frames();

    uint32_t resampledFramesAvailable = 0;
    if ((m_framePosResample + m_pitch >= numFrames) && loop())
        resampledFramesAvailable = (uint32_t)(numFrames / m_pitch);
    else
        resampledFramesAvailable = (uint32_t)((numFrames - m_framePosResample) / m_pitch);

    uint32_t resampledFrameCount = (frameCount <= resampledFramesAvailable) ? frameCount : resampledFramesAvailable;
    for (int i = 0; i < resampledFrameCount; i++)
    {            
        uint64_t framePosResample1 = (uint64_t)m_framePosResample;
        uint64_t framePosResample0 = (framePosResample1 == 0) ? numFrames-1 : framePosResample1-1;
        uint64_t framePosResample2 = framePosResample1+1 >= numFrames ? framePosResample1+1-numFrames : framePosResample1+1;
        uint64_t framePosResample3 = framePosResample1+2 >= numFrames ? framePosResample1+2-numFrames : framePosResample1+2;

        for (int iSample = 0; iSample < 2; iSample++)
        {
            uint32_t samplePosTemp0 = framePosResample0*2 + iSample;
            uint32_t samplePosTemp1 = framePosResample1*2 + iSample;
            uint32_t samplePosTemp2 = framePosResample2*2 + iSample;
            uint32_t samplePosTemp3 = framePosResample3*2 + iSample;

            float mu = (float)(m_framePosResample - (double)framePosResample1);
            
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

        m_framePosResample += m_pitch;
        if ((m_framePosResample >= numFrames) && loop())
            m_framePosResample -= (double)numFrames;
    }    

    m_framePos = (uint64_t)m_framePosResample;

    if ((frameCount >= resampledFramesAvailable) && !loop()) 
        m_status = Stopped;

    *delivered = resampledFrameCount;
    return m_sampleBuffer;
}

