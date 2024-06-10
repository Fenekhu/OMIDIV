using ImGuiNET;
using System.Collections.Generic;
using UnityEngine;
using static MidiManager;

/// <summary>
/// Base TrackInfo for a scene that inherits from <see cref="Base3DTrackInfo"/>
/// </summary>
public class Base3DTrackInfo {
    /// <summary>The game object that contains all of this track's notes.</summary>
    public GameObject obj;
    /// <summary>The <see cref="CookedMidi.Tracks"/> index that this info represents.</summary>
    public int midiTrack;
    public bool enabled = true;
    public Color trackColor;
    public float lengthFactor = 1f;
    public List<int> nowPlaying = new List<int>();
    public int playIndex = 0;
    public uint noteSides;
    public bool updateMeshes = true;
}

/// <summary>
/// A base class for <see cref="Standard3D"/> and <see cref="Circle3D"/> visualizations, since they share much of the same code.<br/>
/// Custom subclasses can be made as well that lay out notes in unique ways. 
/// If you do that, respect <see cref="GlobalScale"/> by making any GameObjects children of this, or manually adjusting their scale.
/// </summary>
/// <typeparam name="TrackInfo"></typeparam>
public abstract class Base3D<TrackInfo> : VisualsComponent where TrackInfo : Base3DTrackInfo, new() {

    protected static readonly float GlobalScale = 1/128.0f;

    [SerializeField] protected GameObject NotePrefab;

    /// <remarks>
    /// Note: the index of a track in here may not be the same as the midi track it represents (such as if it's been reordered).
    /// See <see cref="Base3DTrackInfo.midiTrack"/>.
    /// </remarks>
    protected TrackInfo[] Tracks = new TrackInfo[0];

    // See user guide for a description of most of these.
    protected float VelocityFactor = 0.75f;
    protected int SideCount = 4;
    protected uint SideCountPrev = 0;
    protected float NoteRotation = 0f;
    protected float NoteHeight = 10f;
    protected float NoteHSpacing = 2f;
    protected float PlayedAlpha = 0.05f;

    // these are used to auto-reload the visuals if its been a certain amount of time since a variable was changed requiring a reload.
    protected float LastTrackUpdate = -1f;
    protected float LastNoteUpdate = -1f;
    protected float LastReloadVisuals = -1f;

    protected void Start() {
        transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
    }

    protected override void Update() {
        base.Update();
        // check if we need to reload first, becase base.Update() calls the reload.
        if (LastReloadVisuals > 0 && Time.realtimeSinceStartup - LastReloadVisuals > 0.5f) { SceneController.NeedsVisualReload = true; LastReloadVisuals = -1f; }
        if (LastTrackUpdate > 0 && Time.realtimeSinceStartup - LastTrackUpdate > 0.1f) { ResetTracks(); LastTrackUpdate = -1f; }
        if (LastNoteUpdate > 0 && Time.realtimeSinceStartup - LastNoteUpdate > 0.5f) { ResetNotes(false); LastNoteUpdate = -1f; }

        if (!IsPlaying) return;

        UpdateNowPlayingVisuals();
    }

    protected enum NoteState { Unplayed, Playing, Played }
    /// <summary>
    /// Updates the visuals for a note at a given <paramref name="noteIndex"/> of a specific <paramref name="trackInfo"/>.
    /// </summary>
    protected virtual void SetNoteState(TrackInfo trackInfo, int noteIndex, NoteState state) {
        MidiNote midiNote = Midi.Tracks[trackInfo.midiTrack].notes[noteIndex];
        Material mat = trackInfo.obj.transform.GetChild(noteIndex).GetComponent<MeshRenderer>().material;

        switch (state) {
        case NoteState.Unplayed:
            mat.color = trackInfo.trackColor.MultiplyRGB(1f - VelocityFactor * (1f - midiNote.velocity/127f));
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
            break;
        case NoteState.Playing:
            mat.color = trackInfo.trackColor.LerpWith(Color.white, 0.5f, false);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", trackInfo.trackColor * 4);
            break;
        case NoteState.Played:
            mat.color = trackInfo.trackColor.WithAlpha(PlayedAlpha);
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
            break;
        }
    }

    protected virtual void UpdateNowPlayingVisuals() {
        // for each track
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo track = ref Tracks[j];
            Track midiTrack = Midi.Tracks[track.midiTrack];

            // remove notes that are no longer playing
            for (int i = 0; i < track.nowPlaying.Count; i++) {
                int noteI = track.nowPlaying[i];
                if ((long)midiTrack.notes[noteI].endTick < CurrentTick) {
                    SetNoteState(track, noteI, NoteState.Played);
                    track.nowPlaying.RemoveAt(i--);
                }
            }

            // start at the last activated note, and then activate all notes
            // that should be activated, stopping at the first note with a 
            // start time beyond the current time
            while (true) {
                if (track.playIndex >= midiTrack.notes.Count) break;
                MidiNote note = midiTrack.notes[track.playIndex];
                if ((long)note.startTick < CurrentTick) {
                    track.nowPlaying.Add(track.playIndex);
                    SetNoteState(track, track.playIndex, NoteState.Playing);
                    track.playIndex++;
                } else break;
            }
        }
    }

    protected override void CreateVisuals() {
        // creates all the tracks and GameObjects, but doesn't actually place them.
        Tracks = new TrackInfo[Midi.Tracks.Count];
        for (int i = 0; i < Midi.Tracks.Count; i++) {
            GameObject go = new GameObject(Midi.Tracks[i].name);
            go.transform.SetParent(transform, false);

            Track track = Midi.Tracks[i];
            foreach (MidiNote note in track.notes) {
                GameObject noteGO = Instantiate(NotePrefab);
                noteGO.transform.SetParent(go.transform, false);
            }

            TrackInfo info = new TrackInfo();
            info.obj = go;
            info.trackColor = TrackColors[i % TrackColors.Count];
            info.midiTrack = i;
            info.noteSides = (uint)SideCount;
            Tracks[i] = info;
        }

        // now we actually place the tracks and notes.
        ResetTracks();
        ResetNotes();
    }

    protected bool TrackHasNotes(TrackInfo track) => Midi.Tracks[track.midiTrack].notes.Count != 0;

    protected bool IgnoreTrack(TrackInfo trackInfo) => !trackInfo.enabled || !TrackHasNotes(trackInfo);

    /// <summary>Updates the GameObjects for track objects, and other things that don't need to be changed per-note.</summary>
    protected abstract void ResetTracks();
    /// <summary>Updates GameObjects for note objects.</summary>
    /// <param name="justColors">If true, only reset note states to <see cref="NoteState.Unplayed"/>. Otherwise, recalculate positions, meshes, etc.</param>
    protected abstract void ResetNotes(bool justColors = false);

    protected override void ClearVisuals() {
        foreach (TrackInfo info in Tracks) {
            Destroy(info.obj);
        }
        Tracks = new TrackInfo[0];
    }

    protected override void Restart() {
        for (int i = 0; i < Tracks.Length; i++) {
            ref TrackInfo track = ref Tracks[i];
            
            // reset the x of each track
            Vector3 newPos = track.obj.transform.localPosition;
            newPos.x = 0;
            track.obj.transform.localPosition = newPos;

            Track midiTrack = Midi.Tracks[track.midiTrack];
            for (int j = 0; j < track.playIndex; j++) {
                SetNoteState(track, j, NoteState.Unplayed);
            }

            track.nowPlaying.Clear();
            track.playIndex = 0;
        }
        base.Restart();
    }

    protected override void MovePlay(decimal ticks) {
        //MainCam.transform.Translate((float)ticks, 0, 0, Space.World);
        foreach (TrackInfo info in Tracks) {
            Vector3 ds = new Vector3((float)(ticks * (decimal)info.lengthFactor), 0, 0);
            info.obj.transform.localPosition -= ds;
        }
    }

    /// <summary>
    /// Draw the general controls in the MIDI Controls window for this visualization.
    /// </summary>
    /// <param name="updateTracks">Set this to true if the tracks need to be updated.</param>
    /// <param name="updateNotes">Set this to true if the notes need to be updated.</param>
    protected virtual void DrawGeneralMidiControls(ref bool updateTracks, ref bool updateNotes) { }

    /// <summary>
    /// Draw the individual controls within the tree for a track. Will be called for each track.
    /// </summary>
    /// <param name="trackInfo">The track thats going to have its controls drawn.</param>
    /// <param name="updateTracks">Set this to true if the tracks need to be updated.</param>
    /// <param name="updateNotes">Set this to true if the notes need to be updated.</param>
    protected virtual void DrawIndividualTrackControls(ref TrackInfo trackInfo, ref bool updateTracks, ref bool updateNotes) { }

    /// <summary>
    /// Draw the controls for the "Note Controls" tree.
    /// </summary>
    /// <param name="updateTracks">Set this to true if the tracks need to be updated.</param>
    /// <param name="updateNotes">Set this to true if the notes need to be updated.</param>
    protected virtual void DrawNoteControls(ref bool updateTracks, ref bool updateNotes) { }

    /// <summary>
    /// Called when the track order has been changed or tracks have been enabled/disabled.
    /// </summary>
    /// <param name="updateTracks">Set this to true if the tracks need to be updated.</param>
    /// <param name="updateNotes">Set this to true if the notes need to be updated.</param>
    protected abstract void TrackListChanged(ref bool updateTracks, ref bool updateNotes);

    protected override void DrawGUI() {
        base.DrawGUI();

        bool updateTracks = false;
        bool updateNotes = false;

        if (ImGui.Begin("MIDI Controls")) {
            ImGui.SetNextItemWidth(128f);
            ImGui.SliderFloat("Played Note Alpha", ref PlayedAlpha, 0f, 1f);
            DrawGeneralMidiControls(ref updateTracks, ref updateNotes);

            if (ImGui.TreeNode("Tracks")) {
                float buttonDim = ImGui.GetFrameHeight();
                Vector2 buttonSize = new Vector2(buttonDim, buttonDim);
                int skipCount = 1;
                for (int i = 0; i < Tracks.Length; i++) {
                    if (!TrackHasNotes(Tracks[i])) {
                        skipCount++;
                        continue;
                    } else {
                        skipCount = 1;
                    }

                    if (ImGui.Button(string.Format("^##trUp{0:d}", i), buttonSize) && i >= skipCount) {
                        (Tracks[i-skipCount], Tracks[i]) = (Tracks[i], Tracks[i-skipCount]);
                        TrackListChanged(ref updateTracks, ref updateNotes);
                    }
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);
                    if (ImGui.Button(string.Format("v##trDown{0:d}", i), buttonSize) && i != Tracks.Length - skipCount) {
                        (Tracks[i], Tracks[i+skipCount]) = (Tracks[i+skipCount], Tracks[i]);
                        TrackListChanged(ref updateTracks, ref updateNotes);
                    }
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);
                    if (ImGui.Checkbox(string.Format("##chtr{0:d}", i), ref Tracks[i].enabled))
                        TrackListChanged(ref updateTracks, ref updateNotes);
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);

                    ref TrackInfo track = ref Tracks[i];
                    if (ImGui.TreeNode(string.Format("{0:s}##tr{1:d}", Midi.Tracks[Tracks[i].midiTrack].name, i))) {
                        // --------------- individual track options -----------------------
                        Vector4 color = track.trackColor;
                        if (ImGui.ColorEdit4("Track color", ref color, ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoInputs)) {
                            track.trackColor = color;
                            updateNotes = true;
                        }
                        if (ImGui.InputFloat("Length Factor", ref track.lengthFactor)) updateTracks = true;
                        int sides = (int)track.noteSides;
                        if (ImGui.SliderInt("Note Sides", ref sides, 3, 8)) {
                            track.noteSides = (uint)sides;
                            track.updateMeshes = true;
                            updateNotes = true;
                        }
                        DrawIndividualTrackControls(ref track, ref updateTracks, ref updateNotes);
                        ImGui.TreePop();
                    }
                }
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Note Options")) {
                ImGui.PushItemWidth(128f);
                if (ImGui.SliderFloat("Velocity Intensity", ref VelocityFactor, 0f, 1f)) updateNotes = true;
                if (ImGui.SliderInt("Note Sides", ref SideCount, 3, 8)) updateTracks = updateNotes = true;
                if (ImGui.SliderFloat("Note Rotation", ref NoteRotation, -180f, 180f, "%.1f deg")) updateNotes = true;
                if (ImGui.InputFloat("Height", ref NoteHeight)) updateTracks = updateNotes = true;
                Vector3 newScale = transform.localScale / GlobalScale;
                if (ImGui.InputFloat("Length Factor", ref newScale.x)) transform.localScale = newScale * GlobalScale;
                if (ImGui.InputFloat("Horizontal Spacing", ref NoteHSpacing)) updateNotes = true;
                ImGui.PopItemWidth();
                DrawNoteControls(ref updateTracks, ref updateNotes);
                ImGui.TreePop();
            }
        }
        ImGui.End();

        if (AutoApplyChanges) {
            if (updateTracks) LastTrackUpdate = Time.unscaledTime;
            if (updateNotes) LastNoteUpdate = Time.unscaledTime;
        }
    }

    private struct icpair {
        public int i; public Color c;
        public icpair(int i, Color c) {
            this.i = i;
            this.c = c;
        }
    }

    protected override void WriteConfig() {
        base.WriteConfig();

        string tag = ConfigTag;

        Config.Set(tag+".velFactor", VelocityFactor);
        Config.Set(tag+".noteSides", SideCount);
        Config.Set(tag+".noteRotation", NoteRotation);
        Config.Set(tag+".noteHeight", NoteHeight);
        Config.Set(tag+".noteHSpacing", NoteHSpacing);
        Config.Set(tag+".lengthScale", transform.localScale.x / GlobalScale);
        Config.Set(tag+".playedAlpha", PlayedAlpha);

        List<icpair> trackColors = new List<icpair>(TrackColors.Count);
        foreach (var kvp in TrackColors)
            trackColors.Add(new icpair(kvp.Key, kvp.Value));
        Config.Set(tag+".trackColors", trackColors.ToArray());
    }

    protected override void ReadConfig() {
        base.ReadConfig();

        string tag = ConfigTag;

        Config.TryGet(tag+".velFactor", ref VelocityFactor);
        Config.TryGet(tag+".noteSides", ref SideCount);
        Config.TryGet(tag+".noteRotation", ref NoteRotation);
        Config.TryGet(tag+".noteHeight", ref NoteHeight);
        Config.TryGet(tag+".noteHSpacing", ref NoteHSpacing);
        Vector3 scale = transform.localScale / GlobalScale;
        Config.TryGet(tag+".lengthScale", ref scale.x); transform.localScale = scale * GlobalScale;
        Config.TryGet(tag+".playedAlpha", ref PlayedAlpha);

        List<icpair> trackColors = new List<icpair>();
        Config.Get(tag+".trackColors", trackColors);
        foreach (var kvp in trackColors) {
            TrackColors[kvp.i] = kvp.c;
        }
    }
}