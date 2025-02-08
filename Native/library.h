// library.h
// Simple facade for the MiniAudio library, providing C-compatible functions
// for memory allocation and basic structure initialization.

#ifndef LIBRARY_H
#define LIBRARY_H

#include "Submodules/miniaudio/miniaudio.h"

extern "C" {

    // Frees a structure allocated with sf_create().
    MA_API void sf_free(void* ptr);

    // Allocate memory for a decoder struct.
    MA_API ma_decoder* sf_allocate_decoder();

    // Allocate memory for an encoder struct.
    MA_API void* sf_allocate_encoder();

    // Allocate memory for a device struct.
    MA_API void* sf_allocate_device();

    // Allocate memory for a device configuration struct.
    MA_API ma_device_config* sf_allocate_device_config(ma_device_type deviceType, ma_uint32 sampleRate, ma_device_data_proc dataCallback);

    // Allocate memory for a decoder configuration struct.
    MA_API ma_decoder_config* sf_allocate_decoder_config(ma_format outputFormat, ma_uint32 outputChannels, ma_uint32 outputSampleRate);

    // Allocate memory for an encoder configuration struct.
    MA_API ma_encoder_config* sf_allocate_encoder_config(ma_encoding_format encodingFormat, ma_format format, ma_uint32 channels, ma_uint32 sampleRate);

    // Seek current stream position to a specific offset.
    MA_API ma_result sf_decoder_seek_to_frame(ma_decoder* decoder, ma_uint64 frameIndex);

    // Seek current stream position to a specific time in seconds.
    MA_API ma_result sf_decoder_seek_to_time(const ma_decoder* decoder, double timeInSec);
}

#endif // LIBRARY_H