using SoundFlow.Components;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SoundFlow.Abstracts
{

    /// <summary>
    ///     Base class for audio processing components.
    /// </summary>
    public abstract class SoundComponent
    {
        private static readonly ArrayPool<float> BufferPool = ArrayPool<float>.Shared;

        // Connection state
        private readonly List<SoundComponent> _inputs = new List<SoundComponent>();
        private readonly List<SoundComponent> _outputs = new List<SoundComponent>();
        private readonly object _connectionsLock = new();

        // Processing state
        private readonly List<SoundModifier> _modifiers = new List<SoundModifier>();
        private readonly List<AudioAnalyzer> _analyzers = new List<AudioAnalyzer>();
        private float _pan = 0.5f;
        private bool _solo;
        private float _volume = 1f;
        private Vector2 _volumePanFactors;
        private Vector2 _previousVolumePanFactors;
        private readonly object _stateLock = new();

        /// <summary>
        ///     Name of the component
        /// </summary>
        public virtual string Name { get; set; } = "Component";

        /// <summary>
        ///     Parent mixer of the component
        /// </summary>
        public Mixer? Parent { get; set; } = Mixer.Master;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SoundComponent" /> class.
        /// </summary>
        protected SoundComponent()
        {
            UpdateVolumePanFactors();
            _previousVolumePanFactors = _volumePanFactors;
        }

        /// <summary>
        ///     Input connections
        /// </summary>
        public IReadOnlyList<SoundComponent> Inputs
        {
            get
            {
                lock (_connectionsLock) return new List<SoundComponent>(_inputs);
            }
        }

        /// <summary>
        ///     Output connections
        /// </summary>
        public IReadOnlyList<SoundComponent> Outputs
        {
            get
            {
                lock (_connectionsLock) return new List<SoundComponent>(_outputs);
            }
        }

        /// <summary>
        ///     Volume of the component
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the volume is negative.</exception>
        public virtual float Volume
        {
            get => _volume;
            set
            {
                //ArgumentOutOfRangeException.ThrowIfNegative(value);
                lock (_stateLock)
                {
                    _volume = value;
                    UpdateVolumePanFactors();
                }
            }
        }

        /// <summary>
        ///     Pan of the component
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the pan is outside the range [0, 1].</exception>
        public virtual float Pan
        {
            get => _pan;
            set
            {
                if (value is < 0f or > 1f) throw new ArgumentOutOfRangeException(nameof(value), "Pan must be between 0.0 and 1.0.");
                lock (_stateLock)
                {
                    _pan = value;
                    UpdateVolumePanFactors();
                }
            }
        }

        /// <summary>
        ///     Whether the component is enabled
        /// </summary>
        public virtual bool Enabled { get; set; } = true;

        /// <summary>
        ///     Whether the component is soloed
        /// </summary>
        public virtual bool Solo
        {
            get => _solo;
            set
            {
                lock (_stateLock)
                {
                    _solo = value;
                    if (_solo) AudioEngine.Instance.SoloComponent(this);
                    else AudioEngine.Instance.UnsoloComponent(this);
                }
            }
        }

        /// <summary>
        ///     Whether the component is muted
        /// </summary>
        public virtual bool Mute { get; set; }

        /// <summary>
        ///     Modifiers applied to the component
        /// </summary>
        public IReadOnlyList<SoundModifier?> Modifiers
        {
            get
            {
                lock (_stateLock) return new List<SoundModifier?>(_modifiers);
            }
        }

        /// <summary>
        ///     Analyzers applied to the component
        /// </summary>
        public IReadOnlyList<AudioAnalyzer> Analyzers
        {
            get
            {
                lock (_stateLock) return new List<AudioAnalyzer>(_analyzers);
            }
        }

        private void UpdateVolumePanFactors()
        {
            var panValue = Math.Clamp(_pan, 0f, 1f);
            _volumePanFactors = new Vector2(
                _volume * MathF.Sqrt(1f - panValue),
                _volume * MathF.Sqrt(panValue)
            );
            _previousVolumePanFactors = _volumePanFactors;
        }

        /// <summary>
        ///     Connects this component to another component.
        /// </summary>
        /// <param name="input">The component to connect to.</param>
        /// <exception cref="InvalidOperationException">Thrown if the connection would create a cycle.</exception>
        public void ConnectInput(SoundComponent input)
        {
            //ArgumentNullException.ThrowIfNull(input);
            if (input == this) throw new InvalidOperationException("Cannot connect to self");

            SoundComponent first, second;
            if (GetHashCode() < input.GetHashCode())
            {
                first = this;
                second = input;
            }
            else
            {
                first = input;
                second = this;
            }

            lock (first._connectionsLock)
                lock (second._connectionsLock)
                {
                    if (_inputs.Contains(input)) return;

                    if (IsReachable(input, this))
                        throw new InvalidOperationException("Connection would create a cycle");

                    _inputs.Add(input);
                    input._outputs.Add(this);
                }
        }

        /// <summary>
        ///     Disconnects this component from another component.
        /// </summary>
        /// <param name="input">The component to disconnect from.</param>
        public void DisconnectInput(SoundComponent input)
        {
            lock (_connectionsLock)
            {
                if (!_inputs.Remove(input)) return;

                lock (input._connectionsLock)
                    input._outputs.Remove(this);
            }
        }

        private static bool IsReachable(SoundComponent start, SoundComponent target)
        {
            var visited = new HashSet<SoundComponent>();
            var queue = new Queue<SoundComponent>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == target) return true;
                if (!visited.Add(current)) continue;

                List<SoundComponent> currentOutputs;
                lock (current._connectionsLock)
                    currentOutputs = current._outputs;

                foreach (var output in currentOutputs)
                    queue.Enqueue(output);
            }

            return false;
        }

        /// <summary>
        ///     Adds a modifier to the component.
        /// </summary>
        /// <param name="modifier">The modifier to add.</param>
        public void AddModifier(SoundModifier modifier)
        {
            lock (_stateLock)
            {
                if (!_modifiers.Contains(modifier))
                    _modifiers.Add(modifier);
            }
        }

        /// <summary>
        ///     Removes a modifier from the component.
        /// </summary>
        /// <param name="modifier">The modifier to remove.</param>
        public void RemoveModifier(SoundModifier modifier)
        {
            lock (_stateLock)
                _modifiers.Remove(modifier);
        }

        /// <summary>
        ///     Adds an analyzer to the component.
        /// </summary>
        /// <param name="analyzer">The analyzer to add.</param>
        public void AddAnalyzer(AudioAnalyzer analyzer)
        {
            lock (_stateLock)
            {
                if (!_analyzers.Contains(analyzer))
                    _analyzers.Add(analyzer);
            }
        }

        /// <summary>
        ///     Removes an analyzer from the component.
        /// </summary>
        /// <param name="analyzer">The analyzer to remove.</param>
        public void RemoveAnalyzer(AudioAnalyzer analyzer)
        {
            lock (_stateLock)
                _analyzers.Remove(analyzer);
        }

        internal void Process(Span<float> outputBuffer)
        {
            if (!Enabled || Mute) return;

            float[]? rentedBuffer = null;
            try
            {
                rentedBuffer = BufferPool.Rent(outputBuffer.Length);
                var workingBuffer = rentedBuffer.AsSpan(0, outputBuffer.Length);
                workingBuffer.Clear();

                SoundComponent[] currentInputs = null;
                lock (_connectionsLock)
                {
                    if(_inputs.Count != 0)
                    {
                        currentInputs = _inputs.ToArray();
                    } 
                }
                if (currentInputs != null)
                {
                    foreach (var input in currentInputs)
                    {
                        input.Process(workingBuffer);
                    }
                }

                GenerateAudio(workingBuffer);

                SoundModifier[] currentModifiers = null;
                AudioAnalyzer[] currentAnalyzers = null;
                Vector2 currentVolumePan;

                lock (_stateLock)
                {
                    if(_modifiers.Count !=0)
                    {
                        currentModifiers = _modifiers.ToArray();
                    }
                    if (_analyzers.Count != 0)
                    {
                        currentAnalyzers = _analyzers.ToArray();
                    }
                     
                    currentVolumePan = _volumePanFactors;
                    _previousVolumePanFactors = _volumePanFactors;
                }
                if(currentModifiers!=null)
                {
                    foreach (var modifier in currentModifiers)
                    {
                        if (modifier.Enabled)
                        {
                            modifier.Process(workingBuffer);
                        }
                    }
                }
                 
                ApplyVolumeAndPanning(workingBuffer, currentVolumePan);

                MixBuffers(workingBuffer, outputBuffer);

                if (currentAnalyzers != null)
                {
                    foreach (var analyzer in currentAnalyzers)
                    {
                        analyzer.Process(workingBuffer);
                    }
                }
            }
            finally
            {
                if (rentedBuffer != null)
                {
                    BufferPool.Return(rentedBuffer);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MixBuffers(Span<float> source, Span<float> destination)
        {
            if (source.Length != destination.Length)
                throw new ArgumentException("Source and destination buffers must have the same length.");

            // SIMD-accelerated mixing
            var count = 0;
            var simdLength = source.Length - (source.Length % Vector<float>.Count);

            // Ensure there's enough data for SIMD operations
            if (simdLength > 0 && Vector.IsHardwareAccelerated)
            {
                // Rent temporary arrays from the pool to hold the slice data
                float[] tempSourceArray = ArrayPool<float>.Shared.Rent(Vector<float>.Count);
                float[] tempDestArray = ArrayPool<float>.Shared.Rent(Vector<float>.Count);

                try
                {
                    while (count < simdLength)
                    {
                        // Copy the current slice of the source Span into the temporary source array
                        source.Slice(count, Vector<float>.Count).CopyTo(tempSourceArray);
                        // Copy the current slice of the destination Span into the temporary dest array
                        destination.Slice(count, Vector<float>.Count).CopyTo(tempDestArray);

                        // Now create Vector<float> instances from the float[] arrays
                        var vs = new Vector<float>(tempSourceArray);
                        var vd = new Vector<float>(tempDestArray);

                        // Perform the SIMD operation and copy the result back to the temporary dest array
                        (vd + vs).CopyTo(tempDestArray);

                        // Copy the processed data from the temporary dest array back to the original destination Span
                        tempDestArray.AsSpan(0, Vector<float>.Count).CopyTo(destination.Slice(count, Vector<float>.Count));

                        count += Vector<float>.Count;
                    }
                }
                finally
                {
                    // Always return the rented arrays to the pool
                    ArrayPool<float>.Shared.Return(tempSourceArray);
                    ArrayPool<float>.Shared.Return(tempDestArray);
                }
            }

            // Scalar remainder
            while (count < source.Length)
            {
                destination[count] += source[count];
                count++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyVolumeAndPanning(Span<float> buffer, Vector2 volumePan)
        {
            switch (AudioEngine.Channels)
            {
                case 1:
                    // Constant power calculation for mono
                    var monoGain = MathF.Sqrt(volumePan.X * volumePan.X + volumePan.Y * volumePan.Y);
                    ApplyMonoVolume(buffer, monoGain);
                    break;
                case 2:
                    ApplyStereoVolume(buffer, volumePan);
                    break;
                default:
                    ApplyMultiChannelVolume(buffer, AudioEngine.Channels, volumePan);
                    break;
            }
        }

        private static void ApplyMonoVolume(Span<float> buffer, float volume)
        {
            if (Math.Abs(volume - 1f) < 1e-6f) return;

            if (Vector.IsHardwareAccelerated && buffer.Length >= Vector<float>.Count)
            {
                var vecVolume = new Vector<float>(volume);
                var count = 0;

                // Rent a temporary array for the slice
                float[] tempBufferSlice = ArrayPool<float>.Shared.Rent(Vector<float>.Count);

                try
                {
                    for (; count <= buffer.Length - Vector<float>.Count; count += Vector<float>.Count)
                    {
                        // 1. Copy current Span slice to the temporary array
                        buffer.Slice(count, Vector<float>.Count).CopyTo(tempBufferSlice);

                        // 2. Create Vector<float> from the float[] (tempBufferSlice)
                        var vec = new Vector<float>(tempBufferSlice);

                        // 3. Perform SIMD operation and write result back to the temporary array
                        (vec * vecVolume).CopyTo(tempBufferSlice);

                        // 4. Copy processed data from temporary array back to the original Span
                        tempBufferSlice.AsSpan(0, Vector<float>.Count).CopyTo(buffer.Slice(count, Vector<float>.Count));
                    }
                }
                finally
                {
                    // Ensure the temporary array is returned to the pool
                    ArrayPool<float>.Shared.Return(tempBufferSlice);
                }

                // Scalar remainder (unchanged, as it doesn't use Vector<float> constructor with Span)
                for (; count < buffer.Length; count++)
                {
                    buffer[count] *= volume;
                }
            }
            else
            {
                for (var i = 0; i < buffer.Length; i++)
                {
                    buffer[i] *= volume;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyStereoVolume(Span<float> buffer, Vector2 volume)
        {
            // Early exit for unity volume (both channels at 1.0)
            if (Math.Abs(volume.X - 1f) < 1e-7f && Math.Abs(volume.Y - 1f) < 1e-7f)
            {
                return;
            }
            var volX = volume.X;
            var volY = volume.Y;

            var i = 0;

            if (Vector.IsHardwareAccelerated && buffer.Length >= Vector<float>.Count)
            {
                var vectorSize = Vector<float>.Count;

                if (vectorSize % 2 == 0)
                {
                    Span<float> gainFactorsSpan = stackalloc float[vectorSize]; // stackalloc is fine as it's small and temporary
                    for (var k = 0; k < vectorSize; k += 2)
                    {
                        gainFactorsSpan[k] = volX;
                        gainFactorsSpan[k + 1] = volY;
                    }
                    var simdGainFactors = new Vector<float>(gainFactorsSpan); // This constructor takes Span, which is fine for stackalloc'd Span<float>

                    // Rent a temporary array for the audio slice
                    float[] tempAudioSlice = ArrayPool<float>.Shared.Rent(vectorSize);

                    try
                    {
                        for (; i <= buffer.Length - vectorSize; i += vectorSize)
                        {
                            // 1. Copy current Span slice to the temporary array
                            buffer.Slice(i, vectorSize).CopyTo(tempAudioSlice);

                            // 2. Create Vector<float> from the float[] (tempAudioSlice)
                            var audioSimd = new Vector<float>(tempAudioSlice);

                            // 3. Perform SIMD operation and write result back to the temporary array
                            (audioSimd * simdGainFactors).CopyTo(tempAudioSlice);

                            // 4. Copy processed data from temporary array back to the original Span
                            tempAudioSlice.AsSpan(0, vectorSize).CopyTo(buffer.Slice(i, vectorSize));
                        }
                    }
                    finally
                    {
                        // Ensure the temporary array is returned to the pool
                        ArrayPool<float>.Shared.Return(tempAudioSlice);
                    }
                }
            }

            // Scalar processing for the remainder or if SIMD is not applicable/enabled
            for (; i <= buffer.Length - 2; i += 2)
            {
                buffer[i] *= volX;
                buffer[i + 1] *= volY;
            }

            // Handle the last odd element if the buffer length is odd
            if (i < buffer.Length)
            {
                buffer[i] *= (volX + volY) * 0.5f;
            }
        }

        private static void ApplyMultiChannelVolume(Span<float> buffer, int channels, Vector2 volumePan)
        {
            if (channels < 2) return;

            var weights = new float[channels];
            weights[0] = volumePan.X;
            weights[1] = volumePan.Y;
            var avg = (volumePan.X + volumePan.Y) * 0.5f;

            for (var i = 2; i < channels; i++)
                weights[i] = avg;

            for (var i = 0; i < buffer.Length; i++)
                buffer[i] *= weights[i % channels];
        }

        /// <summary>
        ///     Generates audio data for the component.
        /// </summary>
        /// <param name="buffer">The buffer to write audio data to.</param>
        protected abstract void GenerateAudio(Span<float> buffer);
    }
}