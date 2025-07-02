using System; 

namespace SoundFlow.Extensions.WebRtc.Apm
{
    /// <summary>
    /// Represents a processing configuration for audio processing
    /// </summary>
    public class ProcessingConfig : IDisposable
    {
        private IntPtr _nativeConfig;

        /// <summary>
        /// Creates a new processing configuration
        /// </summary>
        public ProcessingConfig()
        {
            _nativeConfig = NativeMethods.webrtc_apm_processing_config_create();
            if (_nativeConfig == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create processing config");
        }

        /// <summary>
        /// Input stream configuration
        /// </summary>
        public StreamConfig InputStream
        {
            get
            {
                var ptr = NativeMethods.webrtc_apm_processing_config_input_stream(_nativeConfig);
                return new StreamConfig(
                    NativeMethods.webrtc_apm_stream_config_sample_rate_hz(ptr),
                    (int)NativeMethods.webrtc_apm_stream_config_num_channels(ptr));
            }
        }

        /// <summary>
        /// Output stream configuration
        /// </summary>
        public StreamConfig OutputStream
        {
            get
            {
                var ptr = NativeMethods.webrtc_apm_processing_config_output_stream(_nativeConfig);
                return new StreamConfig(
                    NativeMethods.webrtc_apm_stream_config_sample_rate_hz(ptr),
                    (int)NativeMethods.webrtc_apm_stream_config_num_channels(ptr));
            }
        }

        /// <summary>
        /// Reverse input stream configuration
        /// </summary>
        public StreamConfig ReverseInputStream
        {
            get
            {
                var ptr = NativeMethods.webrtc_apm_processing_config_reverse_input_stream(_nativeConfig);
                return new StreamConfig(
                    NativeMethods.webrtc_apm_stream_config_sample_rate_hz(ptr),
                    (int)NativeMethods.webrtc_apm_stream_config_num_channels(ptr));
            }
        }

        /// <summary>
        /// Reverse output stream configuration
        /// </summary>
        public StreamConfig ReverseOutputStream
        {
            get
            {
                var ptr = NativeMethods.webrtc_apm_processing_config_reverse_output_stream(_nativeConfig);
                return new StreamConfig(
                    NativeMethods.webrtc_apm_stream_config_sample_rate_hz(ptr),
                    (int)NativeMethods.webrtc_apm_stream_config_num_channels(ptr));
            }
        }

        internal IntPtr NativePtr => _nativeConfig;

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (_nativeConfig != IntPtr.Zero)
                {
                    NativeMethods.webrtc_apm_processing_config_destroy(_nativeConfig);
                    _nativeConfig = IntPtr.Zero;
                }

                disposedValue = true;
            }
        }

        ~ProcessingConfig()
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