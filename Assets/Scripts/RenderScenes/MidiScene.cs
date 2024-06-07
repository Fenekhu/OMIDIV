using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The base class for a component that draws a visualization. This class itself controls a lot of things about the visualization, such as events.
/// There should only be one of these per scene.
/// </summary>
/// <remarks>
/// I will consider making some of the protected fields public so that you can have multiple components reading midi information and drawing visuals.
/// Or I may find another solution to this, such as a component class that has access to midi information and responds to events, but doesn't control the simulation.<br/>
/// On further thought, yes.<br/>
/// TODO: separate MidiScene into VisualsComponent and SceneController.
/// </remarks>
public abstract class MidiScene : OmidivComponent {
    // This static stuff should be retained between scenes
    private static RawMidi rawMidi;
    private static bool midiPathChanged = false;
    private static double recordingTime = 0; // seconds
    private static double simulatedTime = 0; // seconds
    private static double recordingDeltaTime = 0; // seconds
    private static bool overrideTime = false;

    protected static FileInfo MidiPath;
    protected static CookedMidi Midi = new CookedMidi();
    protected static int MidiDelay; // milliseconds
    private static int midiDelayPrev; // milliseconds
    protected static bool midiDelayChanged;
    
    // should these CurrentXXX fields be static?
    // Yes, because the midi is static, thus multiple midis couldn't be loaded anyway.
    // Additionally, recordingTime is static so all the time keeping should be static as well.

    /// <summary>
    /// The current midi tick the visualization is on. May be negative with a midi delay.
    /// </summary>
    /// <seealso cref="CurrentTickLong"/>
    /// <seealso cref="CurrentTickDouble"/>
    public static decimal CurrentTick { get; set; }

    /// <summary>
    /// The current midi time in microseconds. May be negative with a midi delay.
    /// </summary>
    /// <seealso cref="CurrentTimeLong"/>
    /// <seealso cref="CurrentTimeDouble"/>
    public static decimal CurrentTime { get; set; }

    /// <summary>
    /// The current tempo in microseconds per quarter note (as per the midi file format).
    /// </summary>
    public static uint CurrentTempoMicros { get; set; } = 500000;
    public static double CurrentTempoBPM { 
        get { return MidiUtil.TempoBPM(CurrentTempoMicros); } 
        set { CurrentTempoMicros = MidiUtil.TempoMicros(value); } 
    }

    /// <summary>
    /// High precision duration of one tick.<br/>
    /// Consider using this instead of <see cref="Time.fixedDeltaTime"/>.
    /// </summary>
    public static decimal TickDeltaTime { get { 
            decimal tpqn = CurrentTempoMicros / (Midi.Header.ticksPerQuarter * 1e6m);
            if (Midi == null) return tpqn;
            return Midi.Header.fmt switch {
                EMidiDivisionFormat.TPQN => tpqn,
                EMidiDivisionFormat.SMPTE => 1.0m / (-Midi.Header.smpte * Midi.Header.ticksPerFrame),
                _ => tpqn,
            };
    } }

    /// <summary>
    /// <see cref="Time.deltaTime"/> when not recording, <c>1/recording framerate</c> when recording.
    /// In most cases, use this instead of <see cref="Time.deltaTime"/> so that recordings work properly.
    /// </summary>
    public static double FrameDeltaTime { get { return overrideTime ? recordingDeltaTime : Time.deltaTime; } }

    /// <remarks>Doesn't make changes you make automatically apply. Instead, chack the value of this for whether or not you should apply the changes when they happen.</remarks>
    public static bool AutoReload { get; set; } = true;

    protected static readonly List<Color> TrackColors = new List<Color>();

    /// Populates <see cref="TrackColors"/>
    static MidiScene() {
        TrackColors.AddRange(new Color[] {
            new Color(1.00f, 0.00f, 0.00f, 1.0f),
            new Color(1.00f, 0.25f, 0.00f, 1.0f),
            new Color(1.00f, 0.50f, 0.00f, 1.0f),
            new Color(1.00f, 0.75f, 0.00f, 1.0f),
            new Color(1.00f, 1.00f, 0.00f, 1.0f),
            new Color(0.75f, 1.00f, 0.00f, 1.0f),
            new Color(0.50f, 1.00f, 0.00f, 1.0f),
            new Color(0.25f, 1.00f, 0.00f, 1.0f),
            new Color(0.00f, 1.00f, 0.00f, 1.0f),
            new Color(0.00f, 1.00f, 0.25f, 1.0f),
            new Color(0.00f, 1.00f, 0.50f, 1.0f),
            new Color(0.00f, 1.00f, 0.75f, 1.0f),
            new Color(0.00f, 1.00f, 1.00f, 1.0f),
            new Color(0.00f, 0.75f, 1.00f, 1.0f),
            new Color(0.00f, 0.50f, 1.00f, 1.0f),
            new Color(0.00f, 0.25f, 1.00f, 1.0f),
            new Color(0.00f, 0.00f, 1.00f, 1.0f),
            new Color(0.25f, 0.00f, 1.00f, 1.0f),
            new Color(0.50f, 0.00f, 1.00f, 1.0f),
            new Color(0.75f, 0.00f, 1.00f, 1.0f),
            new Color(1.00f, 0.00f, 1.00f, 1.0f),
            new Color(1.00f, 0.00f, 0.75f, 1.0f),
            new Color(1.00f, 0.00f, 0.50f, 1.0f),
            new Color(1.00f, 0.00f, 0.25f, 1.0f),
        });
    }

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

    [SerializeField] protected RecorderController VideoRecorder;

    private bool bOpenMidi = false;

    /// <summary>
    /// Create your visualization here. Happens every time visuals reload after a call to <see cref="ClearVisuals"/>.
    /// </summary>
    protected abstract void CreateVisuals();

    /// <summary>Clear all created visuals here.</summary>
    /// <remarks>May be called before visuals have been created, so don't assume the visuals will be there.</remarks>
    protected abstract void ClearVisuals();

    /// <summary>
    /// Move your visualization forward or backward (in case midi delay is adjusted).
    /// </summary>
    /// <param name="ticks">Will usually be 1 except if the midi uses SMPTE time format, or the midi delay is adjusted.</param>
    protected abstract void MovePlay(decimal ticks);

    protected override void OnEnable() {
        base.OnEnable();
        ImGuiManager.DrawMainMenuItems += DrawMainMenuItems;
        if (VideoRecorder != null) {
            VideoRecorder.OnRecordingBegin += OnRecordingBegin;
            VideoRecorder.OnBeforeFrame += OnFrameBegin;
            VideoRecorder.OnAfterFrame += OnFrameEnd;
            VideoRecorder.OnRecordingEnd += OnRecordingEnd;
        }
    }

    protected override void OnDisable() {
        base.OnDisable();
        ImGuiManager.DrawMainMenuItems -= DrawMainMenuItems;
        if (VideoRecorder != null) {
            VideoRecorder.OnRecordingBegin -= OnRecordingBegin;
            VideoRecorder.OnBeforeFrame -= OnFrameBegin;
            VideoRecorder.OnAfterFrame -= OnFrameEnd;
            VideoRecorder.OnRecordingEnd -= OnRecordingEnd;
        }
    }

    /// <remarks>
    /// Please call <c>base.Start()</c> if overriding this, unless you really know what you're doing.
    /// </remarks>
    protected virtual void Start() {
        recordingTime = 0;
        if (Midi != null) NeedsVisualReload = true;
    }

    /// <remarks>
    /// Clears visuals by default.<br/>
    /// Please call <c>base.OnDestroy()</c> if overriding this, unless you really know what you're doing.
    /// </remarks>
    protected override void OnDestroy() {
        base.OnDestroy();
        ClearVisuals();
    }

    private void OnRecordingBegin() {
        overrideTime = true;
        recordingDeltaTime = 1f / VideoRecorder.GetFramerate();
    }

    // currently does nothing but may be needed in the future.
    private void OnFrameBegin() { }

    private void OnFrameEnd() {
        recordingTime += recordingDeltaTime;
    }

    private void OnRecordingEnd() {
        simulatedTime = recordingTime = 0;
        overrideTime = false;
    }

    /// <remarks>Please call <c>base.ReadConfig()</c> if overriding this unless you know what you're doing.</remarks>
    protected override void ReadConfig() {
        Config.TryGet<bool>("autoReload", val => AutoReload = val);
    }

    /// <remarks>Please call <c>base.WriteConfig()</c> if overriding this unless you know what you're doing.</remarks>
    protected override void WriteConfig() {
        Config.Set("autoReload", AutoReload);
    }

    private void FixedUpdate() {
        if (VideoRecorder.RecordingEnabled && IsPlaying) {
            while (simulatedTime < recordingTime) {
                CustomFixedUpdate();
                simulatedTime += (double)TickDeltaTime;
            }
        } else {
            CustomFixedUpdate();
        }
    }

    /// <summary>
    /// Happens possibly multiple times per frame, dependent on tempo with consideration to recording.<br/>
    /// You probably want to use this instead of the FixedUpdate Unity message.<br/>
    /// Use <see cref="MidiScene.FrameDeltaTime"/> instead of <see cref="Time.deltaTime"/>.
    /// </summary>
    protected virtual void CustomFixedUpdate() {
        UpdateTPS();
        if (MidiDelay != midiDelayPrev) {
            long diff = 1000L * (midiDelayPrev - MidiDelay);
            decimal tickCount = MicrosToTicks(CurrentTime, diff);
            MovePlay(tickCount);
            CurrentTime += diff;
            CurrentTick += tickCount;

            midiDelayPrev = MidiDelay;
            midiDelayChanged = true;
        } else midiDelayChanged = false;

        if (!IsPlaying) return;

        if (VideoRecorder.RecordingEnabled && VideoRecorder.GetStatus() != RecorderController.Status.Recording)
            return;

        decimal dt = Midi.Header.fmt switch {
            EMidiDivisionFormat.TPQN => 1m,
            EMidiDivisionFormat.SMPTE => TickDeltaTime / (-Midi.Header.smpte * Midi.Header.ticksPerFrame),
            _ => 1m, // shouldn't happen because fmt is a 1 bit value.
        };

        MovePlay(dt);

        CurrentTick++;
        CurrentTime += 1e6m * TickDeltaTime;
    }

    /// <remarks>Please call <c>base.Update()</c> if overriding this.</remarks>
    protected virtual void Update() {
        if (bOpenMidi) {
            SFB.ExtensionFilter[] exts = {new SFB.ExtensionFilter("MIDI", "mid", "midi")};
            SFB.StandaloneFileBrowser.OpenFilePanelAsync("Open MIDI", "", exts, false, (string[] res) => {
                if (res.Length > 0) {
                    MidiPath = new FileInfo(res[0]);
                    midiPathChanged = true;
                    NeedsStopPlay = true;
                    NeedsMidiReload = true;
                    NeedsRestart = true;
                }
            });
            bOpenMidi = false;
        }

        if (IsPlaying && VideoRecorder.RecordingEnabled && VideoRecorder.GetStatus() != RecorderController.Status.Recording)
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

    protected override void Restart() {
        CurrentTime = -MidiDelay * 1000m;
        CurrentTick = MicrosToTicks(0, CurrentTime);
        MovePlay(CurrentTick);
        simulatedTime = recordingTime = 0;
    }

    /// <summary>Currently just reads the midi from the file into <see cref="Midi"/></summary>
    /// <remarks>Please call <c>base.LoadMidi()</c> if overriding this.</remarks>
    protected override void LoadMidi() {
        InitMidi();
    }

    /// <summary>Clears and creates the visuals.</summary>
    /// <remarks>Please call <c>base.LoadVisuals()</c> if overriding this.</remarks>
    protected override void LoadVisuals() {
        ClearVisuals();
        CreateVisuals();
    }

    /// <remarks>Please call <c>base.DrawGUI()</c> if overriding this.</remarks>
    protected override void DrawGUI() {
        if (ImGuiManager.IsDebugEnabled) {
            if (ImGui.Begin("debug")) {
                ImGui.Text(string.Format("time: {0:d}", CurrentTime));
                var nextTempo = Midi.TempoMap.GT((long)CurrentTime) ?? (0, 0);
                ImGui.Text(string.Format("next tempo: {0:d} ({1:F2})", nextTempo.tempoMicros, MidiUtil.TempoBPM(nextTempo.tempoMicros)));
                ImGui.Text(string.Format("at: {0:d}", nextTempo.timeMicros));
            }
            ImGui.End();
        }

        if (ImGui.Begin("MIDI Controls")) {
            ImGui.SetNextItemWidth(128.0f);
            ImGui.InputInt("Delay (ms, +/-)", ref MidiDelay, 100, 2000);
        }
        ImGui.End();

        if (ImGui.Begin("Misc Controls")) {
            bool _autoReload = AutoReload;
            if (ImGui.Checkbox("Auto-apply certain changes", ref _autoReload)) AutoReload = _autoReload;
        }
        ImGui.End();

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

    /// <remarks>Please call <c>base.DrawMainMenuItems()</c> if overriding this.</remarks>
    protected void DrawMainMenuItems(string menuName) {
        if (menuName == "File") {
            bOpenMidi = ImGui.MenuItem("Open Midi");
            ImGui.Separator();
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

    /// <summary>Resets some things and loads the midi if the path has changed then cooks the rawMidi.</summary>
    private void InitMidi() {
        CurrentTick = 0;
        CurrentTime = 0; // TODO this was NOT originally here // huh?
        CurrentTempoMicros = 0;
        ClearVisuals();

        if (MidiPath.Length == 0) return;
        if (midiPathChanged) {
            if (rawMidi == null) rawMidi = new RawMidi();
            rawMidi.Open(MidiPath);

            //using (StreamWriter sw = new StreamWriter(new FileStream("midi_dump.txt", FileMode.Create))) rawMidi.DebugPrint(sw);

            midiPathChanged = false;
        }

        if (Midi == null) Midi = new CookedMidi();
        Midi.Cook(rawMidi);

        UpdateTPS();
    }

    /// <summary>
    /// Converts a number of microseconds into a number of ticks.<br/>
    /// This is dependent on tempo and tempo changes, so the <paramref name="start"/> time is needed.
    /// </summary>
    /// <param name="start">The microsecond of playback to start at. May be negative.</param>
    /// <param name="micros">The number of microseconds to convert</param>
    /// <remarks>
    /// Known very small issue: 
    /// If there is a tempo change at, say 1000 microseconds, start is 1000.xxx, and micros is negative, 
    /// 0.xxx micros will be converted at the previous tempo. Keep in mind this is *nanoseconds* of error.
    /// </remarks>
    protected decimal MicrosToTicks(decimal start, decimal micros) {
        decimal ret = 0;
        var tempoMap = Midi.TempoMap;

        // overview: 
        // this loop looks at the current tempo,
        // looks how much time is spent in the tempo,
        // converts it to a number of ticks,
        // accumulates the number of ticks,
        // then moves on to the next tempo and repeats, unless we finished converting all the time.

        // is there still time left that needs to be converted.
        while (micros != 0) {
            decimal timeSpentInTempo = 0; // in microseconds
            uint _tempo = tempoMap[(long)start].tempoMicros; // the current micro-to-tick conversion factor.

            // if we're moving backwards.
            if (micros < 0) {
                // the index of the previous tempo change, or the first tempo change if we're before it.
                // Note: (long)start rounds down. If (long)start is a tempo change and start has a decimal part,
                // the "previous" tempo change should be the one at (long)start, but instead the one before that will be returned.
                // This only results in less than a microsecond being improperly converted.
                int index = tempoMap.LTIndex((long)start).GetValueOrDefault(0);
                if (index == 0) { // covers everything before the second tempo change.
                    timeSpentInTempo = micros;
                } else {
                    var item = tempoMap.GetAtIndex(index);
                    // if the previous tempo change is before the "end" conversion time, only convert whats left.
                    // keep in mind micros is negative, so start + micros will be before start.
                    if (item.timeMicros < (start + micros)) {
                        timeSpentInTempo = micros;
                    } else {
                        timeSpentInTempo = item.timeMicros - start;
                    }
                }
            } else { // we are moving forwards
                int? index = tempoMap.GTEIndex((long)start);
                if (!index.HasValue || tempoMap.GetAtIndex(index.Value).timeMicros > (start + micros)) {
                    timeSpentInTempo = micros;
                } else {
                    timeSpentInTempo = tempoMap.GetAtIndex(index.Value).timeMicros - start;
                }
            }

            start += timeSpentInTempo;
            micros -= timeSpentInTempo;

            // convert the time spend in the tempo to a number of ticks.
            switch (Midi.Header.fmt) {
            case EMidiDivisionFormat.TPQN:
                ret += timeSpentInTempo * Midi.Header.ticksPerQuarter / _tempo;
                break;
            case EMidiDivisionFormat.SMPTE:
                ret += timeSpentInTempo / (1e6m * -Midi.Header.smpte * Midi.Header.ticksPerFrame);
                break;
            }
        }

        return ret;
    }

    // TODO: store the next tempo change time and only change things if we've passed it.
    private void UpdateTPS() {
        // get the tempo at the current time.
        uint newTempo = Midi.TempoMap[(long)CurrentTime].tempoMicros;
        // if the tempo has changed, update the tempo and the simulation tick rate.
        if (newTempo != CurrentTempoMicros) {
            CurrentTempoMicros = newTempo;
            Time.fixedDeltaTime = (float)TickDeltaTime;
        }
    }
}
