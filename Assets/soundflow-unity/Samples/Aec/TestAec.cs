using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Extensions.WebRtc.Apm;
using SoundFlow.Extensions.WebRtc.Apm.Modifiers;
using SoundFlow.Providers;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TestAec : MonoBehaviour
{
    MiniAudioEngine audioEngine;
    MicrophoneDataProvider microphoneDataProvider;
    SoundPlayer micPlayer;
    WebRtcApmModifier apmModifier;

    // Start is called before the first frame update
    void Start()
    {
        audioEngine = new MiniAudioEngine(16000, Capability.Mixed, channels: 1);
        microphoneDataProvider = new MicrophoneDataProvider();
        micPlayer = new SoundPlayer(microphoneDataProvider);

        apmModifier = new WebRtcApmModifier(
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

        Mixer.Master.AddComponent(micPlayer);
        microphoneDataProvider.StartCapture();

        micPlayer.Play();
    }

    // Update is called once per frame
    void Update()
    {

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
        Mixer.Master.RemoveComponent(micPlayer);

        apmModifier.Dispose(); // Important to release native resources
        microphoneDataProvider.Dispose();
        audioEngine.Dispose();

        SaveClip(1, 16000, floats.ToArray(), Application.streamingAssetsPath + "/7.8.1.wav");
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