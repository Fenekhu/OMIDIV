using ImGuiNET;
using System.Collections.Generic;
using UnityEngine;

public class Base3DTrackInfo {
    public GameObject obj;
    public int midiTrack;
    public bool enabled = true;
    public Color trackColor;
    public float lengthFactor = 1f;
    public List<int> nowPlaying = new List<int>();
    public int playIndex = 0;
    public uint noteSides;
    public bool updateMeshes = true;
}

public abstract class Base3D<TrackInfo> : MidiScene where TrackInfo : Base3DTrackInfo, new() {

    protected static readonly float GlobalScale = 1/128.0f;

    [SerializeField] protected GameObject NotePrefab;

    protected TrackInfo[] Tracks = new TrackInfo[0];

    protected float VelocityFactor = 0.75f;
    protected int SideCount = 4;
    protected uint SideCountPrev = 0;
    protected float NoteRotation = 0f;
    protected float NoteHeight = 10f;
    protected float NoteHSpacing = 2f;
    protected float PlayedAlpha = 0.05f;

    protected float LastTrackUpdate = -1f;
    protected float LastNoteUpdate = -1f;
    protected float LastReloadVisuals = -1f;

    protected override void Start() {
        base.Start();
        transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
    }

    protected override void Update() {
        if (LastReloadVisuals > 0 && Time.realtimeSinceStartup - LastReloadVisuals > 0.5f) { NeedsVisualReload = true; LastReloadVisuals = -1f; }
        base.Update();
        if (LastTrackUpdate > 0 && Time.realtimeSinceStartup - LastTrackUpdate > 0.1f) { ResetTracks(); LastTrackUpdate = -1f; }
        if (LastNoteUpdate > 0 && Time.realtimeSinceStartup - LastNoteUpdate > 0.5f) { ResetNotes(false); LastNoteUpdate = -1f; }

        if (!IsPlaying) return;

        UpdateNowPlayingVisuals();
    }

    protected enum NoteState { Unplayed, Playing, Played }
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
                    SetNoteState(track, track.playIndex, NoteState.Played);
                    track.playIndex++;
                } else break;
            }
        }
    }

    protected override void InitVisuals() {
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

        ResetTracks();
        ResetNotes();
    }

    protected abstract void ResetTracks();
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

    protected override void MovePlay(double ticks) {
        //MainCam.transform.Translate((float)ticks, 0, 0, Space.World);
        foreach (TrackInfo info in Tracks) {
            Vector3 ds = new Vector3((float)ticks * info.lengthFactor, 0, 0);
            info.obj.transform.localPosition -= ds;
        }
    }

    protected override void DrawGUI() {
        base.DrawGUI();

        bool updateTracks = false;
        bool updateNotes = false;
        bool updateVisuals = false;

        if (ImGui.Begin("MIDI Controls")) {
            ImGui.SetNextItemWidth(128f);
            ImGui.SliderFloat("Played Note Alpha", ref PlayedAlpha, 0f, 1f);

            if (ImGui.TreeNode("Tracks")) {
                float buttonDim = ImGui.GetFrameHeight();
                Vector2 buttonSize = new Vector2(buttonDim, buttonDim);
                for (int i = 0; i < Tracks.Length; i++) {
                    if (ImGui.Button(string.Format("^##trUp{0:d}", i), buttonSize) && i != 0) {
                        (Tracks[i-1], Tracks[i]) = (Tracks[i], Tracks[i-1]);
                        updateTracks = true;
                    }
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);
                    if (ImGui.Button(string.Format("v##trDown{0:d}", i), buttonSize) && i != Tracks.Length - 1) {
                        (Tracks[i], Tracks[i+1]) = (Tracks[i+1], Tracks[i]);
                        updateTracks = true;
                    }
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);
                    if (ImGui.Checkbox(string.Format("##chtr{0:d}", i), ref Tracks[i].enabled))
                        updateTracks = true;
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);
                    if (ImGui.TreeNode(string.Format("{0:s}##tr{1:d}", Midi.Tracks[Tracks[i].midiTrack].name, i))) {
                        ref TrackInfo track = ref Tracks[i];
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
                ImGui.TreePop();
            }
        }
        ImGui.End();

        if (AutoReload) {
            if (updateTracks) LastTrackUpdate = Time.unscaledTime;
            if (updateNotes) LastNoteUpdate = Time.unscaledTime;
            if (updateVisuals) LastReloadVisuals = Time.unscaledTime;
        }
    }

    protected abstract string GetConfigTag();

    protected override void WriteConfig() {
        base.WriteConfig();

        string tag = GetConfigTag();

        Config.Set(tag+".velFactor", VelocityFactor);
        Config.Set(tag+".noteSides", SideCount);
        Config.Set(tag+".noteRotation", NoteRotation);
        Config.Set(tag+".noteHeight", NoteHeight);
        Config.Set(tag+".noteHSpacing", NoteHSpacing);
        Config.Set(tag+".lengthScale", transform.localScale.x / GlobalScale);
        Config.Set(tag+".playedAlpha", PlayedAlpha);

        List<Color> trackColors = new List<Color>();
        trackColors.AddRange(TrackColors);
        for (int i = 0; i < Tracks.Length; i++) {
            if (i < trackColors.Count - 1) {
                trackColors[i] = Tracks[i].trackColor;
            } else {
                trackColors.Add(Tracks[i].trackColor);
            }
        }
        Config.Set(tag+".trackColors", trackColors.ToArray());
    }

    protected override void ReadConfig() {
        base.ReadConfig();

        string tag = GetConfigTag();

        Config.TryGet(tag+".velFactor", ref VelocityFactor);
        Config.TryGet(tag+".noteSides", ref SideCount);
        Config.TryGet(tag+".noteRotation", ref NoteRotation);
        Config.TryGet(tag+".noteHeight", ref NoteHeight);
        Config.TryGet(tag+".noteHSpacing", ref NoteHSpacing);
        Vector3 scale = transform.localScale / GlobalScale;
        Config.TryGet(tag+".lengthScale", ref scale.x); transform.localScale = scale * GlobalScale;
        Config.TryGet(tag+".playedAlpha", ref PlayedAlpha);

        List<Color> trackColors = new List<Color>();
        Config.Get(tag+".trackColors", trackColors);
        if (trackColors.Count > 0) {
            TrackColors.Clear();
            TrackColors.AddRange(trackColors);
        }
    }
}