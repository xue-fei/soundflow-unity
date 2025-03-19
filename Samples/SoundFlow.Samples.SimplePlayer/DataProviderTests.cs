using SoundFlow.Abstracts;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Providers;

namespace SoundFlow.Samples.SimplePlayer;

internal static class DataProviderTests
{
    private static AudioEngine _audioEngine = AudioEngine.Instance;
    public static void Run()
    {
        Console.WriteLine("SoundFlow DataProvider Tests\n");

        // Create a test audio file.  We'll use an oscillator to generate it.
        const string testFilePath = "test_audio.mp3";

        // Test each data provider
        TestAssetDataProvider(testFilePath);
        TestChunkedDataProvider(testFilePath);
        TestStreamDataProvider(testFilePath);
        TestNetworkDataProvider();  // Test with a known good URL (you'll need to fill this in).
        TestMicrophoneDataProvider(); // Test Microphone, only checks if it runs

        Console.WriteLine("\nDataProvider Tests Finished. Press any key to exit.");
        Console.ReadKey();
    }

    private static void TestDataProviderCommon(ISoundDataProvider dataProvider, Capability capability = Capability.Playback)
    {
        Console.WriteLine($"Testing {dataProvider.GetType().Name} (CanSeek: {dataProvider.CanSeek})...");
        
        var soundPlayer = new SoundPlayer(dataProvider);
        Mixer.Master.AddComponent(soundPlayer);
        soundPlayer.Play();
        Thread.Sleep(5000);
        soundPlayer.Pause();

        if (dataProvider.CanSeek)
        {
           
            var seekTimes = new List<float> { 30f, 60f, 90f };
            foreach (var seekTime in seekTimes)
            {

                Console.WriteLine($"  Seeking to {seekTime} seconds...");
                soundPlayer.Seek(seekTime);
                soundPlayer.Play();
                Thread.Sleep(5000);
                if (soundPlayer.State != PlaybackState.Stopped)
                    soundPlayer.Pause();
                if (soundPlayer.Time < seekTime)
                    Console.WriteLine($"  ERROR: Seek failed.  Expected time >= {seekTime}, got {soundPlayer.Time}");
            }
        }
        else
        {
            Console.WriteLine(" Skipping seek tests (not supported).");
        }

        // Attempt to play to end.
        Console.WriteLine(dataProvider.Length != 0 ? "  Playing to end..." : "  Playing live stream for 5 seconds...");
        soundPlayer.Play();
        if (dataProvider.Length != 0)
        {
            while(soundPlayer.State != PlaybackState.Stopped)
            {
                Thread.Sleep(100);
            }
        }
        else
        {
            Thread.Sleep(5000);
            soundPlayer.Stop();
        }

        Mixer.Master.RemoveComponent(soundPlayer);
    }

    private static void TestAssetDataProvider(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var dataProvider = new AssetDataProvider(stream);
        TestDataProviderCommon(dataProvider);
    }

    private static void TestChunkedDataProvider(string filePath)
    {
        using var dataProvider = new ChunkedDataProvider(filePath);
        TestDataProviderCommon(dataProvider);
    }

    private static void TestStreamDataProvider(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var dataProvider = new StreamDataProvider(stream);
        TestDataProviderCommon(dataProvider);
    }


    private static void TestNetworkDataProvider()
    {
        const string testUrl = "https://prod-54-159-73-9.amperwave.net/ppm-jazz24mp3-ibc1?session-id=2aa8db63f95f9308fb155ac53cd83f2c";
        Console.WriteLine($"Testing with URL: {testUrl}");

        try
        {
            var dataProvider = new NetworkDataProvider(testUrl);
            TestDataProviderCommon(dataProvider);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: NetworkDataProvider test failed: {ex.Message}");
        }
    }

    private static void TestMicrophoneDataProvider()
    {
        Console.WriteLine("Testing MicrophoneDataProvider (5-second capture)...");

        // Switch to mixed mode for microphone + playback
        _audioEngine.Dispose();
        _audioEngine = new MiniAudioEngine(44100, Capability.Mixed);

        try
        {
            using var microphoneDataProvider = new MicrophoneDataProvider();
            var soundPlayer = new SoundPlayer(microphoneDataProvider);
            Mixer.Master.AddComponent(soundPlayer);

            microphoneDataProvider.StartCapture();
            soundPlayer.Play();

            Thread.Sleep(5000); // Capture/play for 5 seconds

            microphoneDataProvider.StopCapture();
            soundPlayer.Stop();

            Mixer.Master.RemoveComponent(soundPlayer);
            Console.WriteLine(" Microphone test completed (check for audio output).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: MicrophoneDataProvider test failed: {ex.Message}");
        }
        finally
        {
            // switch back to Playback mode
            _audioEngine.Dispose();
            _audioEngine = new MiniAudioEngine(44100, Capability.Playback);
        }
    }
}