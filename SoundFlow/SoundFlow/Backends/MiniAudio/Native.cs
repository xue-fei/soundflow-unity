using System;
using System.Runtime.InteropServices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Enums;

namespace SoundFlow.Backends.MiniAudio
{

    internal static unsafe partial class Native
    {
        private const string LibraryName = "miniaudio";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void AudioCallback(IntPtr device, IntPtr output, IntPtr input, uint length);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate Result BufferProcessingCallback(
            IntPtr pCodecContext,          // The native decoder/encoder instance pointer (ma_decoder*, ma_encoder*)
            IntPtr pBuffer,                // The buffer pointer (void* pBufferOut or const void* pBufferIn)
            ulong bytesRequested,        // The number of bytes requested (bytesToRead or bytesToWrite)
            out ulong* bytesTransferred   // The actual number of bytes processed/transferred (size_t*)
        );

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate Result SeekCallback(IntPtr pDecoder, long byteOffset, SeekPoint origin);

        #region Encoder
        // StringMarshalling = StringMarshalling.Utf8
        [DllImport(LibraryName, EntryPoint = "ma_encoder_init", CharSet = CharSet.Auto)]
        public static extern Result EncoderInit(BufferProcessingCallback onRead, SeekCallback onSeekCallback, IntPtr pUserData, IntPtr pConfig, IntPtr pEncoder);

        [DllImport(LibraryName, EntryPoint = "ma_encoder_uninit")]
        public static extern void EncoderUninit(IntPtr pEncoder);

        [DllImport(LibraryName, EntryPoint = "ma_encoder_write_pcm_frames")]
        public static extern Result EncoderWritePcmFrames(IntPtr pEncoder, IntPtr pFramesIn, ulong frameCount,
            ulong* pFramesWritten);

        #endregion

        #region Decoder

        [DllImport(LibraryName, EntryPoint = "ma_decoder_init")]
        public static extern Result DecoderInit(BufferProcessingCallback onRead, SeekCallback onSeekCallback, IntPtr pUserData,
            IntPtr pConfig, IntPtr pDecoder);

        [DllImport(LibraryName, EntryPoint = "ma_decoder_uninit")]
        public static extern Result DecoderUninit(IntPtr pDecoder);

        [DllImport(LibraryName, EntryPoint = "ma_decoder_read_pcm_frames")]
        public static extern Result DecoderReadPcmFrames(IntPtr decoder, IntPtr framesOut, uint frameCount,
            out ulong framesRead);

        [DllImport(LibraryName, EntryPoint = "ma_decoder_seek_to_pcm_frame")]
        public static extern Result DecoderSeekToPcmFrame(IntPtr decoder, ulong frame);

        [DllImport(LibraryName, EntryPoint = "ma_decoder_get_length_in_pcm_frames")]
        public static extern Result DecoderGetLengthInPcmFrames(IntPtr decoder, out uint* length);

        #endregion

        #region Context

        [DllImport(LibraryName, EntryPoint = "ma_context_init")]
        public static extern Result ContextInit(IntPtr backends, uint backendCount, IntPtr config, IntPtr context);

        [DllImport(LibraryName, EntryPoint = "ma_context_uninit")]
        public static extern void ContextUninit(IntPtr context);

        #endregion

        #region Device

        [DllImport(LibraryName, EntryPoint = "sf_get_devices")]
        public static extern Result GetDevices(IntPtr context, out IntPtr pPlaybackDevices, out IntPtr pCaptureDevices, out IntPtr playbackDeviceCount, out IntPtr captureDeviceCount);

        [DllImport(LibraryName, EntryPoint = "ma_device_init")]
        public static extern Result DeviceInit(IntPtr context, IntPtr config, IntPtr device);

        [DllImport(LibraryName, EntryPoint = "ma_device_uninit")]
        public static extern void DeviceUninit(IntPtr device);

        [DllImport(LibraryName, EntryPoint = "ma_device_start")]
        public static extern Result DeviceStart(IntPtr device);

        [DllImport(LibraryName, EntryPoint = "ma_device_stop")]
        public static extern Result DeviceStop(IntPtr device);

        #endregion

        #region Allocations

        [DllImport(LibraryName, EntryPoint = "sf_allocate_encoder")]
        public static extern IntPtr AllocateEncoder();

        [DllImport(LibraryName, EntryPoint = "sf_allocate_decoder")]
        public static extern IntPtr AllocateDecoder();

        [DllImport(LibraryName, EntryPoint = "sf_allocate_context")]
        public static extern IntPtr AllocateContext();

        [DllImport(LibraryName, EntryPoint = "sf_allocate_device")]
        public static extern IntPtr AllocateDevice();

        [DllImport(LibraryName, EntryPoint = "sf_allocate_decoder_config")]
        public static extern IntPtr AllocateDecoderConfig(SampleFormat format, uint channels, uint sampleRate);

        [DllImport(LibraryName, EntryPoint = "sf_allocate_encoder_config")]
        public static extern IntPtr AllocateEncoderConfig(EncodingFormat encodingFormat, SampleFormat format, uint channels,
            uint sampleRate);

        [DllImport(LibraryName, EntryPoint = "sf_allocate_device_config")]
        public static extern IntPtr AllocateDeviceConfig(Capability capabilityType, SampleFormat format, uint channels,
            uint sampleRate, AudioCallback dataCallback, IntPtr playbackDevice, IntPtr captureDevice);

        #endregion

        #region Utils

        [DllImport(LibraryName, EntryPoint = "sf_free")]
        public static extern void Free(IntPtr ptr);

        #endregion
    }
}