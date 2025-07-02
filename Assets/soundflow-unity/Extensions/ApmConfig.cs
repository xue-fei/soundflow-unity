using System;

namespace SoundFlow.Extensions.WebRtc.Apm
{
    /// <summary>
    /// Represents an APM configuration
    /// </summary>
    public class ApmConfig : IDisposable
    {
        private IntPtr _nativeConfig;

        /// <summary>
        /// Creates a new APM configuration
        /// </summary>
        public ApmConfig()
        {
            _nativeConfig = NativeMethods.webrtc_apm_config_create();
            if (_nativeConfig == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create APM config");
        }

        /// <summary>
        /// Configures the echo canceller
        /// </summary>
        /// <param name="enabled">Whether echo cancellation is enabled</param>
        /// <param name="mobileMode">Whether to use mobile mode</param>
        public void SetEchoCanceller(bool enabled, bool mobileMode)
        {
            NativeMethods.webrtc_apm_config_set_echo_canceller(_nativeConfig, enabled ? 1 : 0, mobileMode ? 1 : 0);
        }

        /// <summary>
        /// Configures noise suppression
        /// </summary>
        /// <param name="enabled">Whether noise suppression is enabled</param>
        /// <param name="level">Noise suppression level</param>
        public void SetNoiseSuppression(bool enabled, NoiseSuppressionLevel level)
        {
            NativeMethods.webrtc_apm_config_set_noise_suppression(_nativeConfig, enabled ? 1 : 0, level);
        }

        /// <summary>
        /// Configures gain controller 1
        /// </summary>
        /// <param name="enabled">Whether gain controller is enabled</param>
        /// <param name="mode">Gain control mode</param>
        /// <param name="targetLevelDbfs">Target level in dBFS</param>
        /// <param name="compressionGainDb">Compression gain in dB</param>
        /// <param name="enableLimiter">Whether to enable the limiter</param>
        public void SetGainController1(bool enabled, GainControlMode mode, int targetLevelDbfs, int compressionGainDb,
            bool enableLimiter)
        {
            NativeMethods.webrtc_apm_config_set_gain_controller1(
                _nativeConfig,
                enabled ? 1 : 0,
                mode,
                targetLevelDbfs,
                compressionGainDb,
                enableLimiter ? 1 : 0);
        }

        /// <summary>
        /// Configures gain controller 2
        /// </summary>
        /// <param name="enabled">Whether gain controller 2 is enabled</param>
        public void SetGainController2(bool enabled)
        {
            NativeMethods.webrtc_apm_config_set_gain_controller2(_nativeConfig, enabled ? 1 : 0);
        }

        /// <summary>
        /// Configures the high pass filter
        /// </summary>
        /// <param name="enabled">Whether the high pass filter is enabled</param>
        public void SetHighPassFilter(bool enabled)
        {
            NativeMethods.webrtc_apm_config_set_high_pass_filter(_nativeConfig, enabled ? 1 : 0);
        }

        /// <summary>
        /// Configures the pre-amplifier
        /// </summary>
        /// <param name="enabled">Whether the pre-amplifier is enabled</param>
        /// <param name="fixedGainFactor">Fixed gain factor</param>
        public void SetPreAmplifier(bool enabled, float fixedGainFactor)
        {
            NativeMethods.webrtc_apm_config_set_pre_amplifier(_nativeConfig, enabled ? 1 : 0, fixedGainFactor);
        }

        /// <summary>
        /// Configures the processing pipeline
        /// </summary>
        /// <param name="maxInternalRate">Maximum internal processing rate</param>
        /// <param name="multiChannelRender">Whether to enable multi-channel render</param>
        /// <param name="multiChannelCapture">Whether to enable multi-channel capture</param>
        /// <param name="downmixMethod">Downmix method</param>
        public void SetPipeline(int maxInternalRate, bool multiChannelRender, bool multiChannelCapture,
            DownmixMethod downmixMethod)
        {
            NativeMethods.webrtc_apm_config_set_pipeline(
                _nativeConfig,
                maxInternalRate,
                multiChannelRender ? 1 : 0,
                multiChannelCapture ? 1 : 0,
                downmixMethod);
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
                    NativeMethods.webrtc_apm_config_destroy(_nativeConfig);
                    _nativeConfig = IntPtr.Zero;
                }

                disposedValue = true;
            }
        }

        ~ApmConfig()
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