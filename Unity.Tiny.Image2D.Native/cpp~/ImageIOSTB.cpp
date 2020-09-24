#include <stdlib.h>
#include <stdint.h>
#include <allocators.h>
#include <stdio.h>

#define STBI_MALLOC(sz)           unsafeutility_malloc(sz,16,Unity::LowLevel::Allocator::Persistent)
#define STBI_REALLOC(p,newsz)     unsafeutility_realloc(p,newsz,16,Unity::LowLevel::Allocator::Persistent)
#define STBI_FREE(p)              unsafeutility_free(p,Unity::LowLevel::Allocator::Persistent)
#define STBI_REALLOC_SIZED(p,oldsz,newsz) STBI_REALLOC(p,newsz)
#define STBIW_MALLOC(sz)          STBI_MALLOC(sz)
#define STBIW_REALLOC(p,newsz)    STBI_REALLOC(p,newsz)
#define STBIW_FREE(p)             STBI_FREE(p)
#define STBIW_REALLOC_SIZED(p,oldsz,newsz) STBI_REALLOC_SIZED(p,oldsz,newsz)

#define STB_IMAGE_IMPLEMENTATION
#include "libstb/stb_image.h"

#define STB_IMAGE_WRITE_IMPLEMENTATION
#include "libstb/stb_image_write.h"

#include "Base64.h"
#include "ThreadPool.h"
#include "Image2DHelpers.h"

#include <Unity/Runtime.h>

#include "src/webp/decode.h"

using namespace ut;
using namespace ut::ThreadPool;

// keep this in sync with C#
class ImageSTB {
public:
    ImageSTB() {
        w = 0;
        h = 0;
        pixels = 0;
    }

    ImageSTB(int _w, int _h) {
        w = _w;
        h = _h;
        pixels = (uint32_t*)STBI_MALLOC(w*h*sizeof(uint32_t));
    }

    ~ImageSTB() {
        Free();
    }

    void Free() {
        STBI_FREE(pixels);
        pixels = 0;
    }

    ImageSTB(ImageSTB&& other) {
        pixels = other.pixels;
        w = other.w;
        h = other.h;
        other.pixels = 0;
    }

    ImageSTB& operator=(ImageSTB&& other) {
        if ( this == &other ) return *this;
        STBI_FREE(pixels);
        pixels = other.pixels;
        w = other.w;
        h = other.h;
        other.pixels = 0;
        return *this;
    }

    void Set(uint32_t *_pixels, int _w, int _h) {
        STBI_FREE(pixels);
        pixels = _pixels;
        w = _w;
        h = _h;
    }

    int w, h;
    uint32_t *pixels;
};

static std::vector<ImageSTB*> allImages(1); // by handle, reserve handle 0

#if defined(UNITY_ANDROID)
extern "C" void* loadAsset(const char *path, int *size, void* (*alloc)(size_t));
#endif

//Loads a file and returns the loaded data
static uint8_t* LoadFile(const char* file_name, size_t* data_size) {
    int ok;
    FILE* in = stbi__fopen(file_name, "rb");
    if (in == NULL) {
        printf("Failed to open input image file '%s'\n", file_name);
        return NULL;
    }
    fseek(in, 0, SEEK_END);
    size_t file_size = ftell(in);
    fseek(in, 0, SEEK_SET);
    uint8_t* file_data = (uint8_t*)malloc(file_size);
    if (file_data == NULL) {
        fclose(in);
        return NULL;
    }
    ok = (fread(file_data, file_size, 1, in) == 1);
    fclose(in);

    if (!ok) {
        free(file_data);
        return NULL;
    }
    *data_size = file_size;
    return file_data;
}

//Load/Read a potential webp compressed image file and try to decode it to RGBA
//TODO: to move to a C# non stb only module
static uint32_t* LoadWebpImage(uint8_t* data, int size_data, int *width, int *height)
{
    //Init webp decoder
    WebPDecoderConfig config;
    if (WebPInitDecoderConfig(&config) != 1)
        return NULL;

    //Retrieve webp feature such as image width/height
    if (WebPGetFeatures(data, size_data, &config.input) != VP8_STATUS_OK)
        return NULL;

    //We support only 32 bits images for now
    config.output.colorspace = WEBP_CSP_MODE::MODE_RGBA;

    //Finally decode the image
    if (WebPDecode(data, size_data, &config) != VP8_STATUS_OK)
        return NULL;

    *width = config.output.width;
    *height = config.output.height;
    uint32_t* pixels = (uint32_t*)STBI_MALLOC((*width) * (*height) * sizeof(uint32_t));

    //Copy the output to pixels
    memcpy(pixels, config.output.u.RGBA.rgba, (*width) * (*height) * sizeof(uint32_t));

    //And release the webp output
    WebPFreeDecBuffer(&config.output);

    return pixels;
}

static bool
LoadImageFromFile(const char* fn, size_t fnlen, ImageSTB& colorImg)
{
    int bpp = 0;
    int w = 0, h = 0;
    uint32_t* pixels = 0;
    // first try if it is a data uri
    std::string mediatype;
    std::vector<uint8_t> dataurimem;
    if (DecodeDataURIBase64(dataurimem, mediatype, fn, fnlen)) // try loading as data uri (ignore media type)
        pixels = (uint32_t*)stbi_load_from_memory(dataurimem.data(), (int)dataurimem.size(), &w, &h, &bpp, 4);
#if defined(UNITY_ANDROID)
    int size;
    void *data = loadAsset(fn, &size, malloc);
    pixels = (uint32_t*)stbi_load_from_memory((uint8_t*)data, size, &w, &h, &bpp, 4);
    if (!pixels)
        pixels = LoadWebpImage((uint8_t*)data, size, &w, &h);
    free(data);
#endif
    if (!pixels) // try loading as file (supported STB image file)
        pixels = (uint32_t*)stbi_load(fn, &w, &h, &bpp, 4);
    if (!pixels) // try loading as webp image file
    {
        //Read image file
        size_t size;
        uint8_t* data = LoadFile(fn, &size);
        pixels = LoadWebpImage(data, (int) size, &w, &h);
        free(data);
    }
    if (!pixels)
        return false;
    colorImg.Set(pixels, w, h);
    return true;
}

static bool
LoadSTBImageOnly(ImageSTB& colorImg, const char *imageFile, const char *maskFile)
{
    bool hasColorFile = imageFile && imageFile[0];
    bool hasMaskFile = maskFile && maskFile[0];

    if (!hasMaskFile && !hasColorFile)
        return false;

    if (hasColorFile && strcmp(imageFile,"::white1x1")==0 ) { // special case 1x1 image
        uint32_t* pixel1x1 = (uint32_t*)STBI_MALLOC(1 * 1 * sizeof(uint32_t));
        colorImg.Set(pixel1x1, 1, 1);
        colorImg.pixels[0] = ~0;
        return true;
    }

    // color from file first
    ImageSTB maskImg;
    if (hasColorFile) {
        if (!LoadImageFromFile(imageFile, strlen(imageFile), colorImg))
            return false;
    }
    // mask from file
    if (hasMaskFile) {
        if (!LoadImageFromFile(maskFile, strlen(maskFile), maskImg))
            return false;
        if (hasColorFile && (colorImg.w != maskImg.w || colorImg.h != maskImg.h))
            return false;
    }

    if (hasMaskFile && hasColorFile) { // merge mask into color if we have both
        // copy alpha from maskImg
        uint32_t* cbits = colorImg.pixels;
        uint32_t* mbits = maskImg.pixels;
        uint32_t npix = colorImg.w * colorImg.h;
        for (uint32_t i = 0; i < npix; i++) {
            uint32_t c = cbits[i] & 0x00ffffff;
            uint32_t m = (mbits[i] << 24) & 0xff000000;
            cbits[i] = c | m;
        }
    } else if (hasMaskFile && !hasColorFile) { // mask only: copy mask to colorImage to all channels
        uint32_t* mbits = maskImg.pixels;
        uint32_t npix = maskImg.w*maskImg.h;
        // take R channel to all
        for (uint32_t i = 0; i < npix; i++) {
            uint32_t c = mbits[i] & 0xff;
            mbits[i] = c | (c << 8) | (c << 16) | (c << 24);
        }
        colorImg = std::move(maskImg);
    }
    return true;
}

static void
initImage2DMask(const ImageSTB& colorImg, uint8_t* dest)
{
    const uint32_t* src = colorImg.pixels;
    int size = colorImg.w * colorImg.h;
    for (int i = 0; i < size; ++i)
        dest[i] = (uint8_t)(src[i]>>24);
}

struct STBIToMemory
{
    static void FWriteStatic(void* context, void* data, int size) { ((STBIToMemory*)context)->FWrite(data, size); }
    void FWrite(void* data, int size)
    {
        size_t olds = mem.size();
        mem.resize(olds + size);
        memcpy(mem.data() + olds, data, size);
    }
    std::vector<uint8_t> mem;
};

class AsyncGLFWImageLoader : public ThreadPool::Job {
public:
    // state needed for Do()
    ImageSTB colorImg;
    std::string imageFile;
    std::string maskFile;

    virtual bool Do()
    {
        progress = 0;
// simulate being slow
#if 0
        for (int i=0; i<20; i++) {
            std::this_thread::sleep_for(std::chrono::milliseconds(20));
            progress = i;
            if ( abort )
                return false;
        }
#endif
        // actual work
        return LoadSTBImageOnly(colorImg, imageFile.c_str(), maskFile.c_str());
    }
};

// zero player API
DOTS_EXPORT(void)
freeimage_stb(int imageHandle)
{
    if (imageHandle<0 || imageHandle>=(int)allImages.size())
        return;
    delete allImages[imageHandle];
    allImages[imageHandle] = 0;
}

DOTS_EXPORT(int64_t)
startload_stb(const char *imageFile, const char *maskFile)
{
    std::unique_ptr<AsyncGLFWImageLoader> loader(new AsyncGLFWImageLoader);
    loader->imageFile = imageFile;
    loader->maskFile = maskFile;
    return Pool::GetInstance()->Enqueue(std::move(loader));
}

DOTS_EXPORT(void)
abortload_stb(int64_t loadId)
{
    Pool::GetInstance()->Abort(loadId);
}

DOTS_EXPORT(int)
checkload_stb(int64_t loadId, int *imageHandle)
{
    *imageHandle = -1;
    std::unique_ptr<ThreadPool::Job> resultTemp = Pool::GetInstance()->CheckAndRemove(loadId);
    if (!resultTemp)
        return 0; // still loading
    if (!resultTemp->GetReturnValue()) {
        resultTemp.reset(0);
        return 2; // failed
    }
    // put it into a local copy
    int found = -1;
    for (int i=1; i<(int)allImages.size(); i++ ) {
        if (!allImages[i]) {
            found = i;
            break;
        }
    }
    AsyncGLFWImageLoader* resultGLFW = (AsyncGLFWImageLoader*)resultTemp.get();
    ImageSTB *im = new ImageSTB(std::move(resultGLFW->colorImg));
    if (found==-1) {
        allImages.push_back(im);
        *imageHandle = (int)allImages.size()-1;
    } else {
        allImages[found] = im;
        *imageHandle = found;
    }
    return 1; // ok
}

DOTS_EXPORT(void)
freeimagemem_stb(int imageHandle)
{
    if (imageHandle<0 || imageHandle>=(int)allImages.size())
        return;
    allImages[imageHandle]->Free(); // free mem, but keep image
}

DOTS_EXPORT(uint8_t*)
getimage_stb(int imageHandle, int *sizeX, int *sizeY)
{
    if (imageHandle<0 || imageHandle>=(int)allImages.size())
        return 0;
    if (!allImages[imageHandle])
        return 0;
    *sizeX = allImages[imageHandle]->w;
    *sizeY = allImages[imageHandle]->h;
    return (uint8_t*)allImages[imageHandle]->pixels;
}

DOTS_EXPORT(void)
initmask_stb(int imageHandle, uint8_t* buffer)
{
    initImage2DMask(*allImages[imageHandle], buffer);
}

DOTS_EXPORT(void)
finishload_stb()
{
}
