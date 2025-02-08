#define DR_FLAC_IMPLEMENTATION
#define DR_MP3_IMPLEMENTATION
#define DR_WAV_IMPLEMENTATION
#define MINIAUDIO_IMPLEMENTATION

#include "Submodules/miniaudio/miniaudio.h"

#define LIBRARY_H

// Helper macro for  memory allocation.
#define sf_create(t) (t*) ma_malloc(sizeof(t), NULL)

void sf_debug(const char *msg, ...) {
    va_list args;
    va_start(args, msg);

    // SF: Print
    freopen("native_output.txt", "a", stdout); // Redirect stdout to a file
    vprintf(msg, args);
    fclose(stdout);

    va_end(args);
}

extern "C" {
// Frees a structure allocated with sf_create().
MA_API void sf_free(void *ptr) {
    return ma_free(ptr, nullptr);
}

// Allocate memory for a decoder struct.
MA_API ma_decoder *sf_allocate_decoder() {
    return sf_create(ma_decoder);
}

// Allocate memory for an encoder struct.
MA_API ma_encoder *sf_allocate_encoder() {
    return sf_create(ma_encoder);
}

// Allocate memory for a device struct.
MA_API ma_device *sf_allocate_device() {
    return sf_create(ma_device);
}

// Allocate memory for a device configuration struct.
MA_API ma_device_config *sf_allocate_device_config(const ma_device_type deviceType, const ma_format format,
                                                   const ma_uint32 channels, const ma_uint32 sampleRate,
                                                   const ma_device_data_proc dataCallback) {
    auto *config = sf_create(ma_device_config);
    if (config == nullptr) {
        return nullptr;
    }
    MA_ZERO_OBJECT(config);

    *config = ma_device_config_init(deviceType);

    // Configure device type and callback
    config->dataCallback = dataCallback;

    // Configure to use stereo signed 16 bit audio
    config->sampleRate = sampleRate;
    config->playback.format = format;
    config->playback.channels = channels;
    config->capture.format = format;
    config->capture.channels = channels;


    return config;
}

// Allocate memory for a decoder configuration struct.
MA_API ma_decoder_config *sf_allocate_decoder_config(const ma_format outputFormat, const ma_uint32 outputChannels,
                                                     const ma_uint32 outputSampleRate) {
    auto *pConfig = sf_create(ma_decoder_config);
    if (pConfig == nullptr) {
        return nullptr;
    }

    MA_ZERO_OBJECT(pConfig);
    *pConfig = ma_decoder_config_init(outputFormat, outputChannels, outputSampleRate);


    return pConfig;
}

// Allocate memory for an encoder configuration struct.
MA_API ma_encoder_config *sf_allocate_encoder_config(const ma_encoding_format encodingFormat, const ma_format format,
                                                     const ma_uint32 channels, const ma_uint32 sampleRate) {
    auto *pConfig = sf_create(ma_encoder_config);
    if (pConfig == nullptr) {
        return nullptr;
    }

    MA_ZERO_OBJECT(pConfig);
    *pConfig = ma_encoder_config_init(encodingFormat, format, channels, sampleRate);

    return pConfig;
}

// Seek current stream position to a specific offset (necessary to call ma_decoder_seek_to_pcm_frame from native code because of invoking issue).
MA_API ma_result sf_decoder_seek_to_frame(ma_decoder *decoder, const ma_uint64 frameIndex) {
    return ma_decoder_seek_to_pcm_frame(decoder, frameIndex);
}

// Seek current stream position to a specific time in seconds.
MA_API ma_result sf_decoder_seek_to_time(ma_decoder *decoder, const double timeInSec) {
    if (timeInSec < 0) {
        return MA_INVALID_ARGS;
    }

    auto target_frame = static_cast<ma_uint64>(timeInSec * decoder->outputSampleRate);
    return ma_decoder_seek_to_pcm_frame(decoder, target_frame);
}
} // End of extern "C" block
