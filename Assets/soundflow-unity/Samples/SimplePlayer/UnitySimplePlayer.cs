using SoundFlow.Abstracts;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitySimplePlayer : MonoBehaviour
{
    private static AudioEngine? _audioEngine;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private static void PlayAudio(ISoundDataProvider dataProvider, bool isSurround = false,
        Action<ISoundPlayer>? configurePlayer = null, List<SoundModifier>? modifiers = null)
    {
        SetOrCreateEngine();
        SoundPlayerBase soundPlayer = isSurround ? new SurroundPlayer(dataProvider) : new SoundPlayer(dataProvider);

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

        PlaybackControls(soundPlayer);

        Mixer.Master.RemoveComponent(soundPlayer);
    }

    private static void SetOrCreateEngine(Capability capability = Capability.Playback, int sampleRate = 48000,
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

    private static void PlaybackControls(ISoundPlayer player)
    {
        var timer = new System.Timers.Timer(500) { AutoReset = true };
        timer.Elapsed += (_, _) =>
        {
            if (player.State != PlaybackState.Stopped)
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(
                    $"Time: {(int)player.Time / 60}:{player.Time % 60:00} / Duration: {(int)player.Duration / 60}:{player.Duration % 60:00}        ");
            }
            else
            {
                timer.Stop();
            }
        };
        timer.Start();

        Console.WriteLine(
            "\nPress 'S' to seek, 'P' to pause/play, any other key to exit playback. 'V' to change volume, '+' to increase speed, '-' to decrease speed, 'R' to reset speed to 1.0");


        while (player.State is PlaybackState.Playing or PlaybackState.Paused)
        {
            var keyInfo = Console.ReadKey(true);
            switch (keyInfo.Key)
            {
                case ConsoleKey.P:
                    if (player.State == PlaybackState.Playing)
                        player.Pause();
                    else
                        player.Play();
                    break;
                case ConsoleKey.S:
                    Console.WriteLine("Enter seek time in seconds (e.g., 5.0):");
                    if (float.TryParse(Console.ReadLine(), out var seekTime))
                        player.Seek(TimeSpan.FromSeconds(seekTime));
                    else
                        Console.WriteLine("Invalid seek time.");
                    break;
                case ConsoleKey.OemPlus:
                case ConsoleKey.Add:
                    {
                        player.PlaybackSpeed += 0.1f;
                        Console.WriteLine($"Speed increased to: {player.PlaybackSpeed:F2}");
                    }

                    break;
                case ConsoleKey.OemMinus:
                case ConsoleKey.Subtract:
                    if (player.PlaybackSpeed > 0.1f)
                    {
                        player.PlaybackSpeed -= 0.1f;
                        Console.WriteLine($"Speed decreased to: {player.PlaybackSpeed:F2}");
                    }

                    break;
                case ConsoleKey.R:
                    player.PlaybackSpeed = 1.0f;
                    Console.WriteLine($"Speed reset to: {player.PlaybackSpeed:F2}");
                    break;
                case ConsoleKey.V:
                    Console.WriteLine("Enter volume (e.g., 1.0):");
                    if (float.TryParse(Console.ReadLine(), out var volume))
                        player.Volume = volume;
                    else
                        Console.WriteLine("Invalid volume.");
                    break;
                default:
                    player.Stop();
                    break;
            }
        }

        timer.Stop();
        timer.Dispose();
        Console.WriteLine("Playback stopped.");
    }
}