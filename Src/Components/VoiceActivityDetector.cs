using SoundFlow.Abstracts;
using SoundFlow.Utils;
using System.Numerics;

namespace SoundFlow.Components;

/// <summary>
///     Voice Activity Detector (VAD) component for detecting speech in audio data.
/// </summary>
public class VoiceActivityDetector : SoundComponent
{
    private readonly int _fftSize;
    private readonly int _hopSize;
    private readonly float[] _window;
    private readonly Complex[] _fftBuffer;
    private readonly float[] _inputBuffer;
    private int _inputBufferIndex;

    // Features
    private float _spectralCentroid;
    private float _spectralFlatness;
    private float _spectralFlux;
    private float _silenceEnergy;
    private readonly float[] _previousSpectrum;

    // Thresholds
    private float _spectralCentroidThreshold;
    private float _spectralFlatnessThreshold;
    private float _spectralFluxThreshold;
    private float _energyThreshold;

    // Smoothing
    private int _speechFrames;
    private int _silenceFrames;
    private int _hangoverFrames;
    private int _attackFrames;
    private readonly float _alpha;
    private readonly float _energyAdaptationAlpha;

    // Dynamic Hangover and Attack
    private float _estimatedNoiseLevel;
    private float _snrEstimate;
    private readonly float _noiseAdaptationRate = 0.05f; // Controls how quickly the noise estimate adapts
    private readonly float _snrAdaptationRate = 0.05f;

    private readonly int _minHangoverFrames;
    private readonly int _maxHangoverFrames;
    private readonly int _minAttackFrames;
    private readonly int _maxAttackFrames;

    private readonly float _hangoverSensitivity; // Adjusts how aggressively hangover changes with noise
    private readonly float _attackSensitivity; // Adjusts how aggressively attack changes with noise

    // Decision
    private bool _isSpeech;

    /// <inheritdoc />
    public override string Name { get; set; } = "Spectral VAD";

    /// <summary>
    /// Event triggered when the VAD decision changes.
    /// </summary>
    public event Action<bool>? SpeechDetected;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceActivityDetector"/> class.
    /// </summary>
    /// <param name="fftSize">The size of the FFT. Must be a power of 2.</param>
    /// <param name="alpha">Smoothing factor for threshold adaptation (0-1).</param>
    /// <param name="minHangoverFrames">Minimum frames to transition from speech to silence (faster).</param>
    /// <param name="maxHangoverFrames">Maximum frames to transition from speech to silence (slower).</param>
    /// <param name="minAttackFrames">Minimum frames to detect speech start (faster).</param>
    /// <param name="maxAttackFrames">Maximum frames to detect speech start (slower).</param>
    /// <param name="spectralCentroidThreshold">The initial threshold for the spectral centroid, used to detect changes in frequency content.</param>
    /// <param name="spectralFlatnessThreshold">The initial threshold for the spectral flatness, used to assess the tonality of the sound.</param>
    /// <param name="spectralFluxThreshold">The initial threshold for the spectral flux, used to measure the rate of spectral change.</param>
    /// <param name="energyThreshold">The initial threshold for the energy level, used for energy-based voice activity detection.</param>
    /// <exception cref="ArgumentException">Thrown if the FFT size is not a power of 2.</exception>
    public VoiceActivityDetector(int fftSize = 1024, int minHangoverFrames = 60, int maxHangoverFrames = 100,
        int minAttackFrames = 1, int maxAttackFrames = 8, float alpha = 0.95f,
        float spectralCentroidThreshold = 0.45f, float spectralFlatnessThreshold = 0.5f,
        float spectralFluxThreshold = 0.12f, float energyThreshold = 0.0002f)
    {
        if ((fftSize & (fftSize - 1)) != 0)
            throw new ArgumentException("FFT size must be a power of 2.");

        _fftSize = fftSize;
        _hopSize = fftSize / 4; // 75% overlap
        _window = MathHelper.HammingWindow(fftSize);
        _fftBuffer = new Complex[fftSize];
        _previousSpectrum = new float[fftSize / 2 + 1];
        _inputBuffer = new float[fftSize];
        _inputBufferIndex = 0;

        // Initial thresholds
        _spectralCentroidThreshold = spectralCentroidThreshold;
        _spectralFlatnessThreshold = spectralFlatnessThreshold; // Higher flatness threshold
        _spectralFluxThreshold = spectralFluxThreshold;
        _energyThreshold = energyThreshold;
        _energyAdaptationAlpha = 0.99f;
        _hangoverSensitivity = 1.0f;
        _attackSensitivity = 1.0f;

        _minHangoverFrames = minHangoverFrames;
        _maxHangoverFrames = maxHangoverFrames;
        _minAttackFrames = minAttackFrames;
        _maxAttackFrames = maxAttackFrames;


        _alpha = alpha;

        _isSpeech = false;
    }

    /// <summary>
    /// Gets the current VAD decision (true if speech is detected, false otherwise).
    /// </summary>
    public bool IsSpeech => _isSpeech;

    /// <inheritdoc/>
    protected override void GenerateAudio(Span<float> buffer)
    {
        var numSamples = buffer.Length;
        var bufferIndex = 0;

        // Allocate spectrum buffer outside the loop
        Span<float> spectrum = new float[_fftSize / 2 + 1];

        while (bufferIndex < numSamples)
        {
            // 1. Fill the internal buffer with enough samples for an FFT
            var samplesToCopy = Math.Min(_fftSize - _inputBufferIndex, numSamples - bufferIndex);
            for (var i = 0; i < samplesToCopy; i++)
            {
                _inputBuffer[_inputBufferIndex++] = buffer[bufferIndex++];
            }

            // 2. If we have enough samples, process a frame, else continue
            if (_inputBufferIndex != _fftSize)
                continue;

            // Apply window and calculate FFT
            for (var i = 0; i < _fftSize; i++)
            {
                _fftBuffer[i] = new Complex(_inputBuffer[i] * _window[i], 0);
            }

            MathHelper.Fft(_fftBuffer);

            // Extract magnitude spectrum        
            for (var i = 0; i <= _fftSize / 2; i++)
            {
                spectrum[i] = (float)_fftBuffer[i].Magnitude;
            }

            // Calculate features
            CalculateSpectralCentroid(spectrum);
            CalculateSpectralFlatness(spectrum);
            CalculateSpectralFlux(spectrum);

            // Update thresholds
            UpdateThresholds();

            // Adapt hangover and attack times
            AdaptHangoverAndAttack();

            // Make VAD decision
            var currentDecision = Decide(_inputBuffer);

            // Apply smoothing/hangover
            if (currentDecision)
            {
                _speechFrames++;
                _silenceFrames = 0;
            }
            else
            {
                _silenceFrames++;
                _speechFrames = 0;
            }

            var previousDecision = _isSpeech;

            if (_speechFrames > _attackFrames)
                _isSpeech = true;
            else if (_silenceFrames > _hangoverFrames)
                _isSpeech = false;

            // Trigger event if decision changed
            if (previousDecision != _isSpeech)
                SpeechDetected?.Invoke(_isSpeech);

            // Copy the current spectrum to the previous spectrum for the next frame's flux calculation
            spectrum.CopyTo(_previousSpectrum);

            // Shift the input buffer
            Array.Copy(_inputBuffer, _hopSize, _inputBuffer, 0, _fftSize - _hopSize);
            _inputBufferIndex = _fftSize - _hopSize;
        }
    }

    /// <summary>
    /// Calculates the spectral centroid.
    /// </summary>
    /// <param name="spectrum">The magnitude spectrum.</param>
    private void CalculateSpectralCentroid(Span<float> spectrum)
    {
        var sum = 0f;
        var weightedSum = 0f;
        for (var i = 0; i < spectrum.Length; i++)
        {
            sum += spectrum[i];
            weightedSum += i * spectrum[i];
        }

        _spectralCentroid = sum > 0 ? weightedSum / sum / (spectrum.Length - 1) : 0;
    }

    /// <summary>
    /// Calculates the spectral flatness.
    /// </summary>
    /// <param name="spectrum">The magnitude spectrum.</param>
    private void CalculateSpectralFlatness(Span<float> spectrum)
    {
        const float epsilon = 1e-10f; // Small constant to avoid log(0)
        var geometricMean = 0f;
        var arithmeticMean = 0f;

        foreach (var t in spectrum)
        {
            arithmeticMean += t;
            geometricMean += MathF.Log(t + epsilon);
        }

        geometricMean = MathF.Exp(geometricMean / spectrum.Length);
        arithmeticMean /= spectrum.Length;

        _spectralFlatness = (arithmeticMean > 0) ? geometricMean / arithmeticMean : 0;
    }

    /// <summary>
    /// Calculates the spectral flux.
    /// </summary>
    /// <param name="spectrum">The magnitude spectrum.</param>
    private void CalculateSpectralFlux(Span<float> spectrum)
    {
        _spectralFlux = 0;
        for (var i = 0; i < spectrum.Length; i++)
        {
            var diff = spectrum[i] - _previousSpectrum[i];
            _spectralFlux += diff * diff;
        }

        _spectralFlux = MathF.Sqrt(_spectralFlux) / spectrum.Length;
    }

    /// <summary>
    /// Updates the thresholds using a simple adaptive scheme.
    /// </summary>
    private void UpdateThresholds()
    {
        if (_isSpeech)
        {
            // Adapt thresholds during speech (slowly)
            _spectralCentroidThreshold = _alpha * _spectralCentroidThreshold + (1 - _alpha) * _spectralCentroid;
            _spectralFlatnessThreshold = _alpha * _spectralFlatnessThreshold + (1 - _alpha) * _spectralFlatness;
            _spectralFluxThreshold = _alpha * _spectralFluxThreshold + (1 - _alpha) * _spectralFlux;
        }
        else
        {
            // Adapt thresholds more quickly during silence
            _spectralCentroidThreshold = 0.8f * _spectralCentroidThreshold + 0.2f * _spectralCentroid;
            _spectralFluxThreshold = 0.8f * _spectralFluxThreshold + 0.2f * _spectralFlux;
        }

        // Adapt silence energy slowly
        var sum = 0f;
        foreach (var s in _inputBuffer) sum += s * s; // using LINQ in realtime is resource-intensive

        _silenceEnergy = _energyAdaptationAlpha * _silenceEnergy +
                         (1 - _energyAdaptationAlpha) * (sum / _inputBuffer.Length);
        _energyThreshold = 2 * _silenceEnergy;

        // Estimate noise level (using a simple moving average of silence energy)
        _estimatedNoiseLevel =
            _noiseAdaptationRate * _silenceEnergy + (1 - _noiseAdaptationRate) * _estimatedNoiseLevel;

        // Estimate SNR (simplified)
        var signalEnergy = 0f;
        foreach (var s in _inputBuffer) signalEnergy += s * s;
        signalEnergy /= _inputBuffer.Length;

        _snrEstimate = _snrAdaptationRate * (signalEnergy / (_estimatedNoiseLevel + 1e-10f)) +
                       (1 - _snrAdaptationRate) *
                       _snrEstimate; // Adding a small constant (1e-10f) to avoid division by zero
    }

    /// <summary>
    /// Adapts the hangover and attack times based on the estimated noise level and SNR.
    /// </summary>
    private void AdaptHangoverAndAttack()
    {
        // Simple linear adaptation based on SNR:
        // Higher SNR -> Shorter hangover, longer attack
        // Lower SNR -> Longer hangover, shorter attack

        // Normalize SNR to a 0-1 range
        var normalizedSnr = Math.Clamp(_snrEstimate / 20.0f, 0.0f, 1.0f);

        // Adapt hangover
        var hangoverRange = _maxHangoverFrames - _minHangoverFrames;
        _hangoverFrames = _minHangoverFrames + (int)(hangoverRange * (1 - normalizedSnr) * _hangoverSensitivity);
        _hangoverFrames = Math.Clamp(_hangoverFrames, _minHangoverFrames, _maxHangoverFrames);

        // Adapt attack
        var attackRange = _maxAttackFrames - _minAttackFrames;
        _attackFrames = _minAttackFrames + (int)(attackRange * normalizedSnr * _attackSensitivity);
        _attackFrames = Math.Clamp(_attackFrames, _minAttackFrames, _maxAttackFrames);
    }

    /// <summary>
    /// Makes a VAD decision based on the current features and thresholds.
    /// </summary>
    /// <param name="buffer">The current audio buffer (used for energy calculation).</param>
    /// <returns>True if speech is detected, false otherwise.</returns>
    private bool Decide(float[] buffer)
    {
        // 1. Calculate energy of the frame
        var energy = 0f;
        foreach (var e in buffer) // using LINQ in realtime is resource-intensive
        {
            energy += e * e;
        }

        energy /= buffer.Length;

        // 2. Energy-based decision
        if (energy < _energyThreshold)
        {
            return false;
        }

        // 3. Spectral-based decision
        var spectralDecision =
            (_spectralCentroid > _spectralCentroidThreshold && _spectralFlatness < _spectralFlatnessThreshold) ||
            (_spectralFlux > _spectralFluxThreshold && _speechFrames > 0) ||
            (_spectralCentroid > _spectralCentroidThreshold * 1.2f && _spectralFlux > _spectralFluxThreshold * 0.5f);


        return spectralDecision;
    }
}