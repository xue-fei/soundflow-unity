﻿using SoundFlow.Abstracts;

namespace SoundFlow.Modifiers;

/// <summary>
/// A sound modifier that implements a multi-channel chorus effect.
/// </summary>
public class MultiChannelChorusModifier : SoundModifier
{
    private class ChannelState(int maxDelay, float depth, float rate, float feedback)
    {
        public readonly float[] DelayLine = new float[maxDelay];
        public float LfoPhase;
        public int DelayIndex;
        public readonly float Depth = depth;
        public readonly float Rate = rate;
        public readonly float Feedback = feedback;
    }

    private readonly ChannelState[] _channels;
    private readonly float _wetMix;
    private readonly int _maxDelay;

    /// <summary>
    /// Constructs a new multi-channel chorus effect.
    /// </summary>
    /// <param name="wetMix">Wet/dry mix ratio (0.0-1.0)</param>
    /// <param name="maxDelay">Maximum delay length in samples</param>
    /// <param name="channelParameters">Array of parameters per channel</param>
    public MultiChannelChorusModifier(
        float wetMix,
        int maxDelay,
        params (float depth, float rate, float feedback)[] channelParameters)
    {
        if (channelParameters.Length != AudioEngine.Channels)
        {
            throw new ArgumentException(
                $"Expected {AudioEngine.Channels} channel parameters, got {channelParameters.Length}");
        }

        _wetMix = wetMix;
        _maxDelay = maxDelay;
        _channels = new ChannelState[AudioEngine.Channels];

        for (var i = 0; i < AudioEngine.Channels; i++)
        {
            _channels[i] = new ChannelState(
                maxDelay,
                channelParameters[i].depth,
                channelParameters[i].rate,
                channelParameters[i].feedback
            );
        }
    }

    /// <inheritdoc />
    public override void Process(Span<float> buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            var channel = i % AudioEngine.Channels;
            var state = _channels[channel];
            
            // Calculate modulated delay
            var lfo = MathF.Sin(state.LfoPhase) * state.Depth;
            var delayTime = (int)(_maxDelay / 2f + lfo);
            
            // Get delayed sample
            var readIndex = (state.DelayIndex - delayTime + _maxDelay) % _maxDelay;
            var delayed = state.DelayLine[readIndex];
            
            // Update delay line
            state.DelayLine[state.DelayIndex] = buffer[i] + delayed * state.Feedback;
            
            // Update LFO phase
            state.LfoPhase += 2 * MathF.PI * state.Rate / AudioEngine.Instance.SampleRate;
            if (state.LfoPhase > 2 * MathF.PI) state.LfoPhase -= 2 * MathF.PI;
            
            // Advance delay index
            state.DelayIndex = (state.DelayIndex + 1) % _maxDelay;
            
            // Mix wet/dry
            buffer[i] = buffer[i] * (1 - _wetMix) + delayed * _wetMix;
        }
    }

    /// <inheritdoc />
    public override float ProcessSample(float sample, int channel) => throw new NotImplementedException();
}