﻿using SoundFlow.Abstracts;

namespace SoundFlow.Modifiers;

/// <summary>
/// Boosts treble frequencies using a resonant high-pass filter.
/// </summary>
public class TrebleBoosterModifier : SoundModifier
{
    private readonly float[] _hpState;
    private readonly float[] _previousInput;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrebleBoosterModifier"/> class.
    /// </summary>
    /// <param name="cutoff">The cutoff frequency of the high-pass filter.</param>
    /// <param name="boostGain">The gain of the boost.</param>
    public TrebleBoosterModifier(float cutoff = 4000f, float boostGain = 6f)
    {
        Cutoff = Math.Min(20000, cutoff);
        BoostGain = MathF.Pow(10, boostGain / 20f);
        _hpState = new float[AudioEngine.Channels];
        _previousInput = new float[AudioEngine.Channels];
    }
    
    /// <summary>
    /// Gets or sets the gain of the treble boost.
    /// </summary>
    public float BoostGain { get; set; }

    /// <summary>
    /// Gets or sets the cutoff frequency of the high-pass filter.
    /// </summary>
    public float Cutoff { get; set; }

    /// <inheritdoc />
    public override float ProcessSample(float sample, int channel)
    {
        // 1-pole high-pass with resonance
        var dt = AudioEngine.Instance.InverseSampleRate;
        var rc = 1f / (2 * MathF.PI * Cutoff);
        var alpha = rc / (rc + dt);

        // High-pass filter
        var hp = alpha * (_hpState[channel] + sample - _previousInput[channel]);
        _hpState[channel] = hp;
        _previousInput[channel] = sample;

        // Boost and mix
        return sample + hp * BoostGain;
    }
}