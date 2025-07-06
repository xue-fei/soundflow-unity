﻿using System.Reflection;
using System.Text.Json;
using SoundFlow.Abstracts;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Editing;
using SoundFlow.Editing.Persistence;
using SoundFlow.Enums;
using SoundFlow.Modifiers;
using SoundFlow.Providers;

namespace SoundFlow.Samples.EditingMixer;

public static class PersistenceExamples
{
    private static AudioEngine? _audioEngine;
    private static readonly string ProjectSaveDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SavedProjects");
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void Run()
    {
        Console.WriteLine("\nSoundFlow Editing - Persistence Examples");
        Console.WriteLine("========================================");

        Directory.CreateDirectory(ProjectSaveDirectory);

        if (!DialogueFiles.CheckFilesExist()) Console.WriteLine("Dialogue files missing. Some examples might not work as intended for 'Save'.");
        
        try
        {
            _audioEngine = new MiniAudioEngine(24000, Capability.Playback, SampleFormat.F32, 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing audio engine: {ex.Message}");
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            return;
        }


        var running = true;
        while (running)
        {
            Console.WriteLine("\nChoose a persistence example:");
            Console.WriteLine(" 1. Create, Save, and Load a Simple Project (Consolidate Media)");
            Console.WriteLine(" 2. Create, Save, and Load a Project (Embed Small Media, No Consolidate)");
            Console.WriteLine(" 3. Load Project with Missing Media and Relink");
            Console.WriteLine(" 4. Demonstrate Dirty Flag");
            Console.WriteLine(" 0. Back to Main Menu / Exit");
            Console.Write("Enter your choice: ");

            if (int.TryParse(Console.ReadLine(), out var choice))
            {
                switch (choice)
                {
                    case 1: RunPersistenceExample(SaveAndLoadSimpleProject_Consolidate, "Save/Load Simple (Consolidate)"); break;
                    case 2: RunPersistenceExample(SaveAndLoadSimpleProject_Embed, "Save/Load Simple (Embed)"); break;
                    case 3: RunPersistenceExample(LoadWithMissingMediaAndRelink, "Load Missing & Relink"); break;
                    case 4: RunPersistenceExample(DemonstrateDirtyFlag, "Demonstrate Dirty Flag"); break;
                    case 0: running = false; break;
                    default: Console.WriteLine("Invalid choice. Please try again."); break;
                }
            }
            else
            {
                Console.WriteLine("Invalid input. Please enter a number.");
            }
        }

        _audioEngine.Dispose();
        _audioEngine = null;
        Console.WriteLine("Exited Persistence Examples.");
    }

    private static async void RunPersistenceExample(Func<Task>? exampleAsyncFunc, string exampleName)
    {
        Console.WriteLine($"\n--- Running: {exampleName} ---");
        try
        {
            if (exampleAsyncFunc != null)
                await exampleAsyncFunc.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred in {exampleName}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        Console.WriteLine($"--- Finished: {exampleName} ---");
        Console.WriteLine("Press any key to return to the menu...");
        Console.ReadKey();
        Console.Clear();
    }
    
    private static void PlayComposition(Composition composition, string message = "Playing composition...")
    {
        Console.WriteLine(message);
        if (AudioEngine.Instance.IsDisposed)
        {
            Console.WriteLine("AudioEngine not available for playback. Skipping.");
            composition.Dispose();
            return;
        }

        var player = new SoundPlayer(composition);
        Mixer.Master.AddComponent(player);
        player.Play();

        Console.WriteLine("Press 's' to stop playback early, or wait for it to finish.");
        var compositionDuration = composition.CalculateTotalDuration();
        var startTime = DateTime.Now;

        while (player.State == PlaybackState.Playing)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.S)
            {
                player.Stop();
                Console.WriteLine("Playback stopped by user.");
                break;
            }
            var elapsed = DateTime.Now - startTime;
            Console.Write($"\rPlayback time: {elapsed:mm\\:ss\\.fff} / {compositionDuration:mm\\:ss\\.fff}   ");
            Thread.Sleep(50);
            if (elapsed > compositionDuration + TimeSpan.FromSeconds(1))
            {
                player.Stop();
                break;
            }
        }
        Console.WriteLine("\nPlayback finished or stopped.");
        Mixer.Master.RemoveComponent(player);
        composition.Dispose();
    }

    private static TimeSpan Ts(string timestamp) => DemoAudio.Ts(timestamp);

    private static async Task SaveAndLoadSimpleProject_Consolidate()
    {
        var projectName = "SimpleProject_Consolidated";
        var projectFilePath = Path.Combine(ProjectSaveDirectory, $"{projectName}.sfproj");

        Console.WriteLine($"Creating composition: {projectName}");
        var compositionToSave = new Composition(projectName, targetChannels: 1) { SampleRate = 24000 };
        var track1 = new Track("Dialogue Track");
        compositionToSave.AddTrack(track1);

        // Segment 1 from Adam.wav
        if (File.Exists(DialogueFiles.AdamWavPath))
        {
            var adamProvider = new StreamDataProvider(File.OpenRead(DialogueFiles.AdamWavPath));
            track1.AddSegment(new AudioSegment(adamProvider, Ts("00:00:106"), Ts("00:05:000"), TimeSpan.Zero, "Adam Intro", ownsDataProvider: true));
        } else { Console.WriteLine("Adam.wav missing, skipping Adam segment.");}

        // Segment 2: A generated beep
        var beepDuration = TimeSpan.FromSeconds(1.5);
        var beepProvider = DemoAudio.GenerateShortBeep(beepDuration);
        track1.AddSegment(new AudioSegment(beepProvider, TimeSpan.Zero, beepDuration, Ts("00:05:000"), "Beep", ownsDataProvider: true));

        Console.WriteLine($"Saving project to: {projectFilePath} with media consolidation.");
        await CompositionProjectManager.SaveProjectAsync(compositionToSave, projectFilePath, consolidateMedia: true, embedSmallMedia: false);
        Console.WriteLine("Project saved.");
        compositionToSave.Dispose(); // Dispose original composition and its providers

        Console.WriteLine($"\nLoading project from: {projectFilePath}");
        var (loadedComposition, missingSources) = await CompositionProjectManager.LoadProjectAsync(projectFilePath);

        if (missingSources.Count != 0)
        {
            Console.WriteLine("Warning: Some media sources were missing upon load:");
            foreach (var ms in missingSources)
            {
                Console.WriteLine($" - ID: {ms.Id}, Original Path: {ms.OriginalAbsolutePath ?? "N/A"}");
            }
        }
        else
        {
            Console.WriteLine("Project loaded successfully with all media.");
        }

        PlayComposition(loadedComposition, $"Playing loaded {projectName}...");
    }

    private static async Task SaveAndLoadSimpleProject_Embed()
    {
        var projectName = "SimpleProject_Embedded";
        var projectFilePath = Path.Combine(ProjectSaveDirectory, $"{projectName}.sfproj");

        Console.WriteLine($"Creating composition: {projectName}");
        var compositionToSave = new Composition(projectName, targetChannels: 1) { SampleRate = 24000 };
        var track1 = new Track("Mixed Sources Track");
        compositionToSave.AddTrack(track1);

        // Segment 1 from Adam.wav (will be flagged as missing since we will set `consolidateMedia` to false, not embedded due to typical size)
        if (File.Exists(DialogueFiles.AdamWavPath))
        {
            var adamProvider = new StreamDataProvider(File.OpenRead(DialogueFiles.AdamWavPath));
            track1.AddSegment(new AudioSegment(adamProvider, Ts("00:00:106"), Ts("00:02:000"), TimeSpan.Zero, "Adam Short", ownsDataProvider: true));
        } else { Console.WriteLine("Adam.wav missing, skipping Adam segment."); }

        // Segment 2: A short generated beep (should be embedded)
        var beepDuration = TimeSpan.FromMilliseconds(500);
        var beepProvider = DemoAudio.GenerateShortBeep(beepDuration);
        track1.AddSegment(new AudioSegment(beepProvider, TimeSpan.Zero, beepDuration, Ts("00:02:500"), "Embedded Beep", ownsDataProvider: true));
        
        Console.WriteLine($"Saving project to: {projectFilePath} with small media embedding (no consolidation for external files).");
        await CompositionProjectManager.SaveProjectAsync(compositionToSave, projectFilePath, consolidateMedia: false, embedSmallMedia: true);
        Console.WriteLine("Project saved.");
        compositionToSave.Dispose();

        Console.WriteLine($"\nLoading project from: {projectFilePath}");
        var (loadedComposition, missingSources) = await CompositionProjectManager.LoadProjectAsync(projectFilePath);

        if (missingSources.Count != 0)
        {
            Console.WriteLine("Warning: Some media sources were missing upon load (as expected):");
            foreach (var ms in missingSources)
            {
                Console.WriteLine($" - ID: {ms.Id}, Original Path: {ms.OriginalAbsolutePath ?? "N/A"}");
            }
            
            Console.WriteLine("Project loaded successfully with embedded only media.");
        }
        else
        {
            Console.WriteLine("WARNING: Project loaded successfully with all media, which is wrong since Adam's segment was not embedded.");
        }

        PlayComposition(loadedComposition, $"Playing loaded {projectName}...");
    }


    private static async Task LoadWithMissingMediaAndRelink()
    {
        var projectName = "ProjectForRelink";
        var projectFilePath = Path.Combine(ProjectSaveDirectory, $"{projectName}.sfproj");
        var tempMissingFileDir = Path.Combine(ProjectSaveDirectory, "TempMissing");
        var originalBellaPath = DialogueFiles.BellaWavPath;
        var movedBellaPath = Path.Combine(tempMissingFileDir, Path.GetFileName(DialogueFiles.BellaWavPath));

        // 1. Create and save a project that references Bella.wav
        Console.WriteLine("Creating a project with Bella.wav...");
        var initialComposition = new Composition(projectName, targetChannels: 1) { SampleRate = 24000 };
        var track = new Track("Bella's Track");
        initialComposition.AddTrack(track);

        if (File.Exists(originalBellaPath))
        {
            var bellaProvider = new StreamDataProvider(File.OpenRead(originalBellaPath));
            track.AddSegment(new AudioSegment(bellaProvider, Ts("00:00:094"), Ts("00:05:000"), TimeSpan.Zero, "Bella Initial", ownsDataProvider: true));
            await CompositionProjectManager.SaveProjectAsync(initialComposition, projectFilePath, consolidateMedia: false, embedSmallMedia: false);
            Console.WriteLine($"Project saved to {projectFilePath}");
            initialComposition.Dispose();
        }
        else
        {
            Console.WriteLine("Bella.wav not found. Cannot run relink example. Skipping.");
            return;
        }

        // 2. "Move" Bella.wav to simulate missing media
        Directory.CreateDirectory(tempMissingFileDir);
        if (File.Exists(movedBellaPath)) File.Delete(movedBellaPath);
        File.Move(originalBellaPath, movedBellaPath);
        Console.WriteLine($"Simulating missing media: Moved {Path.GetFileName(originalBellaPath)} to {tempMissingFileDir}");

        // 3. Load the project - Bella.wav should be missing
        Console.WriteLine($"\nLoading project {projectFilePath} (expecting missing media)...");
        var (loadedComposition, missingSources) = await CompositionProjectManager.LoadProjectAsync(projectFilePath);

        ProjectSourceReference? bellaSourceRef = null;
        if (missingSources.Count != 0)
        {
            Console.WriteLine("Missing media found as expected:");
            foreach (var ms in missingSources)
            {
                Console.WriteLine($" - ID: {ms.Id}, Expected Path: {ms.OriginalAbsolutePath ?? "N/A"}");
                bellaSourceRef = ms; // Only Bella should be missing, we don't store the original path in the current implementation since we mostly rely on streams
            }
        }
        else
        {
            Console.WriteLine("No missing media found. Relink example might not work as intended.");
            PlayComposition(loadedComposition, "Playing loaded project (no missing media)...");
            if (File.Exists(movedBellaPath)) File.Move(movedBellaPath, originalBellaPath);
            return;
        }

        // 4. Attempt to play - segment should be silent or placeholder
        PlayComposition(loadedComposition, "Playing project with missing media (expect silence/placeholder for Bella)...");

        // 5. Relink
        if (bellaSourceRef != null)
        {
            Console.WriteLine($"\nAttempting to relink Bella's audio. Pointing to: {movedBellaPath}");
            var projectDir = Path.GetDirectoryName(projectFilePath) ?? "";
            
            var relinkSuccess = CompositionProjectManager.RelinkMissingMediaAsync(
                bellaSourceRef,
                movedBellaPath,
                projectDir
            );
            
            bellaSourceRef.ResolvedDataProvider?.Dispose(); // Dispose the resolved data provider to free up resources


            if (relinkSuccess)
            {
                Console.WriteLine("Relink reported success. Reloading project to see changes...");

                Console.WriteLine("After relinking the reference, if we were to save and reload, " +
                                  "or if the load process could use updated references, it should work.");
                Console.WriteLine("Attempting to reload the project with the now-correct original path on the reference...");
                
                var tempProjectData = JsonSerializer.Deserialize<ProjectData>(await File.ReadAllTextAsync(projectFilePath), SerializerOptions)!;
                var tempBellaRef = tempProjectData.SourceReferences.FirstOrDefault(sr => sr.Id == bellaSourceRef.Id);
                if (tempBellaRef != null) tempBellaRef.OriginalAbsolutePath = movedBellaPath;
                await File.WriteAllTextAsync(projectFilePath, JsonSerializer.Serialize(tempProjectData, SerializerOptions));
                Console.WriteLine("Temporarily updated project file to point to new location for demonstration.");


                var (relinkedComposition, newMissing) = await CompositionProjectManager.LoadProjectAsync(projectFilePath);
                if (newMissing.All(ms => ms.Id != bellaSourceRef.Id))
                {
                     Console.WriteLine("Bella's audio relinked successfully on reload!");
                     PlayComposition(relinkedComposition, "Playing relinked project...");
                }
                else
                {
                    Console.WriteLine("Relink failed to reflect on reload.");
                }
                
                relinkedComposition.Dispose();
            }
            else
            {
                Console.WriteLine("Relink process failed for Bella's audio.");
            }
        }
        
        // 6. Clean up: Move Bella.wav back
        if (File.Exists(movedBellaPath))
        {
            File.Copy(movedBellaPath, originalBellaPath, true);
            File.Delete(movedBellaPath);
            Console.WriteLine($"Cleaned up: Moved {Path.GetFileName(originalBellaPath)} back to original location.");
        }
        if (Directory.Exists(tempMissingFileDir)) Directory.Delete(tempMissingFileDir, true);
    }

    private static Task DemonstrateDirtyFlag()
    {
        Console.WriteLine("Creating a new composition...");
        var composition = new Composition("DirtyTestComp") { SampleRate = 24000, TargetChannels = 1};
        Console.WriteLine($"IsDirty initially: {composition.IsDirty}"); // Expected: false

        composition.MasterVolume = 0.8f; // This should call MarkDirty
        Console.WriteLine($"After changing MasterVolume, IsDirty: {composition.IsDirty}"); // Expected: true

        var track = new Track("TestTrack");
        composition.AddTrack(track); // AddTrack should call MarkDirty
        Console.WriteLine($"After adding a track, IsDirty: {composition.IsDirty}"); // Expected: true

        if (File.Exists(DialogueFiles.AdamWavPath))
        {
            var provider = new StreamDataProvider(File.OpenRead(DialogueFiles.AdamWavPath));
            var segment = new AudioSegment(provider, TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.Zero, ownsDataProvider: true);
            track.AddSegment(segment); // AddSegment on Track should propagate MarkDirty
            Console.WriteLine($"After adding a segment, IsDirty: {composition.IsDirty}"); // Expected: true

            segment.Settings.Volume = 0.5f; // Settings setter should propagate MarkDirty
            Console.WriteLine($"After changing segment volume, IsDirty: {composition.IsDirty}"); // Expected: true
        } else { Console.WriteLine("Adam.wav not found, skipping some dirty flag tests.");}


        // Simulate saving (clears dirty flag)
        Console.WriteLine("Simulating save...");
        // It's internal method, so we need to access it via reflection to mimic the behavior of project manager
        var clearDirtyFlagMethod = typeof(Composition).GetMethod("ClearDirtyFlag", BindingFlags.Instance | BindingFlags.NonPublic);
        clearDirtyFlagMethod?.Invoke(composition, null);
        Console.WriteLine($"After ClearDirtyFlag, IsDirty: {composition.IsDirty}"); // Expected: false

        composition.Dispose();
        return Task.CompletedTask;
    }
}