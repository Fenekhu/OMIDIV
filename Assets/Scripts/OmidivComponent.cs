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
        MidiScene.OnLoadMidi += LoadMidi;
        MidiScene.OnLoadVisuals += LoadVisuals;
        MidiScene.OnLoadAudio += LoadAudio;
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
        MidiScene.OnLoadMidi -= LoadMidi;
        MidiScene.OnLoadVisuals -= LoadVisuals;
        MidiScene.OnLoadAudio -= LoadAudio;
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

    /// <summary>
    /// Override this. Called when the midi information needs to be loaded from the file. <see cref="LoadVisuals"/> will also be called, so don't do any of that here.
    /// </summary>
    protected virtual void LoadMidi() { }

    /// <summary>
    /// Override this. Called when the visuals need to be recreated (like when something in the MIDI Controls window is changed).
    /// </summary>
    protected virtual void LoadVisuals() { }

    /// <summary>
    /// Override this. Called when the audio needs to be loaded from the file.
    /// </summary>
    protected virtual void LoadAudio() { }
}