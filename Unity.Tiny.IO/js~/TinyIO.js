mergeInto(LibraryManager.library, {
#if SINGLE_FILE
    js_fetch_embedded__proxy: 'sync',
    js_fetch_embedded: function (url, ppData, pLen) {
        var asset = SINGLE_FILE_ASSETS[UTF8ToString(url)];
        if (!asset)
            return false;
        var data = base64Decode(asset);
        var ptr = _malloc(data.length);
        HEAPU8.set(data, ptr);
        HEAPU32[ppData >> 2] = ptr;
        HEAPU32[pLen >> 2] = data.length;
        return true;
    },
#endif
});
