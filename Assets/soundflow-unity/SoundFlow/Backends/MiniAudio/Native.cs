using System.Runtime.InteropServices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Enums;

namespace SoundFlow.Backends.MiniAudio
{
    internal static unsafe partial class Native
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const string LibraryName = "miniaudio";
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        private const string LibraryName = "miniaudio.so";
#elif UNITY_ANDROID && !UNITY_EDITOR
        private const string LibraryName = "miniaudio.so";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private const string LibraryName = "miniaudio";
#endif  

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void AudioCallback(nint device, nint output, nint input, uint length);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate Result BufferProcessingCallback(
            nint pCodecContext,          // The native decoder/encoder instance pointer (ma_decoder*, ma_encoder*)
            nint pBuffer,                // The buffer pointer (void* pBufferOut or const void* pBufferIn)
            ulong bytesRequested,        // The number of bytes requested (bytesToRead or bytesToWrite)
            out ulong* bytesTransferred   // The actual number of bytes processed/transferred (size_t*)
        );

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate Result SeekCallback(nint pDecoder, long byteOffset, SeekPoint origin);

        #region Encoder

        // , StringMarshalling = StringMarshalling.Utf8
        [DllImport(LibraryName, EntryPoint = "ma_encoder_init")]
        public static extern Result EncoderInit(BufferProcessingCallback onRead, SeekCallback onSeekCallback, nint pUserData, nint pConfig, nint pEncoder);

        [DllImport(LibraryName, EntryPoint = "ma_encoder_uninit")]
        public static extern void EncoderUninit(nint pEncoder);

        [DllImport(LibraryName, EntryPoint = "ma_encoder_write_pcm_frames")]
        public static extern Result EncoderWritePcmFrames(nint pEncoder, nint pFramesIn, ulong frameCount,
            ulong* pFramesWritten);

        #endregion

        #region Decoder

        [DllImport(LibraryName, EntryPoint = "ma_decoder_init")]
        public static extern Result DecoderInit(BufferProcessingCallback onRead, SeekCallback onSeekCallback, nint pUserData,
            nint pConfig, nint pDecoder);

        [DllImport(LibraryName, EntryPoint = "ma_decoder_uninit")]
        public static extern Result DecoderUninit(nint pDecoder);

        [DllImport(LibraryName, EntryPoint = "ma_decoder_read_pcm_frames")]
        public static extern Result DecoderReadPcmFrames(nint decoder, nint framesOut, uint frameCount,
            out ulong framesRead);

        [DllImport(LibraryName, EntryPoint = "ma_decoder_seek_to_pcm_frame")]
        public static extern Result DecoderSeekToPcmFrame(nint decoder, ulong frame);

        [DllImport(LibraryName, EntryPoint = "ma_decoder_get_length_in_pcm_frames")]
        public static extern Result DecoderGetLengthInPcmFrames(nint decoder, out uint* length);

        #endregion

        #region Context

        [DllImport(LibraryName, EntryPoint = "ma_context_init")]
        public static extern Result ContextInit(nint backends, uint backendCount, nint config, nint context);

        [DllImport(LibraryName, EntryPoint = "ma_context_uninit")]
        public static extern void ContextUninit(nint context);

        #endregion

        #region Device

        [DllImport(LibraryName, EntryPoint = "sf_get_devices")]
        public static extern Result GetDevices(nint context, out nint pPlaybackDevices, out nint pCaptureDevices, out nint playbackDeviceCount, out nint captureDeviceCount);

        [DllImport(LibraryName, EntryPoint = "ma_device_init")]
        public static extern Result DeviceInit(nint context, nint config, nint device);

        [DllImport(LibraryName, EntryPoint = "ma_device_uninit")]
        public static extern void DeviceUninit(nint device);

        [DllImport(LibraryName, EntryPoint = "ma_device_start")]
        public static extern Result DeviceStart(nint device);

        [DllImport(LibraryName, EntryPoint = "ma_device_stop")]
        public static extern Result DeviceStop(nint device);

        #endregion

        #region Allocations

        [DllImport(LibraryName, EntryPoint = "sf_allocate_encoder")]
        public static extern nint AllocateEncoder();

        [DllImport(LibraryName, EntryPoint = "sf_allocate_decoder")]
        public static extern nint AllocateDecoder();

        [DllImport(LibraryName, EntryPoint = "sf_allocate_context")]
        public static extern nint AllocateContext();

        [DllImport(LibraryName, EntryPoint = "sf_allocate_device")]
        public static extern nint AllocateDevice();

        [DllImport(LibraryName, EntryPoint = "sf_allocate_decoder_config")]
        public static extern nint AllocateDecoderConfig(SampleFormat format, uint channels, uint sampleRate);

        [DllImport(LibraryName, EntryPoint = "sf_allocate_encoder_config")]
        public static extern nint AllocateEncoderConfig(EncodingFormat encodingFormat, SampleFormat format, uint channels,
            uint sampleRate);

        [DllImport(LibraryName, EntryPoint = "sf_allocate_device_config")]
        public static extern nint AllocateDeviceConfig(Capability capabilityType, SampleFormat format, uint channels,
            uint sampleRate, AudioCallback dataCallback, nint playbackDevice, nint captureDevice);

        #endregion

        #region Utils

        [DllImport(LibraryName, EntryPoint = "sf_free")]
        public static extern void Free(nint ptr);

        #endregion
    }
}