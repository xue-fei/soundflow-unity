using SoundFlow.Enums;
using System;

namespace SoundFlow.Exceptions
{
    /// <summary>
    ///     An exception thrown when an error occurs in an audio backend.
    /// </summary>
    public class BackendException : Exception
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="BackendException"/> class.
        /// </summary>
        /// <param name="backendName">The name of the audio backend that threw the exception.</param>
        /// <param name="result">The result returned by the audio backend.</param>
        /// <param name="message">The error message of the exception.</param>
        public BackendException(string backendName, Result result, string message)
            : base(message)
        {
            Backend = backendName;
            Result = result;
        }

        /// <summary>
        ///     Gets the name of the audio backend that threw the exception.
        /// </summary>
        public string Backend { get; }

        /// <summary>
        ///     Gets the result returned by the audio backend.
        /// </summary>
        public Result Result { get; }
    }
}