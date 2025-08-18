using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Devices;
using SoundFlow.Backends.MiniAudio.Enums;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Extensions.WebRtc.Apm;
using SoundFlow.Structs;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeviceType = SoundFlow.Enums.DeviceType;

public class UnityAec2 : MonoBehaviour
{
    private AudioEngine audioEngine;
    AudioCaptureDevice captureDevice;
    Recorder recorder;
    FileStream stream;

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
        UnityAnalyzer unityAnalyzer = new UnityAnalyzer();
        unityAnalyzer.AudioAvailable += OnData;
        recorder.AddAnalyzer(unityAnalyzer);


        apm = new AudioProcessingModule();
        apm.SetStreamDelayMs(-1);
        apmConfig = new ApmConfig();
        apmConfig.SetEchoCanceller(true, false);
        apmConfig.SetNoiseSuppression(true, NoiseSuppressionLevel.High);
        apmConfig.SetGainController1(false, GainControlMode.AdaptiveDigital, -6, 9, true);
        apmConfig.SetGainController2(true);
        apmConfig.SetHighPassFilter(true);
        apmConfig.SetPreAmplifier(true, 1.0f);
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

    List<float> destAudio = new List<float>();
    private void OnData(float[] data)
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
    float[] temp = new float[160];
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (isPlay)
        {
            Debug.Log(data.Length);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = data[i] * 0.5f;
                farQueue.Enqueue(data[i]);
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
        isPlay = false;
    }
}