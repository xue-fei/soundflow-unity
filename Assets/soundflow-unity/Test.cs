using SoundFlow.Enums;
using SoundFlow.Extensions.WebRtc.Apm;
using SoundFlow.Extensions.WebRtc.Apm.Components;
using System;
using System.IO;
using UnityEngine;

public class Test : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // Create AssetDataProvider and NoiseSuppressor
        var dataProvider = new StreamDataProvider(new FileStream(filePath, FileMode.Open, FileAccess.Read));
        var noiseSuppressor = new NoiseSuppressor(
            dataProvider: dataProvider,
            sampleRate: 48000,
            numChannels: 1,
            suppressionLevel: NoiseSuppressionLevel.VeryHigh,
            useMultichannelProcessing: false
        );
        var stream = new FileStream(CleanedFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096);
        var encoder = AudioEngine.Instance.CreateEncoder(stream, EncodingFormat.Wav, SampleFormat.F32, 1, 48000);

        // Process the noisy speech file and save the cleaned audio
        Console.WriteLine("Processing noisy speech file...");

        var cleanData = noiseSuppressor.ProcessAll();
        encoder.Encode(cleanData.AsSpan());
        encoder.Dispose();
        stream.Dispose();

        Console.WriteLine($"Noise suppression applied. Cleaned audio file saved as 'cleaned-audio.wav' at {CleanedFilePath}, Press any key to exit.");
        Console.ReadLine();

        // Dispose noise suppressor and encoder
        noiseSuppressor.Dispose();
        dataProvider.Dispose();
    }

    // Update is called once per frame
    void Update()
    {

    }
}