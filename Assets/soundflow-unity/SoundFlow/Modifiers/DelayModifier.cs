﻿using SoundFlow.Abstracts;
using SoundFlow.Structs;
using System;
using System.Collections.Generic;

namespace SoundFlow.Modifiers
{
    /// <summary>
    /// A sound modifier that implements a delay effect.
    /// </summary>
    public sealed class DelayModifier : SoundModifier
    {
        private readonly List<float[]> _delayLines;
        private readonly int[] _delayIndices;
        private readonly float[] _filterStates;
        private readonly AudioFormat _format;

        /// <summary>
        /// The feedback amount (0.0 - 1.0).
        /// </summary>
        public float Feedback { get; set; }

        /// <summary>
        /// The wet/dry mix (0.0 - 1.0).
        /// </summary>
        public float WetMix { get; set; }

        /// <summary>
        /// The cutoff frequency in Hertz.
        /// </summary>
        public float Cutoff { get; set; }

        /// <summary>
        /// Constructs a new instance of <see cref="DelayModifier"/>.
        /// </summary>
        /// <param name="format">The audio format to process.</param>
        /// <param name="delaySamples">The length of the delay line in samples.</param>
        /// <param name="feedback">The feedback amount (0.0 - 1.0).</param>
        /// <param name="wetMix">The wet/dry mix (0.0 - 1.0).</param>
        /// <param name="cutoff">The cutoff frequency in Hertz.</param>
        public DelayModifier(AudioFormat format, int delaySamples = 48000, float feedback = 0.5f,
            float wetMix = 0.3f, float cutoff = 5000f)
        {
            _format = format;
            Feedback = feedback;
            WetMix = wetMix;
            Cutoff = cutoff;

            _delayLines = new List<float[]>();
            _delayIndices = new int[_format.Channels];
            _filterStates = new float[_format.Channels];

            for (var i = 0; i < _format.Channels; i++)
            {
                _delayLines.Add(new float[delaySamples]);
            }
        }

        /// <inheritdoc />
        public override float ProcessSample(float sample, int channel)
        {
            var delayLine = _delayLines[channel];
            var index = _delayIndices[channel];

            // Get delayed sample
            var delayed = delayLine[index];

            // Apply low-pass filter to feedback
            var rc = 1f / (2 * MathF.PI * Cutoff);
            var alpha = _format.InverseSampleRate / (rc + _format.InverseSampleRate);
            delayed = alpha * delayed + (1 - alpha) * _filterStates[channel];
            _filterStates[channel] = delayed;

            // Write to delay line
            delayLine[index] = sample + delayed * Feedback;
            _delayIndices[channel] = (index + 1) % delayLine.Length;

            return sample * (1 - WetMix) + delayed * WetMix;
        }
    }
}