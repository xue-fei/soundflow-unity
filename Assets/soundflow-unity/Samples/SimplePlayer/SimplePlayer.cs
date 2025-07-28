using SoundFlow.Abstracts;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using System.IO;
using UnityEngine;

public class SimplePlayer : MonoBehaviour
{
    private AudioEngine audioEngine;
    SoundPlayer soundPlayer;

    // Start is called before the first frame update
    void Start()
    {
        audioEngine = new MiniAudioEngine(16000, Capability.Playback, SampleFormat.F32, 1);
        //Debug.Log(audioEngine.PlaybackDevices[3]);
        //Debug.Log(audioEngine.CaptureDevices[2]);
        //audioEngine.SwitchDevice(audioEngine.PlaybackDevices[3], SoundFlow.Enums.DeviceType.Playback);
        //audioEngine.SwitchDevice(audioEngine.CaptureDevices[2], SoundFlow.Enums.DeviceType.Capture);

        Debug.LogError(audioEngine.CurrentPlaybackDevice);
        //Debug.LogError(audioEngine.CurrentCaptureDevice);

        string filePath = Application.streamingAssetsPath + "/mix.wav";
        StreamDataProvider streamDataProvider = new StreamDataProvider(new FileStream(filePath, FileMode.Open, FileAccess.Read));
        soundPlayer = new SoundPlayer(streamDataProvider);
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
        if (soundPlayer != null)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Debug.Log(soundPlayer.Time);
            }
        }
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