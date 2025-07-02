using System;
using System.Runtime.InteropServices;

namespace SoundFlow.Extensions.WebRtc.Apm
{
    /// <summary>
    /// Error codes returned by the WebRTC Audio Processing Module
    /// </summary>
    public enum ApmError
    {
        NoError = 0,
        UnspecifiedError = -1,
        CreationFailed = -2,
        BadParameter = -6,
        BadSampleRate = -7,
        BadDataLength = -8,
        BadNumChannels = -9
    }

    /// <summary>
    /// Noise suppression levels
    /// </summary>
    public enum NoiseSuppressionLevel
    {
        Low,
        Moderate,
        High,
        VeryHigh
    }

    /// <summary>
    /// Gain controller modes
    /// </summary>
    public enum GainControlMode
    {
        AdaptiveAnalog,
        AdaptiveDigital,
        FixedDigital
    }

    /// <summary>
    /// Downmix methods
    /// </summary>
    public enum DownmixMethod
    {
        AverageChannels,
        UseFirstChannel
    }

    /// <summary>
    /// Runtime setting types
    /// </summary>
    public enum RuntimeSettingType
    {
        CapturePreGain,
        CaptureCompressionGain,
        CaptureFixedPostGain,
        PlayoutVolumeChange,
        CustomRenderSetting,
        PlayoutAudioDeviceChange,
        CapturePostGain,
        CaptureOutputUsed
    }

    /// <summary>
    /// Provides access to the WebRTC Audio Processing Module
    /// </summary>
    public class AudioProcessingModule : IDisposable
    {
        public IntPtr NativePtr;

        /// <summary>
        /// Creates a new Audio Processing Module instance
        /// </summary>
        public AudioProcessingModule()
        {
            NativePtr = NativeMethods.webrtc_apm_create();
            if (NativePtr == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create APM instance");
        }

        /// <summary>
        /// Applies configuration to the APM
        /// </summary>
        /// <param name="config">Configuration to apply</param>
        /// <returns>Error code</returns>
        public ApmError ApplyConfig(ApmConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return NativeMethods.webrtc_apm_apply_config(NativePtr, config.NativePtr);
        }

        /// <summary>
        /// Initializes the APM with default settings
        /// </summary>
        /// <returns>Error code</returns>
        public ApmError Initialize()
        {
            return NativeMethods.webrtc_apm_initialize(NativePtr);
        }

        /// <summary>
        /// Processes a stream of audio data
        /// </summary>
        /// <param name="src">Source audio data (array of channels)</param>
        /// <param name="inputConfig">Input stream configuration</param>
        /// <param name="outputConfig">Output stream configuration</param>
        /// <param name="dest">Destination audio data (array of channels)</param>
        /// <returns>Error code</returns>
        public ApmError ProcessStream(float[][] src, StreamConfig inputConfig, StreamConfig outputConfig, float[][] dest)
        {
            if (src == null || inputConfig == null || outputConfig == null || dest == null)
                throw new ArgumentNullException();

            // Convert float[][] to IntPtr[] for interop
            var srcPtrs = new IntPtr[src.Length];
            var destPtrs = new IntPtr[dest.Length];

            try
            {
                for (int i = 0; i < src.Length; i++)
                {
                    srcPtrs[i] = Marshal.AllocHGlobal(src[i].Length * sizeof(float));
                    Marshal.Copy(src[i], 0, srcPtrs[i], src[i].Length);
                }

                for (int i = 0; i < dest.Length; i++)
                {
                    destPtrs[i] = Marshal.AllocHGlobal(dest[i].Length * sizeof(float));
                }

                // Pin the arrays
                GCHandle srcHandle = GCHandle.Alloc(srcPtrs, GCHandleType.Pinned);
                GCHandle destHandle = GCHandle.Alloc(destPtrs, GCHandleType.Pinned);

                try
                {
                    var error = NativeMethods.webrtc_apm_process_stream(
                        NativePtr,
                        srcHandle.AddrOfPinnedObject(),
                        inputConfig.NativePtr,
                        outputConfig.NativePtr,
                        destHandle.AddrOfPinnedObject());

                    // Copy results back
                    for (int i = 0; i < dest.Length; i++)
                    {
                        Marshal.Copy(destPtrs[i], dest[i], 0, dest[i].Length);
                    }

                    return error;
                }
                finally
                {
                    srcHandle.Free();
                    destHandle.Free();
                }
            }
            finally
            {
                // Free allocated memory
                for (int i = 0; i < srcPtrs.Length; i++)
                {
                    if (srcPtrs[i] != IntPtr.Zero)
                        Marshal.FreeHGlobal(srcPtrs[i]);
                }

                for (int i = 0; i < destPtrs.Length; i++)
                {
                    if (destPtrs[i] != IntPtr.Zero)
                        Marshal.FreeHGlobal(destPtrs[i]);
                }
            }
        }

        /// <summary>
        /// Processes a reverse stream of audio data
        /// </summary>
        /// <param name="src">Source audio data (array of channels)</param>
        /// <param name="inputConfig">Input stream configuration</param>
        /// <param name="outputConfig">Output stream configuration</param>
        /// <param name="dest">Destination audio data (array of channels)</param>
        /// <returns>Error code</returns>
        public ApmError ProcessReverseStream(float[][] src, StreamConfig inputConfig, StreamConfig outputConfig,
            float[][] dest)
        {
            if (src == null || inputConfig == null || outputConfig == null || dest == null)
                throw new ArgumentNullException();

            // Convert float[][] to IntPtr[] for interop
            var srcPtrs = new IntPtr[src.Length];
            var destPtrs = new IntPtr[dest.Length];

            try
            {
                for (int i = 0; i < src.Length; i++)
                {
                    srcPtrs[i] = Marshal.AllocHGlobal(src[i].Length * sizeof(float));
                    Marshal.Copy(src[i], 0, srcPtrs[i], src[i].Length);
                }

                for (int i = 0; i < dest.Length; i++)
                {
                    destPtrs[i] = Marshal.AllocHGlobal(dest[i].Length * sizeof(float));
                }

                // Pin the arrays
                GCHandle srcHandle = GCHandle.Alloc(srcPtrs, GCHandleType.Pinned);
                GCHandle destHandle = GCHandle.Alloc(destPtrs, GCHandleType.Pinned);

                try
                {
                    var error = NativeMethods.webrtc_apm_process_reverse_stream(
                        NativePtr,
                        srcHandle.AddrOfPinnedObject(),
                        inputConfig.NativePtr,
                        outputConfig.NativePtr,
                        destHandle.AddrOfPinnedObject());

                    // Copy results back
                    for (int i = 0; i < dest.Length; i++)
                    {
                        Marshal.Copy(destPtrs[i], dest[i], 0, dest[i].Length);
                    }

                    return error;
                }
                finally
                {
                    srcHandle.Free();
                    destHandle.Free();
                }
            }
            finally
            {
                // Free allocated memory
                for (int i = 0; i < srcPtrs.Length; i++)
                {
                    if (srcPtrs[i] != IntPtr.Zero)
                        Marshal.FreeHGlobal(srcPtrs[i]);
                }

                for (int i = 0; i < destPtrs.Length; i++)
                {
                    if (destPtrs[i] != IntPtr.Zero)
                        Marshal.FreeHGlobal(destPtrs[i]);
                }
            }
        }

        /// <summary>
        /// Analyzes a reverse stream of audio data
        /// </summary>
        /// <param name="data">Audio data to analyze (array of channels)</param>
        /// <param name="reverseConfig">Reverse stream configuration</param>
        /// <returns>Error code</returns>
        public ApmError AnalyzeReverseStream(float[][] data, StreamConfig reverseConfig)
        {
            if (data == null || reverseConfig == null)
                throw new ArgumentNullException();

            // Convert float[][] to IntPtr[] for interop
            var dataPtrs = new IntPtr[data.Length];

            try
            {
                for (int i = 0; i < data.Length; i++)
                {
                    dataPtrs[i] = Marshal.AllocHGlobal(data[i].Length * sizeof(float));
                    Marshal.Copy(data[i], 0, dataPtrs[i], data[i].Length);
                }

                // Pin the array
                GCHandle dataHandle = GCHandle.Alloc(dataPtrs, GCHandleType.Pinned);

                try
                {
                    return NativeMethods.webrtc_apm_analyze_reverse_stream(
                        NativePtr,
                        dataHandle.AddrOfPinnedObject(),
                        reverseConfig.NativePtr);
                }
                finally
                {
                    dataHandle.Free();
                }
            }
            finally
            {
                // Free allocated memory
                for (int i = 0; i < dataPtrs.Length; i++)
                {
                    if (dataPtrs[i] != IntPtr.Zero)
                        Marshal.FreeHGlobal(dataPtrs[i]);
                }
            }
        }

        /// <summary>
        /// Sets the analog level for the current stream
        /// </summary>
        /// <param name="level">Analog level (0-255)</param>
        public void SetStreamAnalogLevel(int level)
        {
            NativeMethods.webrtc_apm_set_stream_analog_level(NativePtr, level);
        }

        /// <summary>
        /// Gets the recommended analog level for the current stream
        /// </summary>
        /// <returns>Recommended analog level (0-255)</returns>
        public int GetRecommendedStreamAnalogLevel()
        {
            return NativeMethods.webrtc_apm_recommended_stream_analog_level(NativePtr);
        }

        /// <summary>
        /// Sets the delay in ms between ProcessReverseStream() and ProcessStream()
        /// </summary>
        /// <param name="delayMs">Delay in milliseconds</param>
        public void SetStreamDelayMs(int delayMs)
        {
            NativeMethods.webrtc_apm_set_stream_delay_ms(NativePtr, delayMs);
        }

        /// <summary>
        /// Gets the current stream delay
        /// </summary>
        /// <returns>Current delay in milliseconds</returns>
        public int GetStreamDelayMs()
        {
            return NativeMethods.webrtc_apm_stream_delay_ms(NativePtr);
        }

        /// <summary>
        /// Sets whether a key was pressed during this chunk of audio
        /// </summary>
        /// <param name="keyPressed">Whether a key was pressed</param>
        public void SetStreamKeyPressed(bool keyPressed)
        {
            NativeMethods.webrtc_apm_set_stream_key_pressed(NativePtr, keyPressed ? 1 : 0);
        }

        /// <summary>
        /// Sets whether the output will be muted or in some other way not used
        /// </summary>
        /// <param name="muted">Whether output will be muted</param>
        public void SetOutputWillBeMuted(bool muted)
        {
            NativeMethods.webrtc_apm_set_output_will_be_muted(NativePtr, muted ? 1 : 0);
        }

        /// <summary>
        /// Sets a runtime setting with a float value
        /// </summary>
        /// <param name="type">Type of setting</param>
        /// <param name="value">Float value</param>
        public void SetRuntimeSetting(RuntimeSettingType type, float value)
        {
            NativeMethods.webrtc_apm_set_runtime_setting_float(NativePtr, type, value);
        }

        /// <summary>
        /// Sets a runtime setting with an integer value
        /// </summary>
        /// <param name="type">Type of setting</param>
        /// <param name="value">Integer value</param>
        public void SetRuntimeSetting(RuntimeSettingType type, int value)
        {
            NativeMethods.webrtc_apm_set_runtime_setting_int(NativePtr, type, value);
        }

        /// <summary>
        /// Gets the current processing sample rate
        /// </summary>
        /// <returns>Sample rate in Hz</returns>
        public int GetProcSampleRateHz()
        {
            return NativeMethods.webrtc_apm_proc_sample_rate_hz(NativePtr);
        }

        /// <summary>
        /// Gets the current processing split sample rate
        /// </summary>
        /// <returns>Split sample rate in Hz</returns>
        public int GetProcSplitSampleRateHz()
        {
            return NativeMethods.webrtc_apm_proc_split_sample_rate_hz(NativePtr);
        }

        /// <summary>
        /// Gets the number of input channels
        /// </summary>
        /// <returns>Number of input channels</returns>
        public int GetNumInputChannels()
        {
            return (int)NativeMethods.webrtc_apm_num_input_channels(NativePtr);
        }

        /// <summary>
        /// Gets the number of processing channels
        /// </summary>
        /// <returns>Number of processing channels</returns>
        public int GetNumProcChannels()
        {
            return (int)NativeMethods.webrtc_apm_num_proc_channels(NativePtr);
        }

        /// <summary>
        /// Gets the number of output channels
        /// </summary>
        /// <returns>Number of output channels</returns>
        public int GetNumOutputChannels()
        {
            return (int)NativeMethods.webrtc_apm_num_output_channels(NativePtr);
        }

        /// <summary>
        /// Gets the number of reverse channels
        /// </summary>
        /// <returns>Number of reverse channels</returns>
        public int GetNumReverseChannels()
        {
            return (int)NativeMethods.webrtc_apm_num_reverse_channels(NativePtr);
        }

        /// <summary>
        /// Creates an AEC dump file
        /// </summary>
        /// <param name="fileName">Path to the dump file</param>
        /// <param name="maxLogSizeBytes">Maximum size of the log file in bytes (-1 for unlimited)</param>
        /// <returns>True if successful</returns>
        public bool CreateAecDump(string fileName, long maxLogSizeBytes)
        {
            return NativeMethods.webrtc_apm_create_aec_dump(NativePtr, fileName, maxLogSizeBytes) != 0;
        }

        /// <summary>
        /// Detaches the current AEC dump
        /// </summary>
        public void DetachAecDump()
        {
            NativeMethods.webrtc_apm_detach_aec_dump(NativePtr);
        }

        /// <summary>
        /// Calculates the frame size for a given sample rate
        /// </summary>
        /// <param name="sampleRateHz">Sample rate in Hz</param>
        /// <returns>Frame size in samples</returns>
        public static int GetFrameSize(int sampleRateHz)
        {
            return (int)NativeMethods.webrtc_apm_get_frame_size(sampleRateHz);
        }

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (NativePtr != IntPtr.Zero)
                {
                    NativeMethods.webrtc_apm_destroy(NativePtr);
                    NativePtr = IntPtr.Zero;
                }

                disposedValue = true;
            }
        }

        ~AudioProcessingModule()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}