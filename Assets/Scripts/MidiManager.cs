using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum NoteState { Unplayed, Playing, Played }

/// <summary>
/// Stores information about the currently loaded midi and its playback status.
/// </summary>
public class MidiManager : OmidivComponent {

    private static bool midiPathChanged = false;

    /// <summary>The path to the currently loaded midi file.</summary>
    public static FileInfo MidiPath { get; private set; }
    private static FileInfo newMidiPath;

    /// <summary>The raw midi data of the currently loaded midi.</summary>
    public static RawMidi rawMidi { get; private set; }

    /// <summary>The currently loaded midi.</summary>
    public static CookedMidi Midi { get; private set; } = new CookedMidi();

    private static int _midiDelay;
    public static int MidiDelay {
        get { return _midiDelay; }
        set {
            long diff = 1000L * (_midiDelay - value);
            decimal tickCount = MicrosToTicks(CurrentTime, diff);
            OnMidiDelayChanged?.Invoke(diff, tickCount);
            CurrentTime += diff;
            CurrentTick += tickCount;
            _midiDelay = value;
        }
    }

    /// <summary><c>&lt;
    /// long diffMicros, decimal tickDelta
    /// &gt;</c><br/>
    /// Called before <see cref="CurrentTime"/> and <see cref="CurrentTick"/> have been updated.
    /// </summary>
    public static event Action<long, decimal> OnMidiDelayChanged;

    /// <summary>
    /// The current midi tick the visualization is on. May be negative with a midi delay.
    /// </summary>
    public static decimal CurrentTick { get; private set; }

    /// <summary>
    /// The current midi time in microseconds. May be negative with a midi delay.
    /// </summary>
    public static decimal CurrentTime { get; private set; }

    /// <summary>
    /// The current tempo in microseconds per quarter note (as per the midi file format).
    /// </summary>
    public static uint CurrentTempoMicros { get; private set; } = 500000;

    private static List<int>[] nowPlaying_impl = new List<int>[0];
    /// <summary>
    /// The list of currently playing notes for each track.<br/>
    /// <c>NowPlaying[trackIndex][noteIndex]</c>
    /// </summary>
    public static IReadOnlyCollection<IReadOnlyList<int>> NowPlaying => nowPlaying_impl;

    private static Dictionary<(int trackIndex, int noteIndex), NoteState> updatedStates_impl = new();
    /// <summary>
    /// The list of notes that have had their states changed since the last frame. 
    /// Valid even when not playing in case the delay changes.<br/>
    /// After restarting, this will NOT contain 
    /// </summary>
    public static IReadOnlyDictionary<(int trackIndex, int noteIndex), NoteState> UpdatedNoteStates => updatedStates_impl;


    /// <summary>The current tempo in Beats Per Minute.</summary>
    public static double CurrentTempoBPM {
        get { return MidiUtil.TempoBPM(CurrentTempoMicros); }
        set { CurrentTempoMicros = MidiUtil.TempoMicros(value); }
    }

    /// <summary>
    /// The frame delta time converted to ticks, accounting for tempo changes.
    /// Will be 0 if not playing.
    /// </summary>
    public static decimal TicksPerFrame { get; private set; }

    private static int[] playIndicies;

    /// <summary>
    /// Get the state of the note at the current time.
    /// </summary>
    /// <param name="trackIndex">The index of the track the note is in.</param>
    /// <param name="noteIndex">The index of the note within the track.</param>
    public static NoteState GetNoteState(int trackIndex, int noteIndex) {
        return GetNoteStateAtTick(trackIndex, noteIndex, CurrentTick);
    }

    /// <summary>
    /// Get the state of the note at a specific tick.
    /// </summary>
    /// <param name="trackIndex">The index of the track the note is in.</param>
    /// <param name="noteIndex">The index of the note within the track.</param>
    public static NoteState GetNoteStateAtTick(int trackIndex, int noteIndex, decimal tick) {
        MidiNote note = Midi.Tracks[trackIndex].notes[noteIndex];
        if (note.startTick > CurrentTick) return NoteState.Unplayed;
        if (note.endTick < CurrentTick) return NoteState.Played;
        return NoteState.Playing;
    }

    /// <summary>
    /// Get the state of the note at a specific time in microseconds.
    /// </summary>
    /// <param name="trackIndex">The index of the track the note is in.</param>
    /// <param name="noteIndex">The index of the note within the track.</param>
    public static NoteState GetNoteStateAtTime(int trackIndex, int noteIndex, decimal micro) {
        MidiNote note = Midi.Tracks[trackIndex].notes[noteIndex];
        if (note.startMicro > CurrentTime) return NoteState.Unplayed;
        if (note.endMicro < CurrentTime) return NoteState.Played;
        return NoteState.Playing;
    }

    /// <summary>Resets some things and loads the midi if the path has changed then cooks the rawMidi.</summary>
    private static void InitMidi() {
        CurrentTick = 0;
        CurrentTime = 0; // TODO this was NOT originally here // huh?
        CurrentTempoMicros = 0;

        if (MidiPath.Length == 0) return;
        if (midiPathChanged) {
            if (rawMidi == null) rawMidi = new RawMidi();
            rawMidi.Open(MidiPath);

            //using (StreamWriter sw = new StreamWriter(new FileStream("midi_dump.txt", FileMode.Create))) rawMidi.DebugPrint(sw);

            midiPathChanged = false;
        }

        if (Midi == null) Midi = new CookedMidi();
        Midi.Cook(rawMidi);

        nowPlaying_impl = new List<int>[Midi.Tracks.Count];
        for (int i = 0; i < nowPlaying_impl.Length; i++) nowPlaying_impl[i] = new List<int>();

        playIndicies = new int[Midi.Tracks.Count];

        UpdateTPS();
    }

    /// <summary>The current number of midi ticks per second, dependent on tempo.</summary>
    public static decimal TicksPerSecond(uint tempoMicros) {
        return Midi.Header.fmt switch {
            EMidiDivisionFormat.SMPTE => (decimal)(Midi.Header.ticksPerFrame / Midi.Header.SMPTEFPS),
            EMidiDivisionFormat.TPQN or _ => Midi.Header.ticksPerQuarter * 1e6m / CurrentTempoMicros,
        };
    }

    /// <summary>
    /// The duration of one midi tick.
    /// </summary>
    public static decimal TimePerTick(uint tempoMicros) => 1m / TicksPerSecond(tempoMicros);

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
    public static decimal MicrosToTicks(decimal start, decimal micros) {
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
                int? index = tempoMap.GTIndex((long)start);
                if (!index.HasValue || tempoMap.GetAtIndex(index.Value).timeMicros > (start + micros)) {
                    timeSpentInTempo = micros;
                } else {
                    timeSpentInTempo = tempoMap.GetAtIndex(index.Value).timeMicros - start;
                }
            }

            start += timeSpentInTempo;
            micros -= timeSpentInTempo;

            // convert the time spent in the tempo to a number of ticks.
            switch (Midi.Header.fmt) {
            case EMidiDivisionFormat.TPQN:
                ret += timeSpentInTempo * Midi.Header.ticksPerQuarter / _tempo;
                break;
            case EMidiDivisionFormat.SMPTE:
                ret += timeSpentInTempo / (1e6m * (decimal)Midi.Header.SMPTEFPS * Midi.Header.ticksPerFrame);
                break;
            }
        }

        return ret;
    }

    // TODO: store the next tempo change time and only change things if we've passed it.
    /// <summary>
    /// Checks whether a tempo change has occured and updates the tick rate if so.
    /// </summary>
    private static void UpdateTPS() {
        // get the tempo at the current time.
        uint newTempo = Midi.TempoMap[(long)CurrentTime].tempoMicros;
        // if the tempo has changed, update the tempo and the simulation tick rate.
        if (newTempo != CurrentTempoMicros) {
            CurrentTempoMicros = newTempo;
            Time.fixedDeltaTime = (float)TimePerTick(CurrentTempoMicros);
        }
    }

    private static void UpdateNowPlaying() {
        for (int j = 0; j < Midi.Tracks.Count; j++) {
            Track track = Midi.Tracks[j];

            // remove notes that are no longer playing.
            for (int i = 0; i < nowPlaying_impl[j].Count; i++) {
                int noteIndex = nowPlaying_impl[j][i];
                if (track.notes[noteIndex].endTick < CurrentTick) {
                    updatedStates_impl[(j, noteIndex)] = NoteState.Played;
                    nowPlaying_impl[j].RemoveAt(i--);
                }
            }

            while (true) {
                if (playIndicies[j] >= track.notes.Count) break;
                MidiNote note = track.notes[playIndicies[j]];
                if (note.startTick < CurrentTick) {
                    nowPlaying_impl[j].Add(playIndicies[j]);
                    updatedStates_impl[(j, playIndicies[j])] = NoteState.Playing;
                    playIndicies[j]++;
                } else break;
            }
        }
    }

    private static void OnMidiDelayChanged_impl(long diffMicros, decimal diffTicks) {
        // TODO: This will not reactivate notes if time moves backwards because that might require rescanning all notes from the beginning
        // it only deactivates active notes
        if (diffTicks < 0) {

        }
    }

    

    //----------------------------------------------------------------------------------------
    //       NON-STATIC AREA
    //----------------------------------------------------------------------------------------

    private bool bOpenMidi = false;

    protected override void OnEnable() {
        base.OnEnable();
        ImGuiManager.DrawMainMenuItems += DrawMainMenuItems;
    }

    protected override void OnDisable() {
        base.OnDisable();
        ImGuiManager.DrawMainMenuItems -= DrawMainMenuItems;
    }

    private void Start() {
        if (Midi != null) SceneController.NeedsVisualReload = true;
    }

    private void FixedUpdate() {
        UpdateTPS();
    }

    private void Update() {
        if (bOpenMidi) {
            SFB.ExtensionFilter[] exts = {new SFB.ExtensionFilter("MIDI", "mid", "midi")};
            SFB.StandaloneFileBrowser.OpenFilePanelAsync("Open MIDI", "", exts, false, (string[] res) => {
                if (res.Length > 0) {
                    MidiPath = new FileInfo(res[0]);
                    midiPathChanged = true;
                    SceneController.NeedsStopPlay = true;
                    SceneController.NeedsMidiReload = true;
                    SceneController.NeedsRestart = true;
                }
            });
            bOpenMidi = false;
        }

        UpdateNowPlaying();

        TicksPerFrame = 0;

        if (!IsPlaying)
            return;

        TicksPerFrame = MicrosToTicks(CurrentTime, 1e6m * (decimal)FrameDeltaTime);
    }

    private void LateUpdate() {
        updatedStates_impl.Clear();

        if (!IsPlaying)
            return;

        CurrentTick += TicksPerFrame;
        CurrentTime += 1e6m * (decimal)FrameDeltaTime;
    }

    /// <remarks>Please call <c>base.LoadMidi()</c> if overriding this.</remarks>
    protected override void Restart() {
        CurrentTime = -MidiDelay * 1000m;
        CurrentTick = MicrosToTicks(0, CurrentTime);
    }

    /// <summary>Currently just reads the midi from the file into <see cref="Midi"/></summary>
    /// <remarks>Please call <c>base.LoadMidi()</c> if overriding this.</remarks>
    protected override void LoadMidi() {
        InitMidi();
    }

    protected override void DrawGUI() {
        if (ImGuiManager.IsDebugEnabled) {
            if (ImGui.Begin("debug")) {
                ImGui.Text(string.Format("time: {0:F2}", (double)CurrentTime));
                var currTempo = Midi.TempoMap[(long)CurrentTime];
                ImGui.Text(string.Format("current tempo: {0:d} ({1:F2})", currTempo.tempoMicros, MidiUtil.TempoBPM(currTempo.tempoMicros)));
                ImGui.Text(string.Format("set at: {0:d}", currTempo.timeMicros));
                var nextTempoOpt = Midi.TempoMap.GT((long)CurrentTime);
                if (!nextTempoOpt.HasValue) {
                    ImGui.Text("No next tempo.");
                } else {
                    var nextTempo = nextTempoOpt.Value;
                    ImGui.Text(string.Format("next tempo: {0:d} ({1:F2})", nextTempo.tempoMicros, MidiUtil.TempoBPM(nextTempo.tempoMicros)));
                    ImGui.Text(string.Format("at: {0:d}", nextTempo.timeMicros));
                }
            }
            ImGui.End();
        }

        if (ImGui.Begin("MIDI Controls")) {
            ImGui.SetNextItemWidth(128.0f);
            int _midiDelay = MidiDelay;
            if (ImGui.InputInt("Delay (ms, +/-)", ref _midiDelay, 10, 1000))
                MidiDelay = _midiDelay;
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
}