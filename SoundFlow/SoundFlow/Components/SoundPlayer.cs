using SoundFlow.Abstracts;
using SoundFlow.Interfaces;

namespace SoundFlow.Components
{
    /// <summary>
    /// A sound player that plays audio from a data provider.
    /// </summary>
    public sealed class SoundPlayer : SoundPlayerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SoundPlayer"/> class.
        /// </summary>
        /// <param name="dataProvider">The data provider for audio content.</param>
        public SoundPlayer(ISoundDataProvider dataProvider) : base(dataProvider)
        {
        }

        /// <inheritdoc />
        public override string Name { get; set; } = "Sound Player";
    }
}