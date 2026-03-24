using SoundFlow.Abstracts;
using SoundFlow.Extensions.WebRtc.Apm.Modifiers;
using SoundFlow.Structs;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 挂在 FullDuplexDevice.MasterMixer 上的无声 SoundComponent。
///
/// 继承 SoundComponent（而非 SoundModifier），因为 Mixer.AddComponent
/// 只接受 SoundComponent 子类。
///
/// GenerateAudio 在 SoundFlow 音频线程每帧被调用，不向 buffer 写入任何数据
/// （静音透传），只负责从 FarendCapture.FarendQueue 读取 Unity FMOD 输出的
/// PCM，组装成 10ms 帧后调用 APM 内部的 ProcessReverseStream，使 AEC
/// 的回声参考模型在 nearend（麦克风）处理前完成更新。
/// </summary>
public class FarendBridgeModifier : SoundComponent
{
    public override string Name { get; set; } = "FarendBridge";

    private readonly WebRtcApmModifier _apmModifier;

    // ── 反射缓存 ────────────────────────────────────────────────────────
    private bool _ready;

    private object _apmLockObj;
    private nint _apmNativePtr;
    private float[][] _deinterleavedFarend;
    private nint[] _farendChannelPtrs;
    private nint _farendChannelArrayPtr;
    private nint _dummyReverseOutputArrayPtr;
    private nint _reverseInCfgPtr;
    private nint _reverseOutCfgPtr;
    private int _numChannels;
    private int _frameSizePerChannel;

    private MethodInfo _miProcessReverseStream;

    // ── 跨帧样本累积（处理队列样本数与 APM 帧长不对齐）───────────────
    private float[] _accumBuf;
    private int _accumPos;

    public FarendBridgeModifier(AudioEngine engine, AudioFormat format,
        WebRtcApmModifier apmModifier) : base(engine, format)
    {
        _apmModifier = apmModifier;
    }

    // ── 延迟反射初始化：等 APM 内部完成初始化后才缓存字段 ───────────
    private bool EnsureReady()
    {
        if (_ready) return true;

        var t = typeof(WebRtcApmModifier);
        const BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags bfs = BindingFlags.NonPublic | BindingFlags.Static;

        var fiInit = t.GetField("_isApmSuccessfullyInitialized", bf);
        if (fiInit == null || !(bool)fiInit.GetValue(_apmModifier))
            return false; // APM 尚未就绪，下帧再试

        try
        {
            // 本地辅助：从 _apmModifier 读取私有字段
            T Get<T>(string name) => (T)t.GetField(name, bf)!.GetValue(_apmModifier);

            _numChannels = Get<int>("_numChannels");
            _frameSizePerChannel = Get<int>("_apmFrameSizePerChannel");
            _apmLockObj = Get<object>("_apmLock");
            _deinterleavedFarend = Get<float[][]>("_deinterleavedFarendApmFrame");
            _farendChannelPtrs = Get<nint[]>("_farendChannelPtrs");
            _farendChannelArrayPtr = Get<nint>("_farendChannelArrayPtr");
            _dummyReverseOutputArrayPtr = Get<nint>("_dummyReverseOutputChannelArrayPtr");

            // AudioProcessingModule.NativePtr
            var apmNativeWrapper = Get<object>("_apm");
            _apmNativePtr = GetNativePtr(apmNativeWrapper, "[FarendBridge] _apm");

            // StreamConfig.NativePtr
            var revIn = Get<object>("_reverseInputStreamConfig");
            var revOut = Get<object>("_reverseOutputStreamConfig");
            _reverseInCfgPtr = GetNativePtr(revIn, "[FarendBridge] _reverseInputStreamConfig");
            _reverseOutCfgPtr = GetNativePtr(revOut, "[FarendBridge] _reverseOutputStreamConfig");

            // NativeMethods.ProcessReverseStream（静态私有）
            var nmType = typeof(WebRtcApmModifier).Assembly
                .GetType("SoundFlow.Extensions.WebRtc.Apm.NativeMethods");
            if (nmType == null)
            {
                // 找不到时打印程序集内所有类型名辅助诊断
                var allTypes = string.Join("", System.Linq.Enumerable.Select(
                    typeof(WebRtcApmModifier).Assembly.GetTypes(), t2 => t2.FullName));
                Debug.LogError($"[FarendBridge] NativeMethods 类未找到，程序集内所有类型: { allTypes} ");
                return false;
            }
            _miProcessReverseStream = nmType.GetMethod("ProcessReverseStream", bfs);
            if (_miProcessReverseStream == null)
            {
                var allMethods = string.Join(", ", System.Linq.Enumerable.Select(
                    nmType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
                    m => m.Name));
                Debug.LogError($"[FarendBridge] ProcessReverseStream 未找到，NativeMethods 内方法: {allMethods}");
                return false;
            }
            Debug.Log($"[FarendBridge] 找到 ProcessReverseStream: {_miProcessReverseStream}");

            // 帧累积缓冲（Mono，大小 = frameSizePerChannel）
            _accumBuf = new float[_numChannels * _frameSizePerChannel];
            _accumPos = 0;

            _ready = true;
            Debug.Log($"[FarendBridge] 初始化成功  channels={_numChannels}  frameSize={_frameSizePerChannel}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FarendBridge] 反射初始化异常: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    private static nint GetNativePtr(object obj, string debugName = "")
    {
        if (obj == null)
        {
            Debug.LogError($"[FarendBridge] GetNativePtr: {debugName} is null");
            return IntPtr.Zero;
        }
        var type = obj.GetType();
        // 打印所有属性名用于诊断
        var allProps = string.Join(", ", System.Linq.Enumerable.Select(
            type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            p => p.Name));
        Debug.Log($"[FarendBridge] {debugName} type={type.Name}  props=[{allProps}]");

        // 尝试多个可能的属性名
        foreach (var propName in new[] { "NativePtr", "Handle", "Ptr", "Pointer" })
        {
            var prop = type.GetProperty(propName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
            {
                Debug.Log($"[FarendBridge] {debugName} found property: {propName}");
                return (nint)prop.GetValue(obj);
            }
        }
        // 也尝试字段
        foreach (var fieldName in new[] { "NativePtr", "_nativePtr", "Handle", "_handle", "Ptr", "_ptr" })
        {
            var field = type.GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                Debug.Log($"[FarendBridge] {debugName} found field: {fieldName}");
                return (nint)field.GetValue(obj);
            }
        }
        Debug.LogError($"[FarendBridge] {debugName} (type={type.Name}): NativePtr not found in props or fields");
        return IntPtr.Zero;
    }

    // ── 核心音频回调 ─────────────────────────────────────────────────
    /// <summary>
    /// SoundFlow 音频线程每帧调用。buffer 不写入任何数据（静音）。
    /// 从 FarendCapture.FarendQueue 消费 Unity 播放 PCM，
    /// 凑满 10ms 帧后立即调用 ProcessReverseStream。
    /// </summary>
    protected override void GenerateAudio(Span<float> buffer, int channels)
    {
        // 静音：不向 buffer 写入数据，不影响 MasterMixer 的其他组件输出
        buffer.Clear();

        if (!EnsureReady()) return;

        var queue = FarendCapture.FarendQueue;
        int totalFrame = _numChannels * _frameSizePerChannel;

        while (true)
        {
            // 从队列补充样本到累积缓冲
            while (_accumPos < totalFrame && queue.TryDequeue(out var s))
                _accumBuf[_accumPos++] = s;

            // 不足一帧则等下次调用
            if (_accumPos < totalFrame) break;

            // ① 反交织（当前是 Mono，numChannels=1，此步骤等价于直接拷贝）
            for (int ch = 0; ch < _numChannels; ch++)
            {
                if (_deinterleavedFarend[ch] == null ||
                    _deinterleavedFarend[ch].Length != _frameSizePerChannel)
                    _deinterleavedFarend[ch] = new float[_frameSizePerChannel];

                for (int i = 0; i < _frameSizePerChannel; i++)
                    _deinterleavedFarend[ch][i] = _accumBuf[i * _numChannels + ch];
            }

            // ② 拷贝到 APM 的非托管内存
            for (int ch = 0; ch < _numChannels; ch++)
                Marshal.Copy(_deinterleavedFarend[ch], 0,
                    _farendChannelPtrs[ch], _frameSizePerChannel);

            // ③ 调用 ProcessReverseStream（与 nearend ProcessStream 共享同一把锁）
            lock (_apmLockObj)
            {
                _miProcessReverseStream.Invoke(null, new object[]
                {
                    _apmNativePtr,
                    _farendChannelArrayPtr,
                    _reverseInCfgPtr,
                    _reverseOutCfgPtr,
                    _dummyReverseOutputArrayPtr
                });
            }

            _accumPos = 0;
        }
    }
}