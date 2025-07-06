using System.Buffers;
using System.Runtime.CompilerServices;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Exceptions;
using SoundFlow.Interfaces;
using SoundFlow.Structs;
using SoundFlow.Utils;

namespace SoundFlow.Abstracts;

/// <summary>
///     This event is raised when samples are processed by Input or Output components.
/// </summary>
public delegate void AudioProcessCallback(Span<float> samples, Capability capability);

/// <summary>
///     The base class for audio engines.
///     This class provides common functionality for audio engines such as sample rate and
///     channel count management, thread management, and solo/mute functionality.
/// </summary>
public abstract class AudioEngine : IDisposable
{
    private static AudioEngine? _instance;

    private bool _soloedComponentExists;
    private SoundComponent? _soloedComponent;

    private readonly object _lock = new();

    private Thread? _audioThread;
    private readonly ManualResetEvent _audioThreadStarted = new(false);
    private readonly ManualResetEvent _stopAudioThread = new(false);
    
    /// <summary>
    /// Specifies the direction of audio data processing to guide the central processing method.
    /// </summary>
    private enum ProcessingDirection
    {
        InputFromDevice,
        OutputToDevice
    }

    /// <summary>
    ///     Constructs a new instance of <see cref="AudioEngine" />.
    /// </summary>
    /// <param name="sampleRate">The sampling rate used by the engine.</param>
    /// <param name="capability">The audio device capability.</param>
    /// <param name="sampleFormat">The sample format used by the engine.</param>
    /// <param name="channels">The number of audio channels.</param>
    protected AudioEngine(int sampleRate, Capability capability, SampleFormat sampleFormat, int channels)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        if (_instance != null)
            throw new InvalidOperationException("An instance of AudioEngine is already initialized.");

        _instance = this;
        SampleRate = sampleRate;
        InverseSampleRate = 1f / sampleRate;
        Channels = channels;
        Capability = capability;
        SampleFormat = sampleFormat;

        SetupAudioThread();
    }

    private void SetupAudioThread()
    {
        if (RequiresBackendThread)
        {
            _audioThread = new Thread(AudioThreadLoop);
            _audioThread.Start();
            _audioThreadStarted.WaitOne();
        }
        else
        {
            InitializeAudioDevice();
        }
    }

    private void AudioThreadLoop()
    {
        InitializeAudioDevice();
        _audioThreadStarted.Set();
        while (!_stopAudioThread.WaitOne(0))
        {
            ProcessAudioData();
        }
        CleanupAudioDevice();
    }

    /// <summary>
    ///     Initializes the audio device and starts the audio processing loop.
    /// </summary>
    protected abstract void InitializeAudioDevice();

    /// <summary>
    ///     Processes audio data in a loop.
    /// </summary>
    protected abstract void ProcessAudioData();

    /// <summary>
    ///     Cleans up the audio device.
    /// </summary>
    protected abstract void CleanupAudioDevice();
    
    /// <summary>
    ///     Gets the configured sample format.
    /// </summary>
    public SampleFormat SampleFormat { get; }
    
    /// <summary>
    ///     Gets the configured sample rate (samples per second).
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    ///     Gets the inverse of the configured sample rate (seconds per sample).
    /// </summary>
    public float InverseSampleRate { get; }
    
    /// <summary>
    ///     Gets the number of configured audio channels.
    /// </summary>
    public static int Channels { get; private set; } = 2;
    
    /// <summary>
    ///     Gets or sets the capability of the audio engine.
    /// </summary>
    public Capability Capability { get; }
    
    /// <summary>
    ///     Gets whether the backend requires a dedicated thread for audio processing.
    ///     True if <see cref="AudioEngine"/> manages a backend thread, false otherwise.
    ///     Subclasses override to indicate backend threading requirements.
    /// </summary>
    protected virtual bool RequiresBackendThread { get; } = true;

    /// <summary>
    ///     Gets the currently selected playback device.
    /// </summary>
    public DeviceInfo? CurrentPlaybackDevice { get; protected set; }

    /// <summary>
    ///     Gets the currently selected capture device.
    /// </summary>
    public DeviceInfo? CurrentCaptureDevice { get; protected set; }

    /// <summary>
    ///     Gets the number of available capture devices.
    /// </summary>
    public int CaptureDeviceCount { get; protected set; }
    
    /// <summary>
    ///     Gets the number of available playback devices.
    /// </summary>
    public int PlaybackDeviceCount { get; protected set; }

    /// <summary>
    ///     Gets an array of available playback devices.
    /// </summary>
    public DeviceInfo[] PlaybackDevices { get; protected set; } = [];

    /// <summary>
    ///     Gets an array of available capture devices.
    /// </summary>
    public DeviceInfo[] CaptureDevices { get; protected set; } = [];
    
    /// <summary>
    ///     Gets the audio engine instance.
    /// </summary>
    public static AudioEngine Instance => _instance ?? throw new BackendException("None", Result.NoBackend,
        "AudioEngine is not initialized yet. Create an instance of your backend first.");

    /// <summary>
    ///     Cleans up resources before the object is garbage collected.
    /// </summary>
    ~AudioEngine() => Dispose(false);

    /// <summary>
    ///     Solos the specified sound component, muting all other components.
    /// </summary>
    /// <param name="component">The component to solo.</param>
    public void SoloComponent(SoundComponent component)
    {
        lock (_lock)
        {
            _soloedComponentExists = true;
            _soloedComponent = component;
        }
    }
    
    /// <summary>
    ///     Unsolos the specified sound component.
    /// </summary>
    /// <param name="component">The component to unsolo.</param>
    public void UnsoloComponent(SoundComponent component)
    {
        lock (_lock)
        {
            if (_soloedComponent == component)
            {
                _soloedComponentExists = false;
                _soloedComponent = null;
            }
        }
    }

    /// <summary>
    ///     Processes the audio graph synchronously for playback.
    /// </summary>
    /// <param name="output">A pointer to the output buffer.</param>
    /// <param name="length">The length of the output buffer in samples.</param>
    protected void ProcessGraph(nint output, int length)
    {
        ProcessAudio(output, length, ProcessingDirection.OutputToDevice);
    }

    /// <summary>
    ///     Processes the audio input from the device.
    /// </summary>
    /// <param name="input">A pointer to the input buffer containing raw device data.</param>
    /// <param name="length">The length of the input buffer in samples.</param>
    protected void ProcessAudioInput(nint input, int length)
    {
        ProcessAudio(input, length, ProcessingDirection.InputFromDevice);
    }
    
    /// <summary>
    /// Centralized method to handle audio processing, abstracting buffer management and format conversion.
    /// </summary>
    private void ProcessAudio(nint deviceBufferPtr, int length, ProcessingDirection direction)
    {
        if (length <= 0 || deviceBufferPtr == nint.Zero)
            return;

        // Fast path for F32: process directly on the device buffer without conversion or allocation.
        if (SampleFormat == SampleFormat.F32)
        {
            var floatBuffer = Extensions.GetSpan<float>(deviceBufferPtr, length);
            if (direction == ProcessingDirection.InputFromDevice)
            {
                OnAudioProcessed?.Invoke(floatBuffer, Capability.Record);
            }
            else // OutputToDevice
            {
                lock (_lock)
                {
                    if (_soloedComponentExists)
                        _soloedComponent?.Process(floatBuffer);
                    else
                        Mixer.Master.Process(floatBuffer);
                }
                OnAudioProcessed?.Invoke(floatBuffer, Capability.Playback);
            }
            return;
        }

        // For other formats, rent a temporary float buffer for processing.
        float[] tempBuffer = ArrayPool<float>.Shared.Rent(length);
        var processBuffer = tempBuffer.AsSpan(0, length);

        try
        {
            if (direction == ProcessingDirection.InputFromDevice)
            {
                // Convert from device format to our internal float format.
                ConvertFromDeviceFormat(deviceBufferPtr, processBuffer, length);
                // Now, process the converted float data.
                OnAudioProcessed?.Invoke(processBuffer, Capability.Record);
            }
            else // OutputToDevice
            {
                // First, fill the float buffer with audio data.
                lock (_lock)
                {
                    if (_soloedComponentExists)
                        _soloedComponent?.Process(processBuffer);
                    else
                        Mixer.Master.Process(processBuffer);
                }
                OnAudioProcessed?.Invoke(processBuffer, Capability.Playback);
                // Now, convert from our float format to the device format.
                ConvertToDeviceFormat(processBuffer, deviceBufferPtr, length);
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(tempBuffer);
        }
    }

    /// <summary> Dispatches conversion from a float buffer to the appropriate device format. </summary>
    private void ConvertToDeviceFormat(Span<float> source, nint destination, int length)
    {
        switch (SampleFormat)
        {
            case SampleFormat.S16:  ConvertFloatTo<short>(source, destination, length); break;
            case SampleFormat.S32:  ConvertFloatTo<int>(source, destination, length); break;
            case SampleFormat.U8:   ConvertFloatTo<byte>(source, destination, length); break;
            case SampleFormat.S24:  ConvertFloatToS24(source, destination, length); break;
            default: throw new NotSupportedException($"Sample format {SampleFormat} is not supported for output.");
        }
    }
    
    /// <summary> Dispatches conversion from a raw device buffer to a float buffer. </summary>
    private void ConvertFromDeviceFormat(nint source, Span<float> destination, int length)
    {
        switch (SampleFormat)
        {
            case SampleFormat.S16:  ConvertFrom<short>(source, destination, length); break;
            case SampleFormat.S32:  ConvertFrom<int>(source, destination, length); break;
            case SampleFormat.U8:   ConvertFrom<byte>(source, destination, length); break;
            case SampleFormat.S24:  ConvertFromS24(source, destination, length); break;
            default: throw new NotSupportedException($"Sample format {SampleFormat} is not supported for input.");
        }
    }
    
    #region Generic Conversion Methods

    /// <summary>
    /// Converts a buffer of float samples to a specified integer PCM format and writes to a native memory location.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConvertFloatTo<T>(Span<float> floatBuffer, nint output, int length) where T : unmanaged
    {
        if (typeof(T) == typeof(byte))
        {
            var byteSpan = Extensions.GetSpan<byte>(output, length);
            for (var i = 0; i < length; i++)
            {
                var clipped = Math.Clamp(floatBuffer[i], -1f, 1f);
                byteSpan[i] = (byte)((clipped * 127.5f) + 127.5f); // Scale [-1,1] to [0,255]
            }
        }
        else if (typeof(T) == typeof(short))
        {
            var shortSpan = Extensions.GetSpan<short>(output, length);
            for (var i = 0; i < length; i++)
            {
                var clipped = Math.Clamp(floatBuffer[i], -1f, 1f);
                shortSpan[i] = (short)(clipped * short.MaxValue);
            }
        }
        else if (typeof(T) == typeof(int))
        {
            var intSpan = Extensions.GetSpan<int>(output, length);
            const double scale = int.MaxValue;
            for (var i = 0; i < length; i++)
            {
                var clipped = Math.Clamp(floatBuffer[i], -1f, 1f);
                intSpan[i] = (int)(clipped * scale);
            }
        }
        else
        {
            throw new NotSupportedException($"Unsupported output format: {typeof(T)}");
        }
        floatBuffer.Clear();
    }

    /// <summary>
    /// Converts a native buffer from a specified integer PCM format to a buffer of float samples.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConvertFrom<T>(nint input, Span<float> floatBuffer, int length) where T : unmanaged
    {
        if (typeof(T) == typeof(byte))
        {
            var byteSpan = Extensions.GetSpan<byte>(input, length);
            const float scale = 1f / 128f;
            for (var i = 0; i < length; i++)
            {
                int originalSample = byteSpan[i];
                var dither = ((float)Random.Shared.NextDouble() - (float)Random.Shared.NextDouble());
                var ditheredSample = originalSample != 0 ? originalSample + dither : originalSample;
                floatBuffer[i] = (ditheredSample - 128f) * scale;
            }
        }
        else if (typeof(T) == typeof(short))
        {
            var shortSpan = Extensions.GetSpan<short>(input, length);
            const float scale = 1f / 32767f;
            for (var i = 0; i < length; i++)
            {
                floatBuffer[i] = shortSpan[i] * scale;
            }
        }
        else if (typeof(T) == typeof(int))
        {
            var intSpan = Extensions.GetSpan<int>(input, length);
            const double scale = 1.0 / 2147483647.0;
            for (var i = 0; i < length; i++)
            {
                floatBuffer[i] = (float)(intSpan[i] * scale);
            }
        }
        else
        {
            throw new NotSupportedException($"Unsupported input format: {typeof(T)}");
        }
    }

    /// <summary>
    /// Converts a buffer of float samples to 24-bit PCM format, packing them into a native byte buffer.
    /// </summary>
    private static void ConvertFloatToS24(Span<float> floatBuffer, nint output, int length)
    {
        var outputSpan = Extensions.GetSpan<byte>(output, length * 3);
        for (int i = 0, j = 0; i < length; i++, j += 3)
        {
            var clipped = Math.Clamp(floatBuffer[i], -1f, 1f);
            var sample24 = (int)(clipped * 8388607);

            outputSpan[j] = (byte)sample24;
            outputSpan[j + 1] = (byte)(sample24 >> 8);
            outputSpan[j + 2] = (byte)(sample24 >> 16);
        }
        floatBuffer.Clear();
    }
    
    /// <summary>
    /// Converts a native 24-bit PCM byte buffer to a buffer of float samples.
    /// </summary>
    private static void ConvertFromS24(nint input, Span<float> floatBuffer, int length)
    {
        var inputSpan = Extensions.GetSpan<byte>(input, length * 3);
        const float scale = 1f / 8388607f;
        for (int i = 0, j = 0; i < length; i++, j += 3)
        {
            int sample24 = (inputSpan[j]) | (inputSpan[j + 1] << 8) | (inputSpan[j + 2] << 16);
            if ((sample24 & 0x800000) != 0)
                sample24 |= unchecked((int)0xFF000000);
            floatBuffer[i] = sample24 * scale;
        }
    }

    #endregion
    
    /// <summary>
    ///     Constructs a sound encoder specific to the implementation.
    /// </summary>
    /// <param name="stream">The stream to write encoded audio to.</param>
    /// <param name="encodingFormat">The desired audio encoding format.</param>
    /// <param name="sampleFormat">The format of the input audio samples.</param>
    /// <param name="channels">The number of audio channels.</param>
    /// <param name="sampleRate">The sample rate of the input audio.</param>
    /// <returns>An instance of a sound encoder.</returns>
    public abstract ISoundEncoder CreateEncoder(Stream stream, EncodingFormat encodingFormat,
        SampleFormat sampleFormat, int channels, int sampleRate);
    
    /// <summary>
    ///     Constructs a sound decoder specific to the implementation.
    /// </summary>
    /// <param name="stream">The stream containing the audio data.</param>
    /// <returns>An instance of a sound decoder.</returns>
    public abstract ISoundDecoder CreateDecoder(Stream stream);
    
    /// <summary>
    ///     Switches the audio engine to use the specified device.
    /// </summary>
    /// <param name="deviceInfo">The device info of the device to switch to.</param>
    /// <param name="type">The type of device.</param>
    public abstract void SwitchDevice(DeviceInfo deviceInfo, DeviceType type = DeviceType.Playback);
    
    /// <summary>
    /// Switches the audio engine to the given playback and/or capture devices.
    /// </summary>
    /// <param name="playbackDeviceInfo">The playback device to switch to. <c>null</c> to keep the current playback device.</param>
    /// <param name="captureDeviceInfo">The capture device to switch to. <c>null</c> to keep the current capture device.</param>
    public abstract void SwitchDevices(DeviceInfo? playbackDeviceInfo, DeviceInfo? captureDeviceInfo);
    
    /// <summary>
    ///     Retrieves the list of available playback and capture devices from the underlying audio backend.
    /// </summary>
    /// <remarks>
    ///     This method should be called after any changes to the audio device configuration.
    /// </remarks>
    public abstract void UpdateDevicesInfo();

    /// <summary>
    ///     Occurs when samples are processed by Input or Output components.
    /// </summary>
    public static event AudioProcessCallback? OnAudioProcessed;

    #region IDisposable Support

    /// <summary>
    ///     Gets a value indicating whether the audio engine has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    ///     Disposes of managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        if (RequiresBackendThread)
        {
            _stopAudioThread.Set();
            _audioThread?.Join();
        }
        _instance = null;
        IsDisposed = true;
    }

    /// <summary>
    ///     Disposes of the audio engine.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}