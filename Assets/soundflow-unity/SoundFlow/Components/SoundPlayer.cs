using SoundFlow.Abstracts;
using SoundFlow.Interfaces;
using SoundFlow.Modifiers;
using SoundFlow.Structs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SoundFlow.Components
{
    public sealed class SoundPlayer : SoundPlayerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SoundPlayer"/> class.
        /// </summary>
        /// <param name="engine">The audio engine used for managing audio playback.</param>
        /// <param name="format">The format of the audio stream, including sample rate, sample format, and channel count.</param>
        /// <param name="dataProvider">The data provider that supplies audio data for playback.</param>
        public SoundPlayer(AudioEngine engine, AudioFormat format, ISoundDataProvider dataProvider) : base(engine, format, dataProvider) { }

        /// <inheritdoc />
        public override string Name { get; set; } = "Sound Player";
    }
}