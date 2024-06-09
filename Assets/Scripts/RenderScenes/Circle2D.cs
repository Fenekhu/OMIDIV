using ImGuiNET;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static MidiManager;

/// <summary>
/// A visualization where the pitches of the track appear as pads placed radially. The pads light up when that pitch is played.
/// </summary>
public class Circle2D : VisualsComponent {

    struct TrackInfo {
        public GameObject obj;
        public int midiTrack;
        public bool enabled;
        public Color trackColor;
        public float angleOffset;
        public float angleRange;
        public List<int> nowPlaying;
        public int playIndex;
        public uint noteSides;
        public bool updateMeshes;
        public float[] fadeTimes;
    }

    static readonly float GlobalScale = 1/128.0f;

    public Circle2D() { ConfigTag = "c2d"; }

    [SerializeField] GameObject NotePrefab;

    TrackInfo[] Tracks = new TrackInfo[0];

    float StartRadius = 450.0f;
    float DeltaRadius = -32f;
    float AngleOffset = 0f;
    float AngleRange = 360f;
    bool AlignEnds = false;
    float VelocityFactor = 0.75f;
    int SideCount = 4;
    uint SideCountPrev = 0;
    float NoteRotation = 0f;
    float NoteSize = 30f;
    float NoteAlpha = 0.125f;
    float NoteFadeTime = 0.5f;

    protected float LastTrackUpdate = -1f;
    protected float LastNoteUpdate = -1f;
    protected float LastReloadVisuals = -1f;

    protected void Start() {
        transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);
    }

    protected void Update() {
        if (LastReloadVisuals > 0 && Time.realtimeSinceStartup - LastReloadVisuals > 0.5f) { SceneController.NeedsVisualReload = true; LastReloadVisuals = -1f; }
        if (LastTrackUpdate > 0 && Time.realtimeSinceStartup - LastTrackUpdate > 0.2f) { ResetTracks(); LastTrackUpdate = -1f; }
        if (LastNoteUpdate > 0 && Time.realtimeSinceStartup - LastNoteUpdate > 0.2f) { ResetNotes(false); LastNoteUpdate = -1f; }

        if (!IsPlaying) return;

        // fade the notes
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo track = ref Tracks[j];
            Track midiTrack = Midi.Tracks[track.midiTrack];
            for (int i = 0; i < track.fadeTimes.Length; i++) {
                if (track.fadeTimes[i] == 0) continue;
                var mat = track.obj.transform.GetChild(i).GetComponent<MeshRenderer>().material;
                if (track.fadeTimes[i] > 0) {
                    mat.color = track.trackColor.WithAlpha(math.remap(0, NoteFadeTime, NoteAlpha, 1, track.fadeTimes[i]));
                    mat.SetColor("_EmissionColor", track.trackColor * math.min(4, 8 * math.unlerp(0, NoteFadeTime, track.fadeTimes[i])));
                    track.fadeTimes[i] -= (float)FrameDeltaTime;
                    if (track.fadeTimes[i] == 0) track.fadeTimes[i]--; // ensure that this doesn't land on exactly zero and enters the next block next frame
                } else if (track.fadeTimes[i] < 0) { // properly "shut off" the note only once (instead of every frame if this was <= 0)
                    mat.color = track.trackColor.WithAlpha(NoteAlpha);
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                    track.fadeTimes[i] = 0;
                }
            }
        }

        // update "now playing"
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo track = ref Tracks[j];
            Track midiTrack = Midi.Tracks[track.midiTrack];
            for (int i = 0; i < track.nowPlaying.Count; i++) {
                int noteI = track.nowPlaying[i];
                if ((long)midiTrack.notes[noteI].endTick < CurrentTick) {
                    MidiNote note = midiTrack.notes[noteI];
                    track.fadeTimes[note.pitch - midiTrack.pitchRange.lower] = NoteFadeTime * (1f - VelocityFactor * (1f - note.velocity/127f));
                    track.nowPlaying.RemoveAt(i--);
                }
            }
            while (true) {
                if (track.playIndex >= midiTrack.notes.Count) break;
                MidiNote note = midiTrack.notes[track.playIndex];
                if ((long)note.startTick < CurrentTick) {
                    track.nowPlaying.Add(track.playIndex);
                    track.fadeTimes[note.pitch - midiTrack.pitchRange.lower] = 0;
                    var mat = track.obj.transform.GetChild(note.pitch - midiTrack.pitchRange.lower).GetComponent<MeshRenderer>().material;
                    mat.color = track.trackColor;
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", track.trackColor * 4);
                    track.playIndex++;
                } else break;
            }
        }
    }

    protected override void CreateVisuals() {
        Tracks = new TrackInfo[Midi.Tracks.Count];
        for (int i = 0; i < Midi.Tracks.Count; i++) {
            GameObject go = new GameObject(Midi.Tracks[i].name);
            go.transform.SetParent(transform, false);

            Track track = Midi.Tracks[i];
            for (int j = track.pitchRange.lower; j <= track.pitchRange.upper; j++) {
                GameObject noteGO = Instantiate(NotePrefab);
                noteGO.transform.SetParent(go.transform, false);
            }

            TrackInfo info = new TrackInfo();
            info.obj = go;
            info.midiTrack = i;
            info.enabled = track.notes.Count != 0;
            info.trackColor = TrackColors[i % TrackColors.Count];
            info.angleOffset = 0f;
            info.angleRange = 360f;
            info.nowPlaying = new List<int>();
            info.playIndex = 0;
            info.noteSides = (uint)SideCount;
            info.updateMeshes = true;
            info.fadeTimes = new float[Mathf.Max(0, track.pitchRange.upper - track.pitchRange.lower + 1)];
            Tracks[i] = info;
        }

        ResetTracks();
        ResetNotes();
    }

    private bool TrackHasNotes(TrackInfo track) => Midi.Tracks[track.midiTrack].notes.Count != 0;

    private bool IgnoreTrack(TrackInfo trackInfo) => !trackInfo.enabled || !TrackHasNotes(trackInfo);

    private void ResetTracks() {
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo info = ref Tracks[j];
            Transform tfm = info.obj.transform;
            bool ignore = IgnoreTrack(info);
            info.obj.SetActive(!ignore);
            if (ignore) continue;
            tfm.localEulerAngles = new Vector3(0f, 0f, AngleOffset + info.angleOffset);

            if (SideCount != SideCountPrev) {
                info.noteSides = (uint)SideCount;
                info.updateMeshes = true;
            }
        }
        SideCountPrev = (uint)SideCount;
    }

    private void ResetNotes(bool justColors = false) {
        int j = 0;
        for (int rj = 0; rj < Tracks.Length; rj++) {
            ref TrackInfo trackInfo = ref Tracks[rj];
            if (IgnoreTrack(trackInfo)) continue;

            // will be null if the mesh doesn't need to be updated
            Mesh newMesh = trackInfo.updateMeshes? GeometryUtil.GetNSidedPlaneMesh(trackInfo.noteSides) : null;

            Track midiTrack = Midi.Tracks[trackInfo.midiTrack];
            float trackRadius = StartRadius + j * DeltaRadius;

            for (int i = 0; i < trackInfo.obj.transform.childCount; i++) {
                Transform obj = trackInfo.obj.transform.GetChild(i);

                Material mat = obj.GetComponent<MeshRenderer>().material;
                mat.color = trackInfo.trackColor.WithAlpha(NoteAlpha);
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
                if (justColors) continue;

                if (newMesh is not null) obj.GetComponent<MeshFilter>().mesh = newMesh;

                float noteTheta = Mathf.Deg2Rad * math.remap(midiTrack.pitchRange.lower, midiTrack.pitchRange.upper + (AlignEnds? 0f:1f), trackInfo.angleRange*-0.5f, trackInfo.angleRange*0.5f, midiTrack.pitchRange.lower + i);
                float noteX = trackRadius * Mathf.Sin(noteTheta);
                float noteY = trackRadius * Mathf.Cos(noteTheta);
                obj.localPosition = new Vector3(noteX, noteY);
                obj.localScale = new Vector3(NoteSize, NoteSize, 1f);
                obj.localEulerAngles = new Vector3(0, 0, NoteRotation - Mathf.Rad2Deg * noteTheta);
            }
            trackInfo.updateMeshes = false;

            Array.Clear(trackInfo.fadeTimes, 0, trackInfo.fadeTimes.Length);

            j++;
        }
    }

    protected override void ClearVisuals() {
        foreach (TrackInfo info in Tracks) {
            Destroy(info.obj);
        }
        Tracks = new TrackInfo[0];
    }

    protected override void Restart() {
        for (int i = 0; i < Tracks.Length; i++) {
            ref TrackInfo info = ref Tracks[i];
            Vector3 newPos = info.obj.transform.localPosition;
            newPos.x = 0;
            info.obj.transform.localPosition = newPos;
            info.nowPlaying.Clear();
            info.playIndex = 0;
        }
        base.Restart();

        ResetNotes(true);
    }

    // nothing needs to be done here because visuals only need to know the current time.
    protected override void MovePlay(decimal ticks) {}

    protected override void DrawGUI() {
        base.DrawGUI();

        bool updateTracks = false;
        bool updateNotes = false;

        if (ImGui.Begin("MIDI Controls")) {
            ImGui.PushItemWidth(128f);
            if (ImGui.InputFloat("Track Start Radius", ref StartRadius, 1.0f, 5.0f)) updateNotes = true;
            if (ImGui.InputFloat("Track Radius Delta", ref DeltaRadius, 1.0f, 5.0f)) updateNotes = true;
            if (ImGui.SliderFloat("Angle Range", ref AngleRange, 0, 360f, "%.1f deg")) {
                for (int i = 0; i < Tracks.Length; i++) { ref TrackInfo track = ref Tracks[i]; track.angleRange = AngleRange; }
                updateNotes = true;
            }
            if (ImGui.SliderFloat("Angle Offset", ref AngleOffset, -180f, 180f, "%.1f deg")) updateTracks = true;
            ImGui.PopItemWidth();
            if (ImGui.Checkbox("Align Ends", ref AlignEnds)) updateNotes = true;

            if (ImGui.TreeNode("Tracks")) {
                float buttonDim = ImGui.GetFrameHeight();
                Vector2 buttonSize = new Vector2(buttonDim, buttonDim); int skipCount = 1;
                for (int i = 0; i < Tracks.Length; i++) {
                    if (!TrackHasNotes(Tracks[i])) {
                        skipCount++;
                        continue;
                    } else {
                        skipCount = 1;
                    }

                    if (ImGui.Button(string.Format("^##trUp{0:d}", i), buttonSize) && i >= skipCount) {
                        (Tracks[i-skipCount], Tracks[i]) = (Tracks[i], Tracks[i-skipCount]);
                        updateTracks = updateNotes = true;
                    }
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);
                    if (ImGui.Button(string.Format("v##trDown{0:d}", i), buttonSize) && i != Tracks.Length - skipCount) {
                        (Tracks[i], Tracks[i+skipCount]) = (Tracks[i+skipCount], Tracks[i]);
                        updateTracks = updateNotes = true;
                    }
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);
                    if (ImGui.Checkbox(string.Format("##chtr{0:d}", i), ref Tracks[i].enabled))
                        updateTracks = updateNotes = true;
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);

                    ref TrackInfo track = ref Tracks[i];
                    if (TrackHasNotes(track) && ImGui.TreeNode(string.Format("{0:s}##tr{1:d}", Midi.Tracks[track.midiTrack].name, i))) {
                        // --------------- individual track options -----------------------
                        ImGui.PushItemWidth(96.0f);
                        Vector4 color = track.trackColor;
                        if (ImGui.ColorEdit4("Track Color", ref color, ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoInputs)) {
                            track.trackColor = color;
                            updateNotes = true;
                        }
                        if (ImGui.SliderFloat("Angle Offset", ref track.angleOffset, -180f, 180f, "%.1f deg")) updateTracks = true;
                        if (ImGui.SliderFloat("Angle Range", ref track.angleRange, 0, 360f, "%.1f deg")) updateNotes = true;
                        int sides = (int)track.noteSides;
                        if (ImGui.SliderInt("Note Sides", ref sides, 3, 8)) {
                            track.noteSides = (uint)sides;
                            track.updateMeshes = true;
                            updateNotes = true;
                        }
                        ImGui.PopItemWidth();
                        ImGui.TreePop();
                    }
                }
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Note Options")) {
                ImGui.PushItemWidth(128f);
                ImGui.SliderFloat("Velocity Intensity", ref VelocityFactor, 0f, 1f);
                if (ImGui.SliderInt("Note Sides", ref SideCount, 3, 8)) (updateTracks, updateNotes) = (true, true);
                if (ImGui.SliderFloat("Note Rotation", ref NoteRotation, -180f, 180f, "%.1f deg")) updateNotes = true;
                if (ImGui.InputFloat("Note Size", ref NoteSize)) updateNotes = true;
                if (ImGui.SliderFloat("Note Alpha", ref NoteAlpha, 0f, 1f, "%.3f", 3)) updateNotes = true;
                ImGui.InputFloat("Fade Time", ref NoteFadeTime);
                NoteFadeTime = math.max(NoteFadeTime, 0.00048828125f); // things break if NoteFadeTime is 0. 1/2048 so the display rounds down to 0.000
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

        Config.Set("c2d.startRadius", StartRadius);
        Config.Set("c2d.deltaRadius", DeltaRadius);
        Config.Set("c2d.angleRadius", AngleOffset);
        Config.Set("c2d.angleRange", AngleRange);
        Config.Set("c2d.velFactor", VelocityFactor);
        Config.Set("c2d.alignEnds", AlignEnds);
        Config.Set("c2d.noteSides", SideCount);
        Config.Set("c2d.noteRotation", NoteRotation);
        Config.Set("c2d.noteSize", NoteSize);
        Config.Set("c2d.noteAlpha", NoteAlpha);
        Config.Set("c2d.noteFadeTime", NoteFadeTime);

        List<icpair> trackColors = new List<icpair>(TrackColors.Count);
        foreach (var kvp in TrackColors)
            trackColors.Add(new icpair(kvp.Key, kvp.Value));
        Config.Set("c2d.trackColors", trackColors.ToArray());
    }

    protected override void ReadConfig() {
        base.ReadConfig();

        Config.TryGet("c2d.startRadius", ref StartRadius);
        Config.TryGet("c2d.deltaRadius", ref DeltaRadius);
        Config.TryGet("c2d.angleOffset", ref AngleOffset);
        Config.TryGet("c2d.angleRange", ref AngleRange);
        Config.TryGet("c2d.alignEnds", ref AlignEnds);
        Config.TryGet("c2d.velFactor", ref VelocityFactor);
        Config.TryGet("c2d.noteSides", ref SideCount);
        Config.TryGet("c2d.noteRotation", ref NoteRotation);
        Config.TryGet("c2d.noteSize", ref NoteSize);
        Config.TryGet("c2d.noteAlpha", ref NoteAlpha);
        Config.TryGet("c2d.noteFadeTime", ref NoteFadeTime);

        List<icpair> trackColors = new List<icpair>();
        Config.Get("c2d.trackColors", trackColors);
        foreach (var kvp in trackColors) {
            TrackColors[kvp.i] = kvp.c;
        }
    }
}
