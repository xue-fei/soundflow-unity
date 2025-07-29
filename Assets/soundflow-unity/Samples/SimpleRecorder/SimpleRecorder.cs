using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Extensions.WebRtc.Apm;
using SoundFlow.Extensions.WebRtc.Apm.Modifiers;
using SoundFlow.Structs;
using System.IO;
using UnityEngine;
using DeviceType = SoundFlow.Enums.DeviceType;

public class SimpleRecorder : MonoBehaviour
{
    private AudioEngine audioEngine;
    AudioCaptureDevice captureDevice;
    Recorder recorder;
    FileStream stream;

    // Start is called before the first frame update
    void Start()
    {
        audioEngine = new MiniAudioEngine();
        AudioFormat Format = AudioFormat.Unity;
        var captureDeviceInfo = SelectDevice(DeviceType.Capture);
        if (!captureDeviceInfo.HasValue) return;
        DeviceConfig DeviceConfig = new MiniAudioDeviceConfig
        {
            PeriodSizeInFrames = 320, // 10ms at 48kHz = 480 frames @ 2 channels = 960 frames
            Playback = new DeviceSubConfig
            {
                ShareMode = ShareMode.Shared // Use shared mode for better compatibility with other applications
            },
            Capture = new DeviceSubConfig
            {
                ShareMode = ShareMode.Shared // Use shared mode for better compatibility with other applications
            },
            Wasapi = new WasapiSettings
            {
                Usage = WasapiUsage.ProAudio // Use ProAudio mode for lower latency on Windows
            }
        };
        captureDevice = audioEngine.InitializeCaptureDevice(captureDeviceInfo.Value, Format, DeviceConfig);
        captureDevice.Start();

        stream = new FileStream(Application.streamingAssetsPath + "/output_recording.wav", FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 4096);
        recorder = new Recorder(captureDevice, stream, EncodingFormat.Wav);

        WebRtcApmModifier apmModifier = new WebRtcApmModifier(captureDevice,
           // Echo Cancellation (AEC) settings
           aecEnabled: false,
           aecMobileMode: false, // Desktop mode is generally more robust
           aecLatencyMs: 40,     // Estimated system latency for AEC (tune this)

           // Noise Suppression (NS) settings
           nsEnabled: true,
           nsLevel: NoiseSuppressionLevel.High,

           // Automatic Gain Control (AGC) - Version 1 (legacy)
           agc1Enabled: true,
           agcMode: GainControlMode.AdaptiveDigital,
           agcTargetLevel: -3,   // Target level in dBFS (0 is max, typical is -3 to -18)
           agcCompressionGain: 9, // Only for FixedDigital mode
           agcLimiter: true,

           // Automatic Gain Control (AGC) - Version 2 (newer, often preferred)
           agc2Enabled: false, // Set to true to use AGC2, potentially disable AGC1

           // High Pass Filter (HPF)
           hpfEnabled: true,

           // Pre-Amplifier
           preAmpEnabled: false,
           preAmpGain: 1.0f,

           // Pipeline settings for multi-channel audio (if numChannels > 1)
           useMultichannelCapture: false, // Process capture (mic) as mono/stereo as configured by AudioEngine
           useMultichannelRender: false,  // Process render (playback for AEC) as mono/stereo
           downmixMethod: DownmixMethod.AverageChannels // Method if downmixing is needed
       );

        recorder.AddModifier(apmModifier);
        recorder.StartRecording();
    }

    // Update is called once per frame
    void Update()
    {

    }

    /// <summary>
    /// Prompts the user to select a single device from a list.
    /// </summary>
    private DeviceInfo? SelectDevice(DeviceType type)
    {
        audioEngine.UpdateDevicesInfo();
        var devices = type == DeviceType.Playback ? audioEngine.PlaybackDevices : audioEngine.CaptureDevices;

        if (devices.Length == 0)
        {
            Debug.Log($"No {type.ToString().ToLower()} devices found.");
            return null;
        }

        Debug.Log($"\nPlease select a {type.ToString().ToLower()} device:");
        for (var i = 0; i < devices.Length; i++)
        {
            Debug.Log($"  {i}: {devices[i].Name} {(devices[i].IsDefault ? "(Default)" : "")}");
        }
        return devices[0];
    }

    private void OnDestroy()
    {
        recorder.StopRecording();
        stream.Dispose();
        captureDevice.Stop();
        captureDevice.Dispose();

        if (audioEngine != null)
        {
            audioEngine.Dispose();
            audioEngine = null;
        }
    }
}