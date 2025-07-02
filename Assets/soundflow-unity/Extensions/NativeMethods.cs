using System;
using System.Runtime.InteropServices;

namespace SoundFlow.Extensions.WebRtc.Apm
{
    public static class NativeMethods
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const string LibraryName = "webrtc-apm";
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        private const string LibraryName = "webrtc-apm.so";
#elif UNITY_ANDROID && !UNITY_EDITOR
        private const string LibraryName = "webrtc-apm.so";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private const string LibraryName = "webrtc-apm";
#endif 
        [DllImport(LibraryName, EntryPoint = "webrtc_apm_create")]
        public static extern IntPtr webrtc_apm_create();

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_destroy")]
        public static extern void webrtc_apm_destroy(IntPtr apm);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_config_create")]
        public static extern IntPtr webrtc_apm_config_create();

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_config_destroy")]
        public static extern void webrtc_apm_config_destroy(IntPtr config);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_config_set_echo_canceller")]
        public static extern void webrtc_apm_config_set_echo_canceller(IntPtr config, int enabled, int mobile_mode);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_config_set_noise_suppression")]
        public static extern void webrtc_apm_config_set_noise_suppression(IntPtr config, int enabled,
            NoiseSuppressionLevel level);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_config_set_gain_controller1")]
        public static extern void webrtc_apm_config_set_gain_controller1(IntPtr config, int enabled, GainControlMode mode,
            int target_level_dbfs, int compression_gain_db, int enable_limiter);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_config_set_gain_controller2")]
        public static extern void webrtc_apm_config_set_gain_controller2(IntPtr config, int enabled);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_config_set_high_pass_filter")]
        public static extern void webrtc_apm_config_set_high_pass_filter(IntPtr config, int enabled);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_config_set_pre_amplifier")]
        public static extern void webrtc_apm_config_set_pre_amplifier(IntPtr config, int enabled, float fixed_gain_factor);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_config_set_pipeline")]
        public static extern void webrtc_apm_config_set_pipeline(IntPtr config, int max_internal_rate,
            int multi_channel_render, int multi_channel_capture, DownmixMethod downmix_method);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_apply_config")]
        public static extern ApmError webrtc_apm_apply_config(IntPtr apm, IntPtr config);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_stream_config_create")]
        public static extern IntPtr webrtc_apm_stream_config_create(int sample_rate_hz, UIntPtr num_channels);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_stream_config_destroy")]
        public static extern void webrtc_apm_stream_config_destroy(IntPtr config);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_stream_config_sample_rate_hz")]
        public static extern int webrtc_apm_stream_config_sample_rate_hz(IntPtr config);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_stream_config_num_channels")]
        public static extern UIntPtr webrtc_apm_stream_config_num_channels(IntPtr config);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_processing_config_create")]
        public static extern IntPtr webrtc_apm_processing_config_create();

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_processing_config_destroy")]
        public static extern void webrtc_apm_processing_config_destroy(IntPtr config);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_processing_config_input_stream")]
        public static extern IntPtr webrtc_apm_processing_config_input_stream(IntPtr config);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_processing_config_output_stream")]
        public static extern IntPtr webrtc_apm_processing_config_output_stream(IntPtr config);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_processing_config_reverse_input_stream")]
        public static extern IntPtr webrtc_apm_processing_config_reverse_input_stream(IntPtr config);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_processing_config_reverse_output_stream")]
        public static extern IntPtr webrtc_apm_processing_config_reverse_output_stream(IntPtr config);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_initialize")]
        public static extern ApmError webrtc_apm_initialize(IntPtr apm);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_initialize_with_config")]
        public static extern ApmError webrtc_apm_initialize_with_config(IntPtr apm, IntPtr config);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_process_stream")]
        public static extern ApmError webrtc_apm_process_stream(IntPtr apm, IntPtr src, IntPtr input_config,
            IntPtr output_config, IntPtr dest);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_process_reverse_stream")]
        public static extern ApmError webrtc_apm_process_reverse_stream(IntPtr apm, IntPtr src, IntPtr input_config,
            IntPtr output_config, IntPtr dest);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_analyze_reverse_stream")]
        public static extern ApmError webrtc_apm_analyze_reverse_stream(IntPtr apm, IntPtr data, IntPtr reverse_config);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_set_stream_analog_level")]
        public static extern void webrtc_apm_set_stream_analog_level(IntPtr apm, int level);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_recommended_stream_analog_level")]
        public static extern int webrtc_apm_recommended_stream_analog_level(IntPtr apm);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_set_stream_delay_ms")]
        public static extern void webrtc_apm_set_stream_delay_ms(IntPtr apm, int delay);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_stream_delay_ms")]
        public static extern int webrtc_apm_stream_delay_ms(IntPtr apm);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_set_stream_key_pressed")]
        public static extern void webrtc_apm_set_stream_key_pressed(IntPtr apm, int key_pressed);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_set_output_will_be_muted")]
        public static extern void webrtc_apm_set_output_will_be_muted(IntPtr apm, int muted);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_set_runtime_setting_float")]
        public static extern void webrtc_apm_set_runtime_setting_float(IntPtr apm, RuntimeSettingType type, float value);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_set_runtime_setting_int")]
        public static extern void webrtc_apm_set_runtime_setting_int(IntPtr apm, RuntimeSettingType type, int value);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_proc_sample_rate_hz")]
        public static extern int webrtc_apm_proc_sample_rate_hz(IntPtr apm);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_proc_split_sample_rate_hz")]
        public static extern int webrtc_apm_proc_split_sample_rate_hz(IntPtr apm);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_num_input_channels")]
        public static extern UIntPtr webrtc_apm_num_input_channels(IntPtr apm);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_num_proc_channels")]
        public static extern UIntPtr webrtc_apm_num_proc_channels(IntPtr apm);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_num_output_channels")]
        public static extern UIntPtr webrtc_apm_num_output_channels(IntPtr apm);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_num_reverse_channels")]
        public static extern UIntPtr webrtc_apm_num_reverse_channels(IntPtr apm);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_create_aec_dump")]
        public static extern int webrtc_apm_create_aec_dump(IntPtr apm, [MarshalAs(UnmanagedType.LPStr)] string file_name,
            long max_log_size_bytes);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_detach_aec_dump")]
        public static extern void webrtc_apm_detach_aec_dump(IntPtr apm);

        [DllImport(LibraryName, EntryPoint = "webrtc_apm_get_frame_size")]
        public static extern UIntPtr webrtc_apm_get_frame_size(int sample_rate_hz);
    }
}