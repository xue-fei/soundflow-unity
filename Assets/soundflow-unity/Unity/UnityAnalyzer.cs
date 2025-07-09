using SoundFlow.Abstracts;
using System;

public class UnityAnalyzer : AudioAnalyzer
{
    /// <inheritdoc />
    public override string Name { get; set; } = "Unity Analyzer";

    /// <summary>
    /// Event that is raised when audio data has been analyzed.
    /// Subscribers will receive a read-only span of the audio buffer.
    /// </summary>
    public event Action<float[]> AudioAvailable;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackAnalyzer"/> class.
    /// Note: This analyzer does not use the IVisualizer, so it is ignored.
    /// </summary>
    public UnityAnalyzer() : base(null)
    {
    }

    /// <summary>
    /// Raises the AudioAvailable event, passing the audio buffer to any subscribers.
    /// </summary>
    /// <param name="buffer">The audio buffer to be passed to subscribers.</param>
    protected override void Analyze(Span<float> buffer)
    {
        // Raise the event, notifying any subscribers and passing them the data.
        // We pass it as a ReadOnlySpan to prevent subscribers from modifying the original buffer.
        AudioAvailable?.Invoke(buffer.ToArray());
    }
}