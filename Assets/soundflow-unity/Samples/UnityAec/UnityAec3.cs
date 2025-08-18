using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Extensions.WebRtc.Apm;
using SoundFlow.Interfaces;
using SoundFlow.Providers;
using SoundFlow.Structs;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeviceType = SoundFlow.Enums.DeviceType;

public class UnityAec3 : MonoBehaviour
{
    private AudioEngine audioEngine;
    AudioCaptureDevice captureDevice;
    Recorder recorder;
    FileStream stream;

    AudioPlaybackDevice playbackDevice;
    SoundPlayer soundPlayer;

    int sampleRate = 16000;
    const int numChannels = 1;
    AudioProcessingModule apm;
    ApmConfig apmConfig;
    StreamConfig inputStreamConfig;
    StreamConfig outputStreamConfig;
    bool isPlay = false;

    // Start is called before the first frame update
    void Start()
    {
        audioEngine = new MiniAudioEngine();
        AudioFormat Format = AudioFormat.Unity;
        var captureDeviceInfo = SelectDevice(DeviceType.Capture);
        if (!captureDeviceInfo.HasValue) return;
        DeviceConfig DeviceConfig = new MiniAudioDeviceConfig
        {
            PeriodSizeInFrames = 160, // 10ms at 48kHz = 480 frames @ 2 channels = 960 frames
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
        captureDevice = audioEngine.InitializeCaptureDevice(captureDeviceInfo.Value, Format, DeviceConfig);
        captureDevice.Start();

        stream = new FileStream(Application.dataPath + "/8.18.wav", FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 4096);
        recorder = new Recorder(captureDevice, stream, EncodingFormat.Wav);
        UnityAnalyzer unityAnalyzer1 = new UnityAnalyzer();
        unityAnalyzer1.AudioAvailable += OnNearData;
        recorder.AddAnalyzer(unityAnalyzer1);


        var deviceInfo = SelectDevice(DeviceType.Playback);
        if (!deviceInfo.HasValue) return;

        playbackDevice = audioEngine.InitializePlaybackDevice(deviceInfo.Value, Format, DeviceConfig);
        playbackDevice.Start();

        string filePath = Application.dataPath + "/default.wav";
        ISoundDataProvider streamDataProvider = new AssetDataProvider(audioEngine, Format, new FileStream(filePath, FileMode.Open, FileAccess.Read));
        soundPlayer = new SoundPlayer(audioEngine, Format, streamDataProvider);
        playbackDevice.MasterMixer.AddComponent(soundPlayer);
        soundPlayer.Volume = 1;

        UnityAnalyzer unityAnalyzer2 = new UnityAnalyzer();
        soundPlayer.AddAnalyzer(unityAnalyzer2);
        unityAnalyzer2.AudioAvailable += OnFarData;
        soundPlayer.Play();


        apm = new AudioProcessingModule();
        apm.SetStreamDelayMs(40);
        apmConfig = new ApmConfig();
        apmConfig.SetEchoCanceller(true, false);
        apmConfig.SetNoiseSuppression(false, NoiseSuppressionLevel.Moderate);
        apmConfig.SetGainController1(false, GainControlMode.AdaptiveDigital, -6, 9, true);
        apmConfig.SetGainController2(false);
        apmConfig.SetHighPassFilter(false);
        apmConfig.SetPreAmplifier(false, 1.0f);
        apmConfig.SetPipeline(sampleRate, false, false, DownmixMethod.UseFirstChannel);

        var applyError = apm.ApplyConfig(apmConfig);
        if (applyError != ApmError.NoError)
        {
            apm.Dispose();
            apmConfig.Dispose();
            Debug.LogError($"Failed to apply APM config: {applyError}");
        }

        inputStreamConfig = new StreamConfig(sampleRate, numChannels);
        outputStreamConfig = new StreamConfig(sampleRate, numChannels);

        var initError = apm.Initialize();
        if (initError != ApmError.NoError)
        {
            apm.Dispose();
            apmConfig.Dispose();
            inputStreamConfig.Dispose();
            outputStreamConfig.Dispose();
            Debug.LogError($"Failed to initialize APM: {initError}");
        }

        recorder.StartRecording();
        isPlay = true;
    }

    float[][] near = new float[][]
        {
            new float[160],
            new float[160]
        };
    float[][] dest = new float[][]
        {
            new float[160],
            new float[160]
        };
    float[] temp = new float[160];

    List<float> destAudio = new List<float>();
    private void OnNearData(float[] data)
    {
        if (apm == null)
        {
            return;
        }
        if (farQueue.Count >= 160)
        {
            for (int i = 0; i < temp.Length; i++)
            {
                temp[i] = farQueue.Dequeue();
            }
            far[0] = temp;
            apm.ProcessReverseStream(far, inputStreamConfig, outputStreamConfig, dest);
            near[0] = data;
            apm.ProcessStream(near, inputStreamConfig, outputStreamConfig, dest);
            destAudio.AddRange(dest[0]);
        }
    }

    float[][] far = new float[][]
        {
            new float[160],
            new float[160]
        };
    Queue<float> farQueue = new Queue<float>();

    List<float> farData = new List<float>();
    private void OnFarData(float[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = data[i];
            farQueue.Enqueue(data[i]);
        }
        farData.AddRange(data);
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
        for (var i = 0; i < devices.Length; i++)
        {
            if (devices[i].IsDefault)
            {
                return devices[i];
            }
        }
        return devices[0];
    }

    private void OnDestroy()
    {
        if (soundPlayer != null)
        {
            soundPlayer.Stop();
            playbackDevice.MasterMixer.RemoveComponent(soundPlayer);
        }

        recorder.StopRecording();
        stream.Dispose();
        captureDevice.Stop();
        captureDevice.Dispose();

        if (audioEngine != null)
        {
            audioEngine.Dispose();
            audioEngine = null;
        }

        apm.Dispose();
        apmConfig.Dispose();
        inputStreamConfig.Dispose();
        outputStreamConfig.Dispose();

        Util.SaveClip(numChannels, sampleRate, destAudio.ToArray(), Application.dataPath + "/8.18aec.wav");
        Util.SaveClip(numChannels, sampleRate, farData.ToArray(), Application.dataPath + "/8.18play.wav");
        isPlay = false;
    }
}