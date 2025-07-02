using System;

namespace SoundFlow.Extensions.WebRtc.Apm
{
    /// <summary>
    /// Represents a stream configuration for audio processing
    /// </summary>
    public class StreamConfig : IDisposable
    {
        private IntPtr _nativeConfig;

        /// <summary>
        /// Creates a new stream configuration
        /// </summary>
        /// <param name="sampleRateHz">Sample rate in Hz</param>
        /// <param name="numChannels">Number of channels</param>
        public StreamConfig(int sampleRateHz, int numChannels)
        {
            _nativeConfig = NativeMethods.webrtc_apm_stream_config_create(sampleRateHz, (UIntPtr)numChannels);
            if (_nativeConfig == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create stream config");
        }

        /// <summary>
        /// Sample rate in Hz
        /// </summary>
        public int SampleRateHz => NativeMethods.webrtc_apm_stream_config_sample_rate_hz(_nativeConfig);

        /// <summary>
        /// Number of channels
        /// </summary>
        public int NumChannels => (int)NativeMethods.webrtc_apm_stream_config_num_channels(_nativeConfig);

        internal IntPtr NativePtr => _nativeConfig;

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (_nativeConfig != IntPtr.Zero)
                {
                    NativeMethods.webrtc_apm_stream_config_destroy(_nativeConfig);
                    _nativeConfig = IntPtr.Zero;
                }

                disposedValue = true;
            }
        }

        ~StreamConfig()
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