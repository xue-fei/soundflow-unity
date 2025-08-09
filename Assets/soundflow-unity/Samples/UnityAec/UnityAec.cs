using SoundFlow.Extensions.WebRtc.Apm;
using System;
using System.Collections.Generic;
using uMicrophoneWebGL;
using UnityEngine;

public class UnityAec : MonoBehaviour
{
    int sampleRate = 16000;
    const int numChannels = 1;
    public MicrophoneWebGL microphoneWebGL;
    AudioProcessingModule apm;
    ApmConfig apmConfig;
    StreamConfig inputStreamConfig;
    StreamConfig outputStreamConfig;
    bool isPlay = false;

    // Start is called before the first frame update
    void Start()
    {
        microphoneWebGL = GetComponent<MicrophoneWebGL>();
        apm = new AudioProcessingModule();
        apm.SetStreamDelayMs(-1);
        apmConfig = new ApmConfig();
        apmConfig.SetEchoCanceller(true, false);
        apmConfig.SetNoiseSuppression(false, NoiseSuppressionLevel.High);
        apmConfig.SetGainController1(true, GainControlMode.FixedDigital, 0, 0, false);
        apmConfig.SetGainController2(false);
        apmConfig.SetHighPassFilter(false);
        apmConfig.SetPreAmplifier(false, 1.0f);
        apmConfig.SetPipeline(sampleRate, false, false, DownmixMethod.AverageChannels);

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

        microphoneWebGL.dataEvent.AddListener(OnData);

        isPlay = true;
    }

    // Update is called once per frame
    void Update()
    {

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
    void OnData(float[] data)
    {
        if (apm == null)
        {
            return;
        }
        near[0] = data;
        apm.ProcessStream(near, inputStreamConfig, outputStreamConfig, dest);
        destAudio.AddRange(dest[0]);
        if (farQueue.Count >= 160)
        {
            for (int i = 0; i < temp.Length; i++)
            {
                temp[i] = farQueue.Dequeue();
            }
            far[0] = temp;
            apm.ProcessReverseStream(far, inputStreamConfig, outputStreamConfig, dest);
        }
        //Debug.Log(dest[0].Length);
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
            //apm.ProcessReverseStream()
            for (int i = 0; i < data.Length; i++)
            {
                farQueue.Enqueue(data[i]);
            }
            
        }
    }

    private void OnDestroy()
    {
        apm.Dispose();
        apmConfig.Dispose();
        inputStreamConfig.Dispose();
        outputStreamConfig.Dispose();

        Util.SaveClip(numChannels, sampleRate, destAudio.ToArray(), Application.dataPath + "/8.9.wav");
        isPlay = false;
    }
}