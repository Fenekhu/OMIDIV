using ImGuiNET;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static MidiManager;

public class Standard2D : VisualsComponent {

    protected class TrackInfo {
        /// <summary>The game object that contains all of this track's notes.</summary>
        public GameObject obj;
        /// <summary>The <see cref="CookedMidi.Tracks"/> index that this info represents.</summary>
        public int midiTrack;
        public bool enabled = true;
        public Color trackColor;
        public float lengthFactor = 1f;
        public int pitchOffset = 0;
        public List<int> nowPlaying = new List<int>();
        public int playIndex = 0;
    }

    public Standard2D() { ConfigTag = "s2d"; }

    protected static readonly float GlobalScale = 1/128.0f;

    [SerializeField] protected GameObject NotePrefab;

    /// <remarks>
    /// Note: the index of a track in here may not be the same as the midi track it represents (such as if it's been reordered).
    /// See <see cref="TrackInfo.midiTrack"/>.
    /// </remarks>
    protected TrackInfo[] Tracks = new TrackInfo[0];

    // See user guide for a description of most of these.
    protected float VelocityFactor = 0.75f;
    protected float NoteHeight = 10f;
    protected float NoteHSpacing = 2f;
    protected float NoteVSpacing = 2f;
    protected float PlayedAlpha = 0.05f;

    // these are used to auto-reload the visuals if its been a certain amount of time since a variable was changed requiring a reload.
    protected float LastTrackUpdate = -1f;
    protected float LastNoteUpdate = -1f;
    protected float LastReloadVisuals = -1f;

    protected bool TrackHasNotes(TrackInfo track) => Midi.Tracks[track.midiTrack].notes.Count != 0;

    protected bool IgnoreTrack(TrackInfo trackInfo) => !trackInfo.enabled || !TrackHasNotes(trackInfo);

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
            Tracks[i] = info;
        }

        // now we actually place the tracks and notes.
        ResetTracks();
        ResetNotes();
    }

    protected override void ClearVisuals() {
        foreach (TrackInfo info in Tracks) {
            Destroy(info.obj);
        }
        Tracks = new TrackInfo[0];
    }

    protected void ResetTracks() {
        int i = 0;
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo info = ref Tracks[j];

            bool ignore = IgnoreTrack(info);
            info.obj.SetActive(!ignore);
            if (ignore) continue;

            float posX = info.obj.transform.localPosition.x;
            info.obj.transform.localPosition = new Vector3(posX, info.pitchOffset * (NoteHeight + NoteVSpacing), i);
            Vector3 scale = info.obj.transform.localScale;
            scale.x = info.lengthFactor;
            info.obj.transform.localScale = scale;

            i++;
        }
    }

    protected void ResetNotes(bool justColors = false) {
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo trackInfo = ref Tracks[j];
            if (IgnoreTrack(trackInfo)) continue;

            for (int i = 0; i < trackInfo.obj.transform.childCount; i++) {
                var note = Midi.Tracks[trackInfo.midiTrack].notes[i];
                Transform obj = trackInfo.obj.transform.GetChild(i);

                SetNoteState(trackInfo, i, NoteState.Unplayed);
                if (justColors) continue;

                float noteLength = note.lengthTicks;
                float noteX = note.startTick + noteLength * 0.5f + NoteHSpacing * 0.5f;
                float noteY = (note.pitch - Midi.NoteRange/2) * (NoteHeight + NoteVSpacing);
                obj.localPosition = new Vector3(noteX, noteY);
                obj.localScale = new Vector3(noteLength - NoteHSpacing, NoteHeight, NoteHeight);
            }
        }
    }

    protected override void MovePlay(decimal ticks, decimal microseconds) {
        foreach (TrackInfo info in Tracks) {
            Vector3 ds = new Vector3((float)(ticks * (decimal)info.lengthFactor), 0, 0);
            info.obj.transform.localPosition -= ds;
        }
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

    protected override void DrawGUI() {
        base.DrawGUI();

        bool updateTracks = false;
        bool updateNotes = false;

        if (ImGui.Begin("MIDI Controls")) {
            ImGui.SetNextItemWidth(128f);
            ImGui.SliderFloat("Played Note Alpha", ref PlayedAlpha, 0f, 1f);

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
                        updateTracks = true;
                    }
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);
                    if (ImGui.Button(string.Format("v##trDown{0:d}", i), buttonSize) && i != Tracks.Length - skipCount) {
                        (Tracks[i], Tracks[i+skipCount]) = (Tracks[i+skipCount], Tracks[i]);
                        updateTracks = true;
                    }
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);
                    if (ImGui.Checkbox(string.Format("##chtr{0:d}", i), ref Tracks[i].enabled))
                        updateTracks = true;
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
                        if (ImGui.InputInt("Pitch offset", ref track.pitchOffset)) updateTracks = true;
                        ImGui.TreePop();
                    }
                }
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Note Options")) {
                ImGui.PushItemWidth(128f);
                if (ImGui.SliderFloat("Velocity Intensity", ref VelocityFactor, 0f, 1f)) updateNotes = true;
                if (ImGui.InputFloat("Height", ref NoteHeight)) updateTracks = updateNotes = true;
                Vector3 newScale = transform.localScale / GlobalScale;
                if (ImGui.InputFloat("Length Factor", ref newScale.x)) transform.localScale = newScale * GlobalScale;
                if (ImGui.InputFloat("Horizontal Spacing", ref NoteHSpacing)) updateNotes = true;
                if (ImGui.InputFloat("Vertical Spacing", ref NoteVSpacing)) updateTracks = updateNotes = true;
                ImGui.PopItemWidth();
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
        Config.Set(tag+".noteHeight", NoteHeight);
        Config.Set(tag+".noteHSpacing", NoteHSpacing);
        Config.Set(tag+".noteVSpacing", NoteVSpacing);
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
        Config.TryGet(tag+".noteHeight", ref NoteHeight);
        Config.TryGet(tag+".noteHSpacing", ref NoteHSpacing);
        Config.TryGet(tag+".noteVSpacing", ref NoteVSpacing);
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
