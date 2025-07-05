using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Extensions.WebRtc.Apm;
using SoundFlow.Extensions.WebRtc.Apm.Modifiers;
using SoundFlow.Providers;
using UnityEngine;

public class TestAec : MonoBehaviour
{
    MiniAudioEngine audioEngine;
    MicrophoneDataProvider microphoneDataProvider;
    SoundPlayer micPlayer;
    WebRtcApmModifier apmModifier;

    // Start is called before the first frame update
    void Start()
    {
        audioEngine = new MiniAudioEngine(16000, Capability.Mixed, channels: 1);
        microphoneDataProvider = new MicrophoneDataProvider();
        micPlayer = new SoundPlayer(microphoneDataProvider);

        apmModifier = new WebRtcApmModifier(
           // Echo Cancellation (AEC) settings
           aecEnabled: true,
           aecMobileMode: false, // Desktop mode is generally more robust
           aecLatencyMs: 40,     // Estimated system latency for AEC (tune this)

           // Noise Suppression (NS) settings
           nsEnabled: true,
           nsLevel: NoiseSuppressionLevel.High,

           // Automatic Gain Control (AGC) - Version 1 (legacy)
           agc1Enabled: true,
           agcMode: GainControlMode.AdaptiveDigital,
           agcTargetLevel: -3,   // Target level in dBFS (0 is max, typical is -3 to -18)
           agcCompressionGain: 9, // Only for FixedDigital mode
           agcLimiter: true,

           // Automatic Gain Control (AGC) - Version 2 (newer, often preferred)
           agc2Enabled: false, // Set to true to use AGC2, potentially disable AGC1

           // High Pass Filter (HPF)
           hpfEnabled: true,

           // Pre-Amplifier
           preAmpEnabled: false,
           preAmpGain: 1.0f,

           // Pipeline settings for multi-channel audio (if numChannels > 1)
           useMultichannelCapture: false, // Process capture (mic) as mono/stereo as configured by AudioEngine
           useMultichannelRender: false,  // Process render (playback for AEC) as mono/stereo
           downmixMethod: DownmixMethod.AverageChannels // Method if downmixing is needed
       );
        micPlayer.AddModifier(apmModifier);
        Mixer.Master.AddComponent(micPlayer);
        microphoneDataProvider.StartCapture(); // If using microphone
        micPlayer.Play(); // Start processing the microphone input 
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnDestroy()
    {
        microphoneDataProvider.StopCapture();
        micPlayer.Stop();
        Mixer.Master.RemoveComponent(micPlayer);
        apmModifier.Dispose(); // Important to release native resources
        microphoneDataProvider.Dispose();
        audioEngine.Dispose();
    }
}