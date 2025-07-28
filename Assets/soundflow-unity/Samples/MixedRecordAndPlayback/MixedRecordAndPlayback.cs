using SoundFlow.Abstracts;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using UnityEngine;

public class MixedRecordAndPlayback : MonoBehaviour
{
    AudioEngine audioEngine;
    MicrophoneDataProvider microphoneDataProvider;
    SoundPlayer soundPlayer;

    // Start is called before the first frame update
    void Start()
    {
        audioEngine = new MiniAudioEngine(16000, Capability.Mixed, SampleFormat.F32, 1);
        microphoneDataProvider = new MicrophoneDataProvider();
        soundPlayer = new SoundPlayer(microphoneDataProvider);

        // Add sound player to the master mixer
        Mixer.Master.AddComponent(soundPlayer);

        // Start capturing audio from the microphone and play it
        microphoneDataProvider.StartCapture();
        soundPlayer.Play();
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnDestroy()
    {

    }

    private void OnApplicationQuit()
    {
        // Stop capturing and playing
        microphoneDataProvider.StopCapture();
        soundPlayer.Stop();
        Mixer.Master.RemoveComponent(soundPlayer);
        microphoneDataProvider.Dispose();
        audioEngine.Dispose();
    }
}