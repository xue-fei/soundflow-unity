using SoundFlow.Abstracts;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using System.IO;
using UnityEngine;

public class SimpleRecorder : MonoBehaviour
{
    private AudioEngine? _audioEngine;
    Recorder recorder;
    FileStream stream;

    // Start is called before the first frame update
    void Start()
    {
        _audioEngine = new MiniAudioEngine(44100, Capability.Record, SampleFormat.F32, 2);

        stream = new FileStream(Application.streamingAssetsPath + "/output_recording.wav", FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096);
        recorder = new Recorder(stream);
        recorder.StartRecording();
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnDestroy()
    {
        recorder.StopRecording();
        stream.Dispose();
        if (_audioEngine != null)
        {
            _audioEngine.Dispose();
            _audioEngine = null;
        }
    }
}