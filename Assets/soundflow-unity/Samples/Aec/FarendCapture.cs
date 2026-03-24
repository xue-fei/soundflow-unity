using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// 挂在 AudioListener 所在的 GameObject 上（通常是主摄像机）。
///
/// OnAudioFilterRead 在 FMOD 混音完成后、PCM 送到声卡之前被调用，
/// 是 Unity 内唯一能截获真实播放信号的时机。
///
/// 本类负责：
///   1. 把 Stereo/48kHz 的 FMOD 输出降混为 Mono/16kHz
///   2. 写入线程安全队列，供 FarendBridgeModifier 在 SoundFlow 音频线程消费
///
/// 重采样原理（48kHz Stereo → 16kHz Mono）：
///   Step1: Stereo → Mono：左右声道取均值
///   Step2: 48kHz → 16kHz：每 3 个样本取均值（box filter 均值降采样）
///          48000/16000 = 3，是精确整数比，box filter 第一零点恰好在 16kHz，
///          天然抑制混叠，语音质量足够。
///
/// 注意：若 Unity Project Settings > Audio > System Sample Rate 不是 48000，
///   需要修改 SrcSampleRate 常量，或改用动态读取 AudioSettings.outputSampleRate。
/// </summary>
[RequireComponent(typeof(AudioListener))]
public class FarendCapture : MonoBehaviour
{
    // Unity 输出采样率（Project Settings > Audio > System Sample Rate）
    // 0 表示跟随系统，实际值通过 AudioSettings.outputSampleRate 运行时读取
    public static int SrcSampleRate { get; private set; }

    // APM 需要的目标采样率
    public const int DstSampleRate = 16000;

    // 线程安全队列：FarendBridgeModifier 在 SoundFlow 音频线程读取
    // 容量上限防止内存无限增长（约 2 秒缓冲）
    private const int MaxQueueSamples = DstSampleRate * 2;
    public static readonly ConcurrentQueue<float> FarendQueue = new ConcurrentQueue<float>();

    // 降采样状态：跨 OnAudioFilterRead 调用保持余量样本
    private float _monoAccum;   // 当前 box 窗口的累积值
    private int _accumCount;  // 当前 box 窗口已累积的样本数
    private int _ratio;       // 降采样比（48000/16000=3）

    // 诊断用：记录实际队列长度供外部 Debug
    public static int QueueCount => _queueCount;
    private static int _queueCount;

    void Awake()
    {
        SrcSampleRate = AudioSettings.outputSampleRate;
        if (SrcSampleRate <= 0) SrcSampleRate = 48000;

        _ratio = SrcSampleRate / DstSampleRate;
        if (SrcSampleRate % DstSampleRate != 0)
        {
            Debug.LogWarning(
                $"[FarendCapture] 采样率 {SrcSampleRate} 不能被 {DstSampleRate} 整除，" +
                $"将使用最近整数比 {_ratio}，可能有轻微音调误差。");
        }
        Debug.Log($"[FarendCapture] SrcRate={SrcSampleRate} DstRate={DstSampleRate} Ratio=1:{_ratio}");
    }

    /// <summary>
    /// Unity 音频线程回调，buffer 是 FMOD 混音后即将送声卡的 PCM（interleaved）。
    /// 不修改 buffer 内容，保持透明，不影响实际播放。
    /// </summary>
    void OnAudioFilterRead(float[] buffer, int channels)
    {
        // 队列过长时丢弃旧数据，避免延迟累积
        // （正常情况下 SoundFlow 消费速度与 Unity 产生速度匹配，不会积压）
        while (FarendQueue.Count > MaxQueueSamples)
            FarendQueue.TryDequeue(out _);

        int sampleCount = buffer.Length / channels;

        for (int i = 0; i < sampleCount; i++)
        {
            // Step1: Stereo → Mono（所有声道取均值）
            float mono = 0f;
            for (int ch = 0; ch < channels; ch++)
                mono += buffer[i * channels + ch];
            mono /= channels;

            // Step2: box filter 均值降采样
            _monoAccum += mono;
            _accumCount++;

            if (_accumCount >= _ratio)
            {
                FarendQueue.Enqueue(_monoAccum / _ratio);
                _monoAccum = 0f;
                _accumCount = 0;
            }
        }

        _queueCount = FarendQueue.Count;
    }

    void OnDestroy()
    {
        // 清空队列，避免残留数据干扰下次启动
        while (FarendQueue.TryDequeue(out _)) { }
    }
}