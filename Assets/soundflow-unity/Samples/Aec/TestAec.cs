using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Components;
using SoundFlow.Extensions.WebRtc.Apm;
using SoundFlow.Extensions.WebRtc.Apm.Modifiers;
using SoundFlow.Providers;
using SoundFlow.Structs;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;
using DeviceType = SoundFlow.Enums.DeviceType;

public class TestAec : MonoBehaviour
{
    MiniAudioEngine audioEngine;
    AudioCaptureDevice captureDevice;
    AudioPlaybackDevice playbackDevice;
    MicrophoneDataProvider microphoneDataProvider;
    SoundPlayer micPlayer;
    WebRtcApmModifier apmModifier;

    // Start is called before the first frame update
    void Start()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }
        audioEngine = new MiniAudioEngine();
        AudioFormat Format = AudioFormat.Unity;
        var captureDeviceInfo = SelectDeviceDefault(DeviceType.Capture);
        if (!captureDeviceInfo.HasValue) return;
        DeviceConfig DeviceConfig = new MiniAudioDeviceConfig
        {
            PeriodSizeInFrames = 160, // 10ms at 48kHz = 480 frames @ 2 channels = 960 frames
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
//#if UNITY_ANDROID || UNITY_IOS
                Usage = WasapiUsage.Games
//#else
//                Usage = WasapiUsage.ProAudio // Use ProAudio mode for lower latency on Windows
//#endif
            }
        };
        captureDevice = audioEngine.InitializeCaptureDevice(captureDeviceInfo.Value, Format, DeviceConfig);
        captureDevice.Start();

        microphoneDataProvider = new MicrophoneDataProvider(captureDevice);
        micPlayer = new SoundPlayer(audioEngine, Format, microphoneDataProvider);

        var deviceInfo = SelectDeviceDefault(DeviceType.Playback);
        if (!deviceInfo.HasValue) return;

        playbackDevice = audioEngine.InitializePlaybackDevice(deviceInfo.Value, Format, DeviceConfig);
        playbackDevice.Start();

        apmModifier = new WebRtcApmModifier(playbackDevice,
           // Echo Cancellation (AEC) settings
           aecEnabled: true,
//#if UNITY_ANDROID || UNITY_IOS
           aecMobileMode: true,
//#else
//           aecMobileMode: false, // Desktop mode is generally more robust
//#endif
           aecLatencyMs: -1,     // Estimated system latency for AEC (tune this)

           // Noise Suppression (NS) settings
           nsEnabled: true,
           nsLevel: NoiseSuppressionLevel.High,

           // Automatic Gain Control (AGC) - Version 1 (legacy)
           agc1Enabled: false,
           agcMode: GainControlMode.AdaptiveDigital,
           agcTargetLevel: -6,   // Target level in dBFS (0 is max, typical is -3 to -18)
           agcCompressionGain: 9, // Only for FixedDigital mode
           agcLimiter: true,

           // Automatic Gain Control (AGC) - Version 2 (newer, often preferred)
           agc2Enabled: true, // Set to true to use AGC2, potentially disable AGC1

           // High Pass Filter (HPF)
           hpfEnabled: true,

           // Pre-Amplifier
           preAmpEnabled: true,
           preAmpGain: 1.0f,

           // Pipeline settings for multi-channel audio (if numChannels > 1)
           useMultichannelCapture: false, // Process capture (mic) as mono/stereo as configured by AudioEngine
           useMultichannelRender: false,  // Process render (playback for AEC) as mono/stereo
           downmixMethod: DownmixMethod.UseFirstChannel // Method if downmixing is needed
       );
        micPlayer.AddModifier(apmModifier);

        UnityAnalyzer unityAnalyzer = new UnityAnalyzer();
        unityAnalyzer.AudioAvailable += OnDataAec;
        micPlayer.AddAnalyzer(unityAnalyzer);

        playbackDevice.MasterMixer.AddComponent(micPlayer);

        microphoneDataProvider.StartCapture();

        micPlayer.Play();
    }

    // Update is called once per frame
    void Update()
    {

    }

    /// <summary>
    /// Prompts the user to select a single device from a list.
    /// </summary>
    private DeviceInfo? SelectDeviceDefault(DeviceType type)
    {
        audioEngine.UpdateDevicesInfo();
        DeviceInfo[] devices = null;
        if (type == DeviceType.Playback)
        {
            devices = audioEngine.PlaybackDevices;
        }
        if (type == DeviceType.Capture)
        {
            devices = audioEngine.CaptureDevices; 
        }
        if (devices.Length == 0)
        {
            Debug.LogError($"No {type.ToString().ToLower()} devices found.");
            return null;
        }
        for (var i = 0; i < devices.Length; i++)
        {
            var device = devices[i];
            if (device.IsDefault)
            {
                Debug.Log("device.Name:" + device.Name);
                return device;
            }
        }
        return devices[0];
    }

    List<float> floats = new List<float>();
    private void OnDataAec(float[] samples)
    {
        Debug.Log(samples.Length);
        floats.AddRange(samples);
    }

    private void OnDestroy()
    {
        microphoneDataProvider.StopCapture();
        micPlayer.Stop();
        playbackDevice.MasterMixer.RemoveComponent(micPlayer);

        apmModifier.Dispose(); // Important to release native resources
        microphoneDataProvider.Dispose();
        audioEngine.Dispose();

        Util.SaveClip(1, 16000, floats.ToArray(), Application.persistentDataPath + "/7.30.2.wav");
    }
}