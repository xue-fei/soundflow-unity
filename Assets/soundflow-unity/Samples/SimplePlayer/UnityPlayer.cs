using SoundFlow.Abstracts;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using UnityEngine;

public class UnityPlayer : MonoBehaviour
{
    private AudioEngine audioEngine;
    SoundPlayer soundPlayer;
    public AudioClip audioClip;

    // Start is called before the first frame update
    void Start()
    {
        audioEngine = new MiniAudioEngine(16000, Capability.Playback, SampleFormat.F32, 1);
        var dataProvider = new UnityAudioProvider(audioClip);
        soundPlayer = new SoundPlayer(dataProvider);
        Mixer.Master.AddComponent(soundPlayer);
        soundPlayer.Volume = 1;
        soundPlayer.Play();
        Debug.Log(soundPlayer.State);
        Debug.Log("Play");
        Debug.Log(soundPlayer.State);
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnApplicationQuit()
    {
        if (soundPlayer != null)
        {
            soundPlayer.Stop();
            Mixer.Master.RemoveComponent(soundPlayer);
        }
        if (audioEngine != null)
        {
            audioEngine.Dispose();
            audioEngine = null;
        }
        Debug.Log("OnApplicationQuit");
    }
}