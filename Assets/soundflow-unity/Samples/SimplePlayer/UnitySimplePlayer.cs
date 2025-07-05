using SoundFlow.Abstracts;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class UnitySimplePlayer : MonoBehaviour
{
    private AudioEngine? _audioEngine;
    SoundPlayerBase soundPlayer;

    // Start is called before the first frame update
    void Start()
    {
        SetOrCreateEngine(); 
        Debug.Log(_audioEngine.PlaybackDevices[1]);
        Debug.Log(_audioEngine.CaptureDevices[0]);
        _audioEngine.SwitchDevice(_audioEngine.PlaybackDevices[1], SoundFlow.Enums.DeviceType.Playback);
        _audioEngine.SwitchDevice(_audioEngine.CaptureDevices[0], SoundFlow.Enums.DeviceType.Capture);
        PlayAudioFromFile(Application.streamingAssetsPath + "/output_recording.wav", false);
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

    private void PlayAudioFromFile(string filePath, bool isSurround)
    {
        List<SoundModifier> modifiers = new List<SoundModifier>();
        StreamDataProvider streamDataProvider = new StreamDataProvider(new FileStream(filePath, FileMode.Open, FileAccess.Read));
        PlayAudio(streamDataProvider, isSurround,
            player =>
            {
                if (isSurround && player is SurroundPlayer surroundPlayer)
                {
                    surroundPlayer.Panning = SurroundPlayer.PanningMethod.Vbap;
                    surroundPlayer.ListenerPosition = new System.Numerics.Vector2(0.9f, 0.5f);
                    surroundPlayer.SpeakerConfig = SurroundPlayer.SpeakerConfiguration.Surround71;
                }
            }, modifiers);
    }

    private void PlayAudio(ISoundDataProvider dataProvider, bool isSurround = false,
        Action<ISoundPlayer>? configurePlayer = null, List<SoundModifier>? modifiers = null)
    {
        SetOrCreateEngine();
        soundPlayer = isSurround ? new SurroundPlayer(dataProvider) : new SoundPlayer(dataProvider);

        if (modifiers != null)
        {
            foreach (var modifier in modifiers)
            {
                soundPlayer.AddModifier(modifier);
            }
        }

        Mixer.Master.AddComponent(soundPlayer);
        configurePlayer?.Invoke(soundPlayer);

        soundPlayer.Play();
        Debug.Log(soundPlayer.State);
        Debug.Log("Play");
        Debug.Log(soundPlayer.State);
    }

    private void SetOrCreateEngine(Capability capability = Capability.Playback, int sampleRate = 441000,
        SampleFormat sampleFormat = SampleFormat.F32, int channels = 2)
    {
        if (_audioEngine == null || _audioEngine.IsDisposed)
        {
            _audioEngine = new MiniAudioEngine(sampleRate, capability, sampleFormat, channels);
        }
        else if ((_audioEngine.Capability & capability) != capability || _audioEngine.SampleRate != sampleRate ||
                 _audioEngine.SampleFormat != sampleFormat || AudioEngine.Channels != channels)
        {
            _audioEngine.Dispose();
            _audioEngine = new MiniAudioEngine(sampleRate, capability, sampleFormat, channels);
        }
    }
      
    private void OnDestroy()
    {
        if (soundPlayer != null)
        {
            soundPlayer.Stop();
            Mixer.Master.RemoveComponent(soundPlayer);
        }
        if (_audioEngine != null)
        {
            _audioEngine.Dispose();
            _audioEngine = null;
        }
        Debug.Log("OnDestroy");
    }
}