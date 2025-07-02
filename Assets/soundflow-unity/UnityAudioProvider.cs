using SoundFlow.Enums;
using SoundFlow.Interfaces;
using System;
using UnityEngine;

namespace SoundFlow.Providers
{
    /// <summary>
    /// Provides audio data from a Unity AudioClip.
    /// </summary>
    public sealed class UnityAudioProvider : ISoundDataProvider
    {
        private readonly AudioClip _audioClip;
        private readonly float[] _audioData;
        private int _position;

        /// <inheritdoc />
        public event EventHandler<EventArgs>? EndOfStreamReached;
        /// <inheritdoc />
        public event EventHandler<PositionChangedEventArgs>? PositionChanged;

        /// <inheritdoc />
        public int Position
        {
            get => _position;
            private set
            {
                if (_position == value) return;
                _position = value;
                PositionChanged?.Invoke(this, new PositionChangedEventArgs(value));
            }
        }

        /// <inheritdoc />
        public int Length => _audioData.Length;
        /// <inheritdoc />
        public bool CanSeek => true;
        /// <inheritdoc />
        public SampleFormat SampleFormat => SampleFormat.F32;
        /// <inheritdoc />
        public int SampleRate => _audioClip.frequency;
        /// <inheritdoc />
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnityAudioProvider"/> class.
        /// </summary>
        /// <param name="audioClip">The Unity AudioClip to provide data from.</param>
        public UnityAudioProvider(AudioClip audioClip)
        {
            _audioClip = audioClip ?? throw new ArgumentNullException(nameof(audioClip));

            // Preload audio data into memory
            _audioData = new float[_audioClip.samples * _audioClip.channels];
            _audioClip.GetData(_audioData, 0);
        }

        /// <inheritdoc />
        public int ReadBytes(Span<float> buffer)
        {
            if (IsDisposed) return 0;

            var available = Length - Position;
            var count = Math.Min(buffer.Length, available);

            if (count > 0)
            {
                _audioData.AsSpan(Position, count).CopyTo(buffer);
                Position += count;
            }

            // Check if we've reached the end
            if (Position >= Length)
            {
                EndOfStreamReached?.Invoke(this, EventArgs.Empty);
            }

            return count;
        }

        /// <inheritdoc />
        public void Seek(int sampleOffset)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(UnityAudioProvider));
            if (sampleOffset < 0 || sampleOffset > Length)
                throw new ArgumentOutOfRangeException(nameof(sampleOffset), "Seek position is outside the valid range.");

            Position = sampleOffset;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            // Note: We don't own the AudioClip, so we don't dispose it
        }
    }
}