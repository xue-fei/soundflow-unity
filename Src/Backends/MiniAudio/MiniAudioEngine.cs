using SoundFlow.Abstracts;
using SoundFlow.Enums;
using SoundFlow.Interfaces;

namespace SoundFlow.Backends.MiniAudio;

/// <summary>
///     MiniAudio implementation of <see cref="AudioEngine" />.
/// </summary>
public sealed class MiniAudioEngine(int sampleRate, Capability capability, SampleFormat sampleFormat = SampleFormat.F32, int channels = 2)
    : AudioEngine(sampleRate, capability, sampleFormat, channels)
{
    private Native.AudioCallback? _audioCallback;
    private nint _device;

    /// <inheritdoc />
    protected override bool RequiresBackendThread { get; } = false;
    
    /// <inheritdoc />
    protected override void InitializeAudioDevice()
    {
        // Allocate device config
        var deviceConfig = Native.AllocateDeviceConfig(Capability, SampleFormat, (uint)Channels, (uint)SampleRate, _audioCallback =
            (_, output, input, length) =>
            {
                var sampleCount = (int)length * Channels;
                if (Capability != Capability.Record)
                    ProcessGraph(output, sampleCount);
                if (Capability != Capability.Playback)
                    ProcessAudioInput(input, sampleCount);
            });

        // Allocate device data and initialize
        _device = Native.AllocateDevice();
        var result = Native.DeviceInit(nint.Zero, deviceConfig, _device);
        if (result != Result.Success)
            throw new InvalidOperationException("Unable to init device. " + result);

        // Free device config, device has been initialized
        Native.Free(deviceConfig);

        result = Native.DeviceStart(_device);
        if (result != Result.Success)
            throw new InvalidOperationException("Unable to start device. " + result);
    }
    
    /// <inheritdoc />
    protected override void ProcessAudioData() { } // Not used as MiniAudio supports callbacks

    /// <inheritdoc />
    protected override void CleanupAudioDevice()
    {
        GC.KeepAlive(_audioCallback);
        Native.DeviceStop(_device);
        Native.DeviceUninit(_device);
        Native.Free(_device);
    }
    
    /// <inheritdoc />
    protected internal override ISoundEncoder CreateEncoder(string filePath, EncodingFormat encodingFormat, SampleFormat sampleFormat, int encodingChannels, int sampleRate)
    {
        return new MiniAudioEncoder(filePath, encodingFormat, sampleFormat, encodingChannels, sampleRate);
    }
    
    /// <inheritdoc />
    protected internal override ISoundDecoder CreateDecoder(Stream stream)
    {
        return new MiniAudioDecoder(stream);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        CleanupAudioDevice();
        base.Dispose(disposing);
    }
}