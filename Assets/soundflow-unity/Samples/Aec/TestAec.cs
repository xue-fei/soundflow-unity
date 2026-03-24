using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Extensions.WebRtc.Apm;
using SoundFlow.Extensions.WebRtc.Apm.Modifiers;
using SoundFlow.Providers;
using SoundFlow.Structs;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;
using DeviceType = SoundFlow.Enums.DeviceType;

/// <summary>
/// AEC 主组件。
///
/// 信号流：
///   [Unity AudioSource] → FMOD → OnAudioFilterRead(FarendCapture) → FarendQueue
///                                                                          ↓
///   [麦克风] → CaptureDevice → MicrophoneDataProvider → SoundPlayer → ApmModifier
///                                                                    ↑
///                                                         FarendBridgeModifier
///                                                         从 FarendQueue 消费
///                                                         并调用 ProcessReverseStream
///
/// aecLatencyMs 估算：
///   实测回声延迟 ~10ms + OnAudioFilterRead 缓冲 ~21ms(@48kHz/1024帧) ≈ 31ms
///   从 30 开始，每次 ±5ms 微调，残留回声→调大，出现失真→调小。
/// </summary>
public class TestAec : MonoBehaviour
{
    // F32 + Mono + 16kHz：WebRTC APM 原生格式
    private static readonly AudioFormat AecFormat = new AudioFormat
    {
        Format = SampleFormat.F32,
        Channels = 1,
        SampleRate = 16000
    };

    private const int SampleRate = 16000;
    private const int PeriodFrames = SampleRate / 100; // 160 frames = 10ms @ 16kHz

    MiniAudioEngine audioEngine;
    FullDuplexDevice fullDuplexDevice;
    MicrophoneDataProvider microphoneDataProvider;
    SoundPlayer micPlayer;
    WebRtcApmModifier apmModifier;
    FarendBridgeModifier farendBridge;

    void Start()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            Permission.RequestUserPermission(Permission.Microphone);

        // 确认场景中 AudioListener 上挂了 FarendCapture
        if (FindObjectOfType<FarendCapture>() == null)
            Debug.LogError("[AEC] 未找到 FarendCapture 组件，请将其挂到 AudioListener 所在的 GameObject！");

        audioEngine = new MiniAudioEngine();

        var deviceConfig = new MiniAudioDeviceConfig
        {
            PeriodSizeInFrames = PeriodFrames,
            Playback = new DeviceSubConfig { ShareMode = ShareMode.Shared },
            Capture = new DeviceSubConfig { ShareMode = ShareMode.Shared },
#if UNITY_ANDROID || UNITY_IOS
            Wasapi = new WasapiSettings { Usage = WasapiUsage.Games }
#else
            Wasapi = new WasapiSettings { Usage = WasapiUsage.ProAudio }
#endif
        };

        audioEngine.UpdateDevicesInfo();
        var playbackInfo = SelectDeviceDefault(DeviceType.Playback);
        var captureInfo = SelectDeviceDefault(DeviceType.Capture);
        if (!playbackInfo.HasValue || !captureInfo.HasValue) return;

        fullDuplexDevice = audioEngine.InitializeFullDuplexDevice(
            playbackInfo.Value, captureInfo.Value, AecFormat, deviceConfig);
        fullDuplexDevice.Start();

        microphoneDataProvider = new MicrophoneDataProvider(fullDuplexDevice.CaptureDevice);
        micPlayer = new SoundPlayer(audioEngine, AecFormat, microphoneDataProvider);

        apmModifier = new WebRtcApmModifier(
            fullDuplexDevice,
            aecEnabled: true,
#if UNITY_ANDROID || UNITY_IOS
            aecMobileMode: true,
#else
            aecMobileMode: false,
#endif
            aecLatencyMs: 30,  // 实测回声~10ms + FMOD缓冲~21ms，从30开始调

            nsEnabled: true,
            nsLevel: NoiseSuppressionLevel.High,

            agc1Enabled: false,
            agcMode: GainControlMode.AdaptiveDigital,
            agcTargetLevel: -6,
            agcCompressionGain: 9,
            agcLimiter: true,
            agc2Enabled: true,

            hpfEnabled: true,
            preAmpEnabled: true,
            preAmpGain: 1.0f,

            useMultichannelCapture: false,
            useMultichannelRender: false,
            downmixMethod: DownmixMethod.AverageChannels
        );

        // FarendBridgeModifier 从 FarendCapture.FarendQueue 读取 Unity 播放的 PCM
        // FarendBridgeModifier 继承 SoundComponent，需传入 engine 和 format
        farendBridge = new FarendBridgeModifier(audioEngine, AecFormat, apmModifier);

        // 注意：farendBridge 加入 MasterMixer 仅用于借助 SoundFlow 的音频线程驱动
        // 它本身不产生任何声音，farend 数据来自 FarendQueue 而非 MasterMixer 的输入
        fullDuplexDevice.MasterMixer.AddComponent(farendBridge);
        fullDuplexDevice.MasterMixer.AddComponent(micPlayer);

        micPlayer.AddModifier(apmModifier);

        var recordAnalyzer = new UnityAnalyzer();
        recordAnalyzer.AudioAvailable += OnDataAec;
        micPlayer.AddAnalyzer(recordAnalyzer);

        microphoneDataProvider.StartCapture();
        micPlayer.Play();
    }

    void Update()
    {
        // 诊断：定期打印队列长度，确认 FarendCapture 在持续产出数据
        if (Time.frameCount % 300 == 0)
            Debug.Log($"[AEC] FarendQueue size: {FarendCapture.QueueCount} samples");
    }

    private DeviceInfo? SelectDeviceDefault(DeviceType type)
    {
        var devices = type == DeviceType.Playback
            ? audioEngine.PlaybackDevices
            : audioEngine.CaptureDevices;

        if (devices == null || devices.Length == 0)
        {
            Debug.LogError($"[AEC] No {type} devices found.");
            return null;
        }
        foreach (var d in devices)
            if (d.IsDefault) { Debug.Log($"[AEC] {type}: {d.Name}"); return d; }
        return devices[0];
    }

    readonly List<float> floats = new();
    void OnDataAec(float[] samples) => floats.AddRange(samples);

    void OnDestroy()
    {
        microphoneDataProvider?.StopCapture();
        micPlayer?.Stop();

        if (fullDuplexDevice != null)
        {
            fullDuplexDevice.MasterMixer.RemoveComponent(micPlayer);
            fullDuplexDevice.MasterMixer.RemoveComponent(farendBridge);
        }

        apmModifier?.Dispose();
        farendBridge?.Dispose();
        microphoneDataProvider?.Dispose();
        fullDuplexDevice?.Dispose();
        audioEngine?.Dispose();

        if (floats.Count > 0)
            Util.SaveClip(1, SampleRate, floats.ToArray(),
                Application.dataPath + "/aec_output.wav");
    }
}