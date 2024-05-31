using ImGuiNET;
using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class OmidivComponent : MonoBehaviour {
    protected static bool IsPlaying = false;

    protected virtual void OnEnable() {
        ImGuiManager.Draw += DrawGUI;
        Config.AfterLoading += ReadConfig;
        Config.BeforeSaving += WriteConfig;
        MidiScene.OnPlayStarted += OnPlayStart;
        MidiScene.OnPlayStopped += OnPlayStop;
        MidiScene.OnReset += Reset_;
        MidiScene.OnRestart += Restart;
        MidiScene.OnReloadMidi += ReloadMidi;
        MidiScene.OnReloadVisuals += ReloadVisuals;
        MidiScene.OnReloadAudio += ReloadAudio;
    }

    protected virtual void OnDisable() {
        ImGuiManager.Draw -= DrawGUI;
        Config.AfterLoading -= ReadConfig;
        Config.BeforeSaving -= WriteConfig;
        MidiScene.OnPlayStarted -= OnPlayStart;
        MidiScene.OnPlayStopped -= OnPlayStop;
        MidiScene.OnReset -= Reset_;
        MidiScene.OnRestart -= Restart;
        MidiScene.OnReloadMidi -= ReloadMidi;
        MidiScene.OnReloadVisuals -= ReloadVisuals;
        MidiScene.OnReloadAudio -= ReloadAudio;
    }

    protected virtual void Awake() {
        ReadConfig();
    }

    protected virtual void OnDestroy() {
        WriteConfig();
    }

    protected virtual void DrawGUI() { }
    protected virtual void ReadConfig() { }
    protected virtual void WriteConfig() { }

    protected virtual void OnPlayStart() { }
    protected virtual void OnPlayStop() { }

    protected virtual void Reset_() { }

    protected virtual void Restart() { }

    protected virtual void ReloadMidi() { }

    protected virtual void ReloadVisuals() { }

    protected virtual void ReloadAudio() { }
}