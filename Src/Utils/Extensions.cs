using System.Numerics;
using System.Runtime.InteropServices;
using SoundFlow.Enums;

namespace SoundFlow.Utils;

/// <summary>
///     Extension methods.
/// </summary>
public static class Extensions
{
    /// <summary>
    ///     Gets the size of a single sample in bytes for this sample format.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="sampleFormat" /> is invalid.</exception>
    /// <returns>The size of a single sample in bytes.</returns>
    public static int GetBytesPerSample(this SampleFormat sampleFormat)
    {
        return sampleFormat switch
        {
            SampleFormat.U8 => 1,
            SampleFormat.S16 => 2,
            SampleFormat.S24 => 3,
            SampleFormat.S32 => 4,
            SampleFormat.F32 => 4,
            SampleFormat.Unknown => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(sampleFormat), "Invalid SampleFormat")
        };
    }

    /// <summary>
    ///     Gets a <see cref="Span{T}" /> for a given pointer and length.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the span.</typeparam>
    /// <param name="ptr">The pointer to the first element of the span.</param>
    /// <param name="length">The number of elements in the span.</param>
    /// <returns>A <see cref="Span{T}" /> for the given pointer and length.</returns>
    public static unsafe Span<T> GetSpan<T>(nint ptr, int length) where T : unmanaged
    {
        return new Span<T>((void*)ptr, length);
    }
}