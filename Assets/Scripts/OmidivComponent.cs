using UnityEngine;

/**
 * 
 */
public abstract class OmidivComponent : MonoBehaviour {
    protected static bool IsPlaying = false;

    // Please call base.OnEnable() if overriding this, unless you really know what you're doing.
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

    // Please call base.OnEnable() if overriding this, unless you really know what you're doing.
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

    // Please call base.OnEnable() if overriding this, unless you really know what you're doing.
    protected virtual void Awake() {
        ReadConfig();
    }

    // Please call base.OnEnable() if overriding this, unless you really know what you're doing.
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