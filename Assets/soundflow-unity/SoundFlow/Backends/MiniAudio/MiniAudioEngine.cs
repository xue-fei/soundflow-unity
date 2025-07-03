using SoundFlow.Abstracts;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;
using SoundFlow.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SoundFlow.Backends.MiniAudio
{
    /// <summary>
    ///     An audio engine based on the MiniAudio library.
    /// </summary>
    public sealed class MiniAudioEngine : AudioEngine
    {
        public MiniAudioEngine(
        int sampleRate,
        Capability capability,
        SampleFormat sampleFormat = SampleFormat.F32,
        int channels = 2)
        : base(sampleRate, capability, sampleFormat, channels)
        {
        }

        private Native.AudioCallback? _audioCallback;
        private nint _context;
        private nint _device = IntPtr.Zero;
        private nint _currentPlaybackDeviceId = IntPtr.Zero;
        private nint _currentCaptureDeviceId = IntPtr.Zero;

        /// <inheritdoc />
        protected override bool RequiresBackendThread { get; } = false;


        /// <inheritdoc />
        protected override void InitializeAudioDevice()
        {
            _context = Native.AllocateContext();
            var result = Native.ContextInit(IntPtr.Zero, 0, IntPtr.Zero, _context);
            if (result != Result.Success)
                throw new InvalidOperationException("Unable to init context. " + result);

            InitializeDeviceInternal(IntPtr.Zero, IntPtr.Zero);
        }


        private void InitializeDeviceInternal(nint playbackDeviceId, nint captureDeviceId)
        {
            if (_device != IntPtr.Zero)
                CleanupCurrentDevice();

            var deviceConfig = Native.AllocateDeviceConfig(Capability, SampleFormat, (uint)Channels, (uint)SampleRate,
                _audioCallback ??= AudioCallback,
                playbackDeviceId,
                captureDeviceId);

            _device = Native.AllocateDevice();
            var result = Native.DeviceInit(_context, deviceConfig, _device);
            Native.Free(deviceConfig);

            if (result != Result.Success)
            {
                Native.Free(_device);
                _device = IntPtr.Zero;
                throw new InvalidOperationException($"Unable to init device. {result}");
            }

            result = Native.DeviceStart(_device);
            if (result != Result.Success)
            {
                CleanupCurrentDevice();
                throw new InvalidOperationException($"Unable to start device. {result}");
            }

            UpdateDevicesInfo();
            CurrentPlaybackDevice = PlaybackDevices.FirstOrDefault(x => x.Id == playbackDeviceId);
            CurrentCaptureDevice = CaptureDevices.FirstOrDefault(x => x.Id == captureDeviceId);
            CurrentPlaybackDevice ??= PlaybackDevices.FirstOrDefault(x => x.IsDefault);
            CurrentCaptureDevice ??= CaptureDevices.FirstOrDefault(x => x.IsDefault);

            if (CurrentPlaybackDevice != null) _currentPlaybackDeviceId = CurrentPlaybackDevice.Value.Id;
            if (CurrentCaptureDevice != null) _currentCaptureDeviceId = CurrentCaptureDevice.Value.Id;

            UnityEngine.Debug.LogWarning(CurrentPlaybackDevice.Value.Id);
            UnityEngine.Debug.LogWarning(CurrentCaptureDevice.Value.Id);
        }

        private void CleanupCurrentDevice()
        {
            if (_device == IntPtr.Zero) return;
            _ = Native.DeviceStop(_device);
            Native.DeviceUninit(_device);
            Native.Free(_device);
            _device = IntPtr.Zero;
        }


        private void AudioCallback(IntPtr _, IntPtr output, IntPtr input, uint length)
        {
            var sampleCount = (int)length * Channels;
            if (Capability != Capability.Record) ProcessGraph(output, sampleCount);
            if (Capability != Capability.Playback) ProcessAudioInput(input, sampleCount);
        }


        /// <inheritdoc />
        protected override void ProcessAudioData() { }

        /// <inheritdoc />
        protected override void CleanupAudioDevice()
        {
            CleanupCurrentDevice();
            Native.ContextUninit(_context);
            Native.Free(_context);
        }


        /// <inheritdoc />
        public override ISoundEncoder CreateEncoder(Stream stream, EncodingFormat encodingFormat,
            SampleFormat sampleFormat, int encodingChannels, int sampleRate)
        {
            return new MiniAudioEncoder(stream, encodingFormat, sampleFormat, encodingChannels, sampleRate);
        }

        /// <inheritdoc />
        public override ISoundDecoder CreateDecoder(Stream stream)
        {
            return new MiniAudioDecoder(stream);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            CleanupAudioDevice();
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        public override void SwitchDevice(DeviceInfo deviceInfo, DeviceType type = DeviceType.Playback)
        {
            if (deviceInfo.Id == IntPtr.Zero)
                throw new InvalidOperationException("Unable to switch device. Device ID is invalid.");

            switch (type)
            {
                case DeviceType.Playback:
                    InitializeDeviceInternal(deviceInfo.Id, _currentCaptureDeviceId);
                    break;
                case DeviceType.Capture:
                    InitializeDeviceInternal(_currentPlaybackDeviceId, deviceInfo.Id);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Invalid DeviceType for SwitchDevice.");
            }
        }

        /// <inheritdoc />
        public override void SwitchDevices(DeviceInfo? playbackDeviceInfo, DeviceInfo? captureDeviceInfo)
        {
            var playbackDeviceId = _currentPlaybackDeviceId;
            var captureDeviceId = _currentCaptureDeviceId;

            if (playbackDeviceInfo != null)
            {
                if (playbackDeviceInfo.Value.Id == IntPtr.Zero)
                    throw new InvalidOperationException("Invalid Playback Device ID provided for SwitchDevices.");
                playbackDeviceId = playbackDeviceInfo.Value.Id;
            }

            if (captureDeviceInfo != null)
            {
                if (captureDeviceInfo.Value.Id == IntPtr.Zero)
                    throw new InvalidOperationException("Invalid Capture Device ID provided for SwitchDevices.");
                captureDeviceId = captureDeviceInfo.Value.Id;
            }

            InitializeDeviceInternal(playbackDeviceId, captureDeviceId);
        }


        /// <inheritdoc />
        public override void UpdateDevicesInfo()
        {
            nint pPlaybackDevices = 0;
            nint pCaptureDevices = 0;
            nint playbackDeviceCount = 0;
            nint captureDeviceCount = 0;

            var result = Native.GetDevices(_context, out pPlaybackDevices, out pCaptureDevices,
                out playbackDeviceCount, out captureDeviceCount);
            if (result != Result.Success)
            {
                throw new InvalidOperationException("Unable to get devices.");
            }
            PlaybackDeviceCount = (int)playbackDeviceCount;
            CaptureDeviceCount = (int)captureDeviceCount;

            if (pPlaybackDevices == IntPtr.Zero && pCaptureDevices == IntPtr.Zero)
            {
                PlaybackDevices = null;
                CaptureDevices = null;
                return;
            }

            PlaybackDevices = pPlaybackDevices.ReadArray<DeviceInfo>(PlaybackDeviceCount);
            CaptureDevices = pCaptureDevices.ReadArray<DeviceInfo>(CaptureDeviceCount);

            foreach (var device in PlaybackDevices)
            {
                UnityEngine.Debug.LogWarning(device);
            }

            foreach (var device in CaptureDevices)
            {
                UnityEngine.Debug.LogWarning(device);
            }

            Native.Free(pPlaybackDevices);
            Native.Free(pCaptureDevices);

            if (playbackDeviceCount == 0)
            {
                PlaybackDevices = null;
            }
            if (captureDeviceCount == 0)
            {
                CaptureDevices = null;
            }
        }
    }
}