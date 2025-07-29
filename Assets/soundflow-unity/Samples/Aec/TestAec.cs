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
using System.IO;
using UnityEngine;
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

        microphoneDataProvider = new MicrophoneDataProvider(captureDevice);
        micPlayer = new SoundPlayer(audioEngine, Format, microphoneDataProvider);

        var deviceInfo = SelectDevice(DeviceType.Playback);
        if (!deviceInfo.HasValue) return;

        playbackDevice = audioEngine.InitializePlaybackDevice(deviceInfo.Value, Format, DeviceConfig);
        playbackDevice.Start();

        apmModifier = new WebRtcApmModifier(playbackDevice,
           // Echo Cancellation (AEC) settings
           aecEnabled: true,
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
        if (type == DeviceType.Capture)
        {
            return devices[1];
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

        SaveClip(1, 16000, floats.ToArray(), Application.streamingAssetsPath + "/7.29.1.wav");
    }

    private void SaveClip(int channels, int frequency, float[] data, string filePath)
    {
        using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
        {
            using (BinaryWriter writer = new BinaryWriter(fileStream))
            {
                // 写入RIFF头部标识
                writer.Write("RIFF".ToCharArray());
                // 写入文件总长度（后续填充）
                writer.Write(0);
                writer.Write("WAVE".ToCharArray());
                // 写入fmt子块
                writer.Write("fmt ".ToCharArray());
                writer.Write(16); // PCM格式块长度
                writer.Write((short)1); // PCM编码类型
                writer.Write((short)channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * 2); // 字节率
                writer.Write((short)(channels * 2)); // 块对齐
                writer.Write((short)16); // 位深度
                                         // 写入data子块
                writer.Write("data".ToCharArray());
                writer.Write(data.Length * 2); // 音频数据字节数
                                               // 写入PCM数据（float转为short）
                foreach (float sample in data)
                {
                    writer.Write((short)(sample * 32767));
                }
                // 返回填充文件总长度
                fileStream.Position = 4;
                writer.Write((int)(fileStream.Length - 8));
            }
        }
    }
}