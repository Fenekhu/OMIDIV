using ImGuiNET;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class MidiScene : OmidivComponent {
    // This static stuff should be retained between scenes
    private static Dictionary<int, string> scenes = new Dictionary<int, string>();

    private static RawMidi rawMidi;
    private static bool midiPathChanged = false;
    private static float recordingTime = 0;
    private static float simulatedTime = 0;

    protected static FileInfo MidiPath;
    protected static CookedMidi Midi = new CookedMidi();
    protected static int MidiOffset;
    private static int midiOffsetPrev;
    protected static bool midiOffsetChanged;

    protected static bool IsPlaying = false;
    public static bool AutoReload = true;

    public static float DeltaTime = 0;

    protected static readonly List<Color> TrackColors = new List<Color>();

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

    [SerializeField] protected RecorderController VideoRecorder;

    protected long CurrentTick { get { return (long)CurrentTickDouble; } }
    protected double CurrentTickDouble = 0;
    // the current midi time in microseconds
    protected long CurrentTime { get { return (long)CurrentTimeDouble; } }
    // the current midi time in microseconds
    protected double CurrentTimeDouble = 0;
    protected uint CurrentTempo = 500000;

    public static event Action OnPlayStarted;
    public static event Action OnPlayStopped;
    public static event Action OnReset;
    public static event Action OnRestart;
    public static event Action OnReloadMidi;
    public static event Action OnReloadAudio;
    public static event Action OnReloadVisuals;
    public static bool NeedsStartPlay = false;
    public static bool NeedsStopPlay = false;
    public static bool NeedsReset = false;
    public static bool NeedsRestart = false;
    public static bool NeedsMidiReload = false;
    public static bool NeedsVisualReload = false;
    public static bool NeedsAudioReload = false;

    public static void RegisterScene(int index, string name) {
        scenes.Add(index, name);
    }

    public MidiScene() {
        RegisterScene(GetSceneIndex(), GetSceneName());
    }

    public void SetScene(int index) {
        StartCoroutine(CoroutineUtils.LoadScene(index));
    }

    protected abstract int GetSceneIndex();
    protected abstract string GetSceneName();
    protected abstract void InitVisuals();
    protected abstract void ClearVisuals();
    protected abstract void MovePlay(double ticks);

    protected override void OnEnable() {
        Config.AfterLoading += ReadConfig;
        Config.BeforeSaving += WriteConfig;
        if (VideoRecorder != null) {
            VideoRecorder.OnBeforeFrame += OnFrameBegin;
            VideoRecorder.OnAfterFrame += OnFrameEnd;
            VideoRecorder.OnRecordingEnd += OnRecordingEnd;
        }
        OnReset += Reset_;
        OnRestart += Restart;
    }

    protected override void OnDisable() {
        Config.AfterLoading -= ReadConfig;
        Config.BeforeSaving -= WriteConfig;
        if (VideoRecorder != null) {
            VideoRecorder.OnBeforeFrame -= OnFrameBegin;
            VideoRecorder.OnAfterFrame -= OnFrameEnd;
            VideoRecorder.OnRecordingEnd -= OnRecordingEnd;
        }
        OnReset -= Reset_;
        OnRestart -= Restart;
    }

    protected virtual void Start() {
        recordingTime = 0;
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        ClearVisuals();
    }

    private void OnFrameBegin() { }

    private void OnFrameEnd() {
        DeltaTime = 1f / VideoRecorder.GetFramerate();
        recordingTime += DeltaTime;
    }

    private void OnRecordingEnd() {
        simulatedTime = recordingTime = 0;
    }

    protected override void ReadConfig() {
        AutoReload = Config.Get<bool>("autoReload") ?? AutoReload;
    }

    protected override void WriteConfig() {
        Config.Set("autoReload", AutoReload);
    }

    private void FixedUpdate() {
        if (VideoRecorder.RecordingEnabled && IsPlaying) {
            while (simulatedTime < recordingTime) {
                CustomFixedUpdate();
                simulatedTime += Time.fixedDeltaTime;
            }
        } else {
            CustomFixedUpdate();
        }
    }

    // happens possibly multiple times per frame, dependent on tempo
    // with consideration to recording.
    // Similarly, use MidiScene.DeltaTime instead of Time.deltaTime
    protected virtual void CustomFixedUpdate() {
        UpdateTPS();
        if (MidiOffset != midiOffsetPrev) {
            int diff = 1000 * (MidiOffset - midiOffsetPrev);
            double tickCount = math.floor(MicrosToTicks(CurrentTime, diff));
            MovePlay(tickCount);
            CurrentTimeDouble += diff;
            CurrentTickDouble += tickCount;

            midiOffsetPrev = MidiOffset;
            midiOffsetChanged = true;
        } else midiOffsetChanged = false;

        if (!IsPlaying) return;

        if (VideoRecorder.RecordingEnabled && VideoRecorder.GetStatus() != RecorderController.Status.Recording)
            return;

        double dt = Midi.Header.fmt switch {
            EMidiDivisionFormat.TPQN => 1.0,
            EMidiDivisionFormat.SMPTE => Time.fixedDeltaTime / (-Midi.Header.smpte * Midi.Header.ticksPerFrame)
        };

        MovePlay(dt);

        CurrentTickDouble++;
        CurrentTimeDouble += Time.fixedDeltaTime * 1e6d;
    }

    protected virtual void Update() {
        if (IsPlaying && VideoRecorder.RecordingEnabled && VideoRecorder.GetStatus() != RecorderController.Status.Recording)
            return;

        HandleInputs();
        if (NeedsStopPlay) {
            StopPlay();
        }
        if (NeedsReset) {
            NeedsReset = false;
            NeedsRestart = false;
            NeedsMidiReload = false;
            NeedsVisualReload = false;
            NeedsAudioReload = false;
            OnReset?.Invoke();
        }
        if (NeedsRestart) {
            NeedsRestart = false;
            OnRestart?.Invoke();
        }
        if (NeedsMidiReload) {
            NeedsMidiReload = false;
            NeedsVisualReload = false;
            OnReloadMidi?.Invoke();
        }
        if (NeedsVisualReload) {
            NeedsVisualReload = false;
            OnReloadVisuals?.Invoke();
        }
        if (NeedsAudioReload) {
            NeedsAudioReload = false;
            OnReloadAudio?.Invoke();
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

    protected override void DrawGUI() {
        if (ImGuiManager.IsDebugEnabled) {
            if (ImGui.Begin("debug")) {
                ImGui.Text(string.Format("time: {0:d}", CurrentTime));
                var nextTempo = Midi.TempoMap.GT(CurrentTime) ?? (0, 0);
                ImGui.Text(string.Format("next tempo: {0:d} ({1:F2})", nextTempo.tempoMicros, MidiUtil.TempoBPM(nextTempo.tempoMicros)));
                ImGui.Text(string.Format("at: {0:d}", nextTempo.timeMicros));
            }
            ImGui.End();
        }

        bool bOpenMidi = false;

        if (ImGui.BeginMainMenuBar()) {
            if (ImGui.BeginMenu("File")) {
                ImGui.Separator();
                bOpenMidi = ImGui.MenuItem("Open Midi");
                if (ImGui.MenuItem("Quit")) Application.Quit();
                ImGui.Separator();
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("View")) {

                foreach (var kvp in scenes) {
                    if (ImGui.MenuItem(kvp.Value, kvp.Key != GetSceneIndex()))
                        SetScene(kvp.Key);
                }
                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }

        if (ImGui.Begin("MIDI Controls")) {
            ImGui.SetNextItemWidth(128.0f);
            ImGui.InputInt("Offset (ms)", ref MidiOffset);
        }
        ImGui.End();

        if (ImGui.Begin("Misc Controls")) {
            ImGui.Checkbox("Auto-apply certain changes", ref AutoReload);
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
        }
    }

    protected override void Reset_() {
        OnReloadMidi?.Invoke();
        OnReloadAudio?.Invoke();
        OnRestart?.Invoke();
    }

    protected override void Restart() {
        CurrentTimeDouble = MidiOffset * 1000d;
        CurrentTickDouble = MicrosToTicks(0, CurrentTime);
        MovePlay(CurrentTickDouble);
        simulatedTime = recordingTime = 0;
    }

    protected override void ReloadMidi() {
        InitMidi();
        OnReloadVisuals?.Invoke();
    }

    protected override void ReloadVisuals() {
        ClearVisuals();
        InitVisuals();
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

    private void InitMidi() {
        CurrentTickDouble = 0;
        CurrentTimeDouble = 0; // TODO this was NOT originally here
        CurrentTempo = 0;
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

    protected double MicrosToTicks(long start, long micros) {
        double ret = 0;
        var tempoMap = Midi.TempoMap;

        while (micros != 0) {
            long timeSpentInTempo = 0;
            uint _tempo = tempoMap[start].tempoMicros;

            if (micros < 0) {
                int index = tempoMap.GTEIndex(start).GetValueOrDefault(tempoMap.Count);
                if (index == 0) {
                    timeSpentInTempo = micros;
                } else {
                    index--;
                    var item = tempoMap.GetAtIndex(index);
                    if (item.timeMicros < (start + micros)) {
                        timeSpentInTempo = micros;
                    } else {
                        timeSpentInTempo = item.timeMicros - start;
                    }
                }
            } else {
                int? index = tempoMap.GTIndex(start);
                if (!index.HasValue || tempoMap.GetAtIndex(index.Value).timeMicros > (start + micros)) {
                    timeSpentInTempo = micros;
                } else {
                    timeSpentInTempo = tempoMap.GetAtIndex(index.Value).timeMicros - start;
                }
            }

            start += timeSpentInTempo;
            micros -= timeSpentInTempo;

            switch (Midi.Header.fmt) {
            case EMidiDivisionFormat.TPQN:
                ret += timeSpentInTempo * Midi.Header.ticksPerQuarter / _tempo;
                break;
            case EMidiDivisionFormat.SMPTE:
                ret += timeSpentInTempo / (1e6d * -Midi.Header.smpte * Midi.Header.ticksPerFrame);
                break;
            }
        }

        return ret;
    }

    private void UpdateTPS() {
        uint newTempo = Midi.TempoMap[CurrentTime].tempoMicros;
        if (newTempo != CurrentTempo) {
            CurrentTempo = newTempo;

            switch (Midi.Header.fmt) {
            case EMidiDivisionFormat.TPQN:
                Time.fixedDeltaTime = CurrentTempo / (Midi.Header.ticksPerQuarter * 1e6f);
                break;
            case EMidiDivisionFormat.SMPTE:
                Time.fixedDeltaTime = 1.0f / (-Midi.Header.smpte * Midi.Header.ticksPerFrame);
                break;
            }
        }
    }
}
