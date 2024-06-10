using UnityEngine;

/// <summary>
/// A base component that is subscribed to a bunch of events.
/// </summary>
public abstract class OmidivComponent : MonoBehaviour {

    /// <summary>Is the visualization currently playing.</summary>
    /// <remarks>Identical to <see cref="SceneController.IsPlaying"/>.</remarks>
    public static bool IsPlaying => SceneController.IsPlaying;

    /// <summary>
    /// <see cref="Time.deltaTime"/> when not recording, <c>1/recording framerate</c> when recording.
    /// In most cases, use this instead of <see cref="Time.deltaTime"/> so that recordings work properly.
    /// </summary>
    /// <remarks>Identical to <see cref="SceneController.FrameDeltaTime"/>.</remarks>
    public static double FrameDeltaTime => SceneController.FrameDeltaTime;

    /// <summary>
    /// Use this to give different instances in difference scenes different saved config.<br/>
    /// </summary>
    /// <remarks>You must manually add this to your config keys (for now?).</remarks>
    public string ConfigTag = "def";

    /// <remarks>
    /// Please call <c>base.OnEnable()</c> if overriding this, unless you really know what you're doing.
    /// </remarks>
    protected virtual void OnEnable() {
        ImGuiManager.Draw += DrawGUI;
        Config.AfterLoading += ReadConfig;
        Config.BeforeSaving += WriteConfig;
        SceneController.OnPlayStarted += OnPlayStart;
        SceneController.OnPlayStopped += OnPlayStop;
        SceneController.OnReset += Reset_;
        SceneController.OnRestart += Restart;
        SceneController.OnLoadMidi += LoadMidi;
        SceneController.OnLoadVisuals += LoadVisuals;
        SceneController.OnLoadAudio += LoadAudio;
    }

    /// <remarks>
    /// Please call <c>base.OnDisable()</c> if overriding this, unless you really know what you're doing.
    /// </remarks>
    protected virtual void OnDisable() {
        ImGuiManager.Draw -= DrawGUI;
        Config.AfterLoading -= ReadConfig;
        Config.BeforeSaving -= WriteConfig;
        SceneController.OnPlayStarted -= OnPlayStart;
        SceneController.OnPlayStopped -= OnPlayStop;
        SceneController.OnReset -= Reset_;
        SceneController.OnRestart -= Restart;
        SceneController.OnLoadMidi -= LoadMidi;
        SceneController.OnLoadVisuals -= LoadVisuals;
        SceneController.OnLoadAudio -= LoadAudio;
    }

    /// <remarks>
    /// Please call <c>base.Awake()</c> if overriding this, unless you really know what you're doing.
    /// </remarks>
    protected virtual void Awake() {
        ReadConfig();
    }

    /// <remarks>
    /// Please call <c>base.OnDestroy()</c> if overriding this, unless you really know what you're doing.
    /// </remarks>
    protected virtual void OnDestroy() {
        WriteConfig();
    }

    /// <summary>
    /// Override this. Use ImGui to draw most UI elements here.
    /// </summary>
    /// <remarks>Subscribe to <see cref="ImGuiManager.DrawMainMenuItems"/> to add to main menu bar menus instead of here.</remarks>
    protected virtual void DrawGUI() { }

    /// <summary>
    /// Override this. Read from <see cref="Config"/> here.<br/>
    /// Don't forget to include <see cref="ConfigTag"/> in your config keys.
    /// </summary>
    protected virtual void ReadConfig() { }

    /// <summary>
    /// Override this. Write to <see cref="Config"/> here.<br/>
    /// Don't forget to include <see cref="ConfigTag"/> in your config keys.
    /// </summary>
    protected virtual void WriteConfig() { }

    /// <summary>
    /// Override this. Called when the visualization starts playing. May be starting from the beginning or resuming.
    /// </summary>
    protected virtual void OnPlayStart() { }

    /// <summary>
    /// Override this. Called when the visualization stops playing.
    /// </summary>
    protected virtual void OnPlayStop() { }

    /// <summary>
    /// Override this. Called when <see cref="SceneController.OnReset"/> is fired. 
    /// <see cref="LoadAudio"/> and <see cref="LoadMidi"/> will also be called afterwards, so don't do any of that here,
    /// but do reload any other resources your component needs.
    /// </summary>
    /// <remarks>Reset() is a Unity message, hence the <c>_</c></remarks>
    protected virtual void Reset_() { }

    /// <summary>
    /// Override this. Called when the visualization restarts (R is pressed).
    /// </summary>
    protected virtual void Restart() { }

    /// <summary>
    /// Override this. Called when the midi information needs to be loaded from the file. <see cref="LoadVisuals"/> will also be called afterwards, so don't do any of that here.
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