using SoundFlow.Extensions.WebRtc.Apm;
using UnityEngine;

public class UnityAec : MonoBehaviour
{
    AudioProcessingModule apm;
    ApmConfig apmConfig;

    // Start is called before the first frame update
    void Start()
    {
        apm = new AudioProcessingModule();
        apm.SetStreamDelayMs(40);

        apmConfig = new ApmConfig();
        apmConfig.SetEchoCanceller(true,false);
        apmConfig.SetNoiseSuppression(true, NoiseSuppressionLevel.High);
        apmConfig.SetGainController1(true, GainControlMode.AdaptiveDigital, -3, 9, true);
        apmConfig.SetGainController2(true);
         
    }

    // Update is called once per frame
    void Update()
    {

    }

     
    private void OnApplicationQuit()
    {
        
    }
}