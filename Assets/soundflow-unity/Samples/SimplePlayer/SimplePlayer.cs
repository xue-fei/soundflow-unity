using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Components;
using SoundFlow.Interfaces;
using SoundFlow.Providers;
using SoundFlow.Structs;
using System;
using System.IO;
using UnityEngine;
using DeviceType = SoundFlow.Enums.DeviceType;

public class SimplePlayer : MonoBehaviour
{
    private AudioEngine audioEngine;
    AudioPlaybackDevice playbackDevice;
    SoundPlayer soundPlayer;

    // Start is called before the first frame update
    void Start()
    {
        audioEngine = new MiniAudioEngine();
        AudioFormat Format = AudioFormat.Unity;
        DeviceConfig DeviceConfig = new MiniAudioDeviceConfig
        {
            PeriodSizeInFrames = 960, // 10ms at 48kHz = 480 frames @ 2 channels = 960 frames
            Playback = new DeviceSubConfig
            {
                ShareMode = ShareMode.Shared // Use shared mode for better compatibility with other applications
            },
            Capture = new DeviceSubConfig
            {
                ShareMode = ShareMode.Shared // Use shared mode for better compatibility with other applications
            },
            Wasapi = new WasapiSettings
            {
                Usage = WasapiUsage.ProAudio // Use ProAudio mode for lower latency on Windows
            }
        };
        var deviceInfo = SelectDevice(DeviceType.Playback);
        if (!deviceInfo.HasValue) return;

        playbackDevice = audioEngine.InitializePlaybackDevice(deviceInfo.Value, Format, DeviceConfig);
        playbackDevice.Start();

        string filePath = Application.streamingAssetsPath + "/mix.wav";
        ISoundDataProvider streamDataProvider = new AssetDataProvider(audioEngine, Format, new FileStream(filePath, FileMode.Open, FileAccess.Read));
        soundPlayer = new SoundPlayer(audioEngine, Format, streamDataProvider);
        playbackDevice.MasterMixer.AddComponent(soundPlayer);
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

    /// <summary>
    /// Prompts the user to select a single device from a list.
    /// </summary>
    private DeviceInfo? SelectDevice(DeviceType type)
    {
        audioEngine.UpdateDevicesInfo();
        var devices = type == DeviceType.Playback ? audioEngine.PlaybackDevices : audioEngine.CaptureDevices;

        if (devices.Length == 0)
        {
            Debug.Log($"No {type.ToString().ToLower()} devices found.");
            return null;
        }

        Debug.Log($"\nPlease select a {type.ToString().ToLower()} device:");
        for (var i = 0; i < devices.Length; i++)
        {
            Debug.Log($"  {i}: {devices[i].Name} {(devices[i].IsDefault ? "(Default)" : "")}");
        } 
        return devices[1];
    }

    private void OnApplicationQuit()
    {
        if (soundPlayer != null)
        {
            soundPlayer.Stop();
            playbackDevice.MasterMixer.RemoveComponent(soundPlayer);
        }
        if (audioEngine != null)
        {
            audioEngine.Dispose();
            audioEngine = null;
        }
        Debug.Log("OnApplicationQuit");
    }
}