using UnityEngine;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Providers;
using SoundFlow.Enums;
using System.IO;

public class Test : MonoBehaviour
{
    MiniAudioEngine audioEngine;
    SoundPlayer player;

    // Start is called before the first frame update
    void Start()
    {
        // Initialize the audio engine with the MiniAudio backend
        // Ensure a sample rate compatible with WebRTC APM (8k, 16k, 32k, or 48k Hz) if using the APM extension.
        audioEngine = new MiniAudioEngine(48000, Capability.Playback);

        // Create a SoundPlayer and load an audio file
        player = new SoundPlayer(new StreamDataProvider(File.OpenRead(Application.streamingAssetsPath+ "/audio.wav")));

        // Add the player to the master mixer
        Mixer.Master.AddComponent(player);

        // Start playback
        player.Play();
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnDestroy()
    {
        player.Stop();
        Mixer.Master.RemoveComponent(player);
        // Dispose the audio engine when the game object is destroyed
        audioEngine.Dispose();
    }
}