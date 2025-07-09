using SoundFlow.Abstracts;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Extensions.WebRtc.Apm;
using SoundFlow.Extensions.WebRtc.Apm.Modifiers;
using SoundFlow.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class NewTest : MonoBehaviour
{
    AudioEngine audioEngine;
    Recorder recorder; 
    WebRtcApmModifier apmModifier;
    SoundPlayer audioPlayer;
    List<float> floats = new List<float>();
    List<float> floatsaec = new List<float>();

    // Start is called before the first frame update
    void Start()
    {
        audioEngine = new MiniAudioEngine(16000, Capability.Mixed, SampleFormat.F32, 1); 
        recorder = new Recorder(OnData, SampleFormat.F32, EncodingFormat.Wav, 16000, 1); 
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

        string filePath = Application.streamingAssetsPath + "/test.wav";
        StreamDataProvider streamDataProvider = new StreamDataProvider(new FileStream(filePath, FileMode.Open, FileAccess.Read));
        audioPlayer = new SoundPlayer(streamDataProvider);

        audioPlayer.AddModifier(apmModifier);
        UnityAnalyzer unityAnalyzer = new UnityAnalyzer();
        unityAnalyzer.AudioAvailable += OnDataAec;
        //recorder.AddModifier(apmModifier);
        recorder.AddAnalyzer(unityAnalyzer);

        Mixer.Master.AddComponent(audioPlayer);
        
        audioPlayer.Play();
        recorder.StartRecording();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnData(Span<float> samples, Capability capability)
    {
        if (capability == Capability.Record)
        { 
            floats.AddRange(samples.ToArray());
        }
    }

    void OnDataAec(float[] data)
    {
        floatsaec.AddRange(data);
    }

    private void OnDestroy()
    {
        audioPlayer.RemoveModifier(apmModifier);
        audioPlayer.Stop();

        //recorder.RemoveModifier(apmModifier);
        recorder.StopRecording();
        recorder.Dispose();

        Mixer.Master.RemoveComponent(audioPlayer);
        audioEngine.Dispose();

        SaveClip(1, 16000, floats.ToArray(), Application.streamingAssetsPath + "/7.9.1.wav");
        SaveClip(1, 16000, floatsaec.ToArray(), Application.streamingAssetsPath + "/7.9.1.aec.wav");
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