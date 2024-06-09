using ImGuiNET;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls scene events and coordination.
/// </summary>
public class SceneController : MonoBehaviour {

    /// <summary>Called when the visualization starts playing. May be starting from the beginning or resuming.</summary>
    public static event Action OnPlayStarted;
    /// <summary>Called when the visualization stops playing.</summary>
    public static event Action OnPlayStopped;
    /// <summary><see cref="OnLoadAudio"/> and <see cref="OnLoadMidi"/> will also be called afterwards, so don't do any of that here,
    /// but do reload any other resources your component needs.</summary>
    public static event Action OnReset;
    /// <summary>Called when the visualization restarts (<c>R</c> is pressed).</summary>
    public static event Action OnRestart;
    /// <summary>Called when the midi information needs to be loaded from the file. 
    /// <see cref="OnLoadVisuals"/> will also be called afterwards, so don't do any of that here.</summary>
    public static event Action OnLoadMidi;
    /// <summary>Called when the visuals need to be recreated (like when something in the MIDI Controls window is changed).</summary>
    public static event Action OnLoadVisuals;
    /// <summary>Called when the audio needs to be loaded from the file.</summary>
    public static event Action OnLoadAudio;

    public static bool NeedsStartPlay { get; set; } = false;
    public static bool NeedsStopPlay { get; set; } = false;
    public static bool NeedsReset { get; set; } = false;
    public static bool NeedsRestart { get; set; } = false;
    public static bool NeedsMidiReload { get; set; } = false;
    public static bool NeedsVisualReload { get; set; } = false;
    public static bool NeedsAudioReload { get; set; } = false;

    /// <summary>Is the visualization currently playing.</summary>
    /// <remarks>Identical to <see cref="OmidivComponent.IsPlaying"/>.</remarks>
    public static bool IsPlaying { get; private set; } = false;

    /// <summary>
    /// High precision duration of one tick.<br/>
    /// Consider using this instead of <see cref="Time.fixedDeltaTime"/>.
    /// </summary>
    /// <remarks>Identical to <see cref="OmidivComponent.TickDeltaTime"/>.</remarks>
    public static decimal TickDeltaTime => MidiManager.TickDeltaTime_src;

    /// <summary>
    /// <see cref="Time.deltaTime"/> when not recording, <c>1/recording framerate</c> when recording.
    /// In most cases, use this instead of <see cref="Time.deltaTime"/> so that recordings work properly.
    /// </summary>
    /// <remarks>Identical to <see cref="OmidivComponent.FrameDeltaTime"/>.</remarks>
    public static double FrameDeltaTime => VideoRecorder.FrameDeltaTime_src;

    private void OnEnable() {
        ImGuiManager.Draw += DrawGUI;
    }

    private void OnDisable() {
        ImGuiManager.Draw -= DrawGUI;
    }

    private void DrawGUI() {
        if (ImGui.Begin("Keybinds")) {
            ImGui.Text("Space: Play/Pause");
            ImGui.Text("R: Restart");
            ImGui.Text("F1: Toggle GUI");
            ImGui.Text("F3: Toggle debug info");
            ImGui.Text("F5: Reload Everything");
            ImGui.Text("F6: Refresh MIDI");
            ImGui.Text("F11: Toggle Fullscreen");
        }
        ImGui.End();
    }

    private void Update() {
        if (IsPlaying && VideoRecorder.RecordingEnabled && VideoRecorder.GetStatus() != VideoRecorder.Status.Recording)
            return;

        HandleInputs();

        if (NeedsStopPlay) {
            StopPlay();
        }
        if (NeedsReset) {
            NeedsReset = false;
            NeedsMidiReload = false;
            NeedsVisualReload = false;
            NeedsAudioReload = false;
            NeedsRestart = false;
            OnReset?.Invoke();
            OnLoadMidi?.Invoke();
            OnLoadVisuals?.Invoke();
            OnLoadAudio?.Invoke();
            OnRestart?.Invoke();
        }
        if (NeedsRestart) {
            NeedsRestart = false;
            OnRestart?.Invoke();
        }
        if (NeedsMidiReload) {
            NeedsMidiReload = false;
            NeedsVisualReload = false;
            OnLoadMidi?.Invoke();
            OnLoadVisuals?.Invoke();
        }
        if (NeedsVisualReload) {
            NeedsVisualReload = false;
            OnLoadVisuals?.Invoke();
        }
        if (NeedsAudioReload) {
            NeedsAudioReload = false;
            OnLoadAudio?.Invoke();
        }
        if (NeedsStartPlay) {
            StartPlay();
        }
    }

    private void HandleInputs() {
        // Space -- Start/Stop play
        if (Keyboard.current.spaceKey.wasPressedThisFrame) {
            if (IsPlaying) NeedsStopPlay = true; else NeedsStartPlay = true;
        }

        // R -- Stop and Restart Play
        if (Keyboard.current.rKey.wasPressedThisFrame) {
            NeedsStopPlay = true;
            NeedsRestart = true;
        }

        // F5 -- Full Reset
        if (Keyboard.current.f5Key.wasPressedThisFrame) {
            NeedsStopPlay = true;
            NeedsReset = true;
        }

        // F6 -- Reload visuals or midi (shift)
        if (Keyboard.current.f6Key.wasPressedThisFrame) {
            // Shift + F6
            if (Keyboard.current.shiftKey.isPressed) {
                NeedsMidiReload = true;
            } else { // F6
                NeedsVisualReload = true;
            }
        }

        // F11 -- Toggle Fullscreen
        if (Keyboard.current.f11Key.wasPressedThisFrame) {
            Screen.fullScreen = !Screen.fullScreen;
        }
    }

    private static void StartPlay() {
        NeedsStartPlay = false;
        IsPlaying = true;
        OnPlayStarted?.Invoke();
    }

    private static void StopPlay() {
        NeedsStopPlay = false;
        IsPlaying = false;
        OnPlayStopped?.Invoke();
    }
}