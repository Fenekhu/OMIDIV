using ImGuiNET;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class Circle2D : MidiRenderer {
    struct CameraInfo {
        public Vector3 homePos;
        public Quaternion homeRot;
        public float fov;
    }

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

    GameObject NotePrefab;

    TrackInfo[] Tracks = new TrackInfo[0];
    CameraInfo CamInfo = new CameraInfo();

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

    float needsTrackUpdate = -1f;
    float needsNoteUpdate = -1f;
    float needsReloadVisuals = -1f;

    protected override void OnEnable() {
        CamInfo.homePos = new Vector3(0f, 0f, -8f);
        CamInfo.homeRot = Quaternion.identity;
        CamInfo.fov = 90f;
        base.OnEnable();
    }

    protected override void OnDisable() {
        base.OnDisable();
    }

    // Start is called before the first frame update
    protected override void Start() {
        base.Start();

        transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);

        MainCam.transform.localPosition = CamInfo.homePos;
        MainCam.transform.localRotation = CamInfo.homeRot;
        MainCam.fieldOfView = Camera.HorizontalToVerticalFieldOfView(CamInfo.fov, MainCam.aspect);

        NotePrefab = Resources.Load("Note2D") as GameObject;
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();

    }

    // Update is called once per frame
    protected override void Update() {
        if (needsReloadVisuals > 0 && Time.realtimeSinceStartup - needsReloadVisuals > 0.5f) { ReloadVisuals = true; needsReloadVisuals = -1f; }
        base.Update();
        UpdateInputs();
        if (needsTrackUpdate > 0 && Time.realtimeSinceStartup - needsTrackUpdate > 0.1f) { UpdateTracks(); needsTrackUpdate = -1f; }
        if (needsNoteUpdate > 0 && Time.realtimeSinceStartup - needsNoteUpdate > 0.5f) { UpdateNotes(false); needsNoteUpdate = -1f; }

        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo track = ref Tracks[j];
            Track midiTrack = Midi.Tracks[track.midiTrack];
            for (int i = 0; i < track.fadeTimes.Length; i++) {
                if (track.fadeTimes[i] == 0) continue;
                var mat = track.obj.transform.GetChild(i).GetComponent<MeshRenderer>().material;
                if (track.fadeTimes[i] > 0) {
                    mat.color = track.trackColor.WithAlpha(math.remap(0, NoteFadeTime, NoteAlpha, 1, track.fadeTimes[i]));
                    mat.SetColor("_EmissionColor", track.trackColor * math.min(4, 8 * math.unlerp(0, NoteFadeTime, track.fadeTimes[i])));
                    track.fadeTimes[i] -= Time.deltaTime;
                    if (track.fadeTimes[i] == 0) track.fadeTimes[i]--; // ensure that this doesn't land on exactly zero and enters the next block next frame
                } else if (track.fadeTimes[i] < 0) { // properly "shut off" the note only once (instead of every frame if this was <= 0)
                    mat.color = track.trackColor.WithAlpha(NoteAlpha);
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                    track.fadeTimes[i] = 0;
                }
            }
        }

        if (!IsPlaying) return;

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

    private void UpdateInputs() {
        float speed = 2.0f * Time.deltaTime;
        Vector3 ds = Vector3.zero;
        float rotSpeed = 60f * Time.deltaTime;
        (float x, float y) rot = (0, 0);

        if (Keyboard.current.aKey.isPressed) {
            ds.x -= speed;
        }
        if (Keyboard.current.dKey.isPressed) {
            ds.x += speed;
        }
        if (Keyboard.current.eKey.isPressed) {
            ds.y += speed;
        }
        if (Keyboard.current.qKey.isPressed) {
            ds.y -= speed;
        }
        if (Keyboard.current.wKey.isPressed) {
            ds.z += speed;
        }
        if (Keyboard.current.sKey.isPressed) {
            ds.z -= speed;
        }
        if (Keyboard.current.leftArrowKey.isPressed) {
            rot.x -= rotSpeed;
        }
        if (Keyboard.current.rightArrowKey.isPressed) {
            rot.x += rotSpeed;
        }
        if (Keyboard.current.upArrowKey.isPressed) {
            rot.y += rotSpeed;
        }
        if (Keyboard.current.downArrowKey.isPressed) {
            rot.y -= rotSpeed;
        }

        //ds.z += Mouse.current.scroll.y.value / 64f;

        MainCam.transform.Translate(ds, Space.Self);
        MainCam.transform.Rotate(Vector3.up, rot.x, Space.World);
        MainCam.transform.Rotate(Vector3.left, rot.y, Space.Self);
    }

    protected override void InitVisuals() {
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
            info.enabled = true;
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

        UpdateTracks();
        UpdateNotes();
    }

    private void UpdateTracks() {
        int i = 0;
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo info = ref Tracks[j];
            Transform tfm = info.obj.transform;
            info.obj.SetActive(info.enabled);
            if (!info.enabled) continue;
            tfm.localEulerAngles = new Vector3(0f, 0f, AngleOffset + info.angleOffset);

            if (SideCount != SideCountPrev) {
                info.noteSides = (uint)SideCount;
                info.updateMeshes = true;
            }

            i++;
        }
        SideCountPrev = (uint)SideCount;
    }

    private void UpdateNotes(bool justColors = false) {
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo trackInfo = ref Tracks[j];

            // will be null if the mesh doesn't need to be updated
            Mesh newMesh = trackInfo.updateMeshes? GetNSidedPlaneMesh(trackInfo.noteSides) : null;

            Track midiTrack = Midi.Tracks[trackInfo.midiTrack];
            float trackRadius = StartRadius + j * DeltaRadius;

            for (int i = 0; i < trackInfo.obj.transform.childCount; i++) {
                Transform obj = trackInfo.obj.transform.GetChild(i);

                obj.GetComponent<MeshRenderer>().material.color = trackInfo.trackColor.WithAlpha(NoteAlpha);
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

        MainCam.transform.localPosition = CamInfo.homePos;
        MainCam.transform.localRotation = CamInfo.homeRot;

        UpdateNotes(true);
    }

    protected override void MovePlay(double ticks) {}

    protected override void DrawGUI() {
        if (!ImGuiManager.IsEnabled) return;
        base.DrawGUI();

        if (ImGui.Begin(MainCam.name)) {
            ImGui.Text("Position");
            Vector3 camPos = MainCam.transform.localPosition;
            ImGui.InputFloat("X", ref camPos.x, 2f, 20f);
            ImGui.InputFloat("Y", ref camPos.y, 2f, 20f);
            ImGui.InputFloat("Z", ref camPos.z, 2f, 20f);
            MainCam.transform.localPosition = camPos;
            if (ImGui.Button("Set as default##pos")) CamInfo.homePos = camPos;

            ImGui.Text("Orientation");
            Vector3 camRot = MainCam.transform.localEulerAngles;
            string fmt = "%.1f";
            ImGui.SetNextItemWidth(48f);
            if (ImGui.InputFloat("##pitchin", ref camRot.x, 0, 0, fmt))
                camRot.x %= 360f;
            ImGui.SameLine();
            ImGui.SliderFloat("Pitch", ref camRot.x, -179f, 179f);
            ImGui.SetNextItemWidth(48f);
            if (ImGui.InputFloat("##yawin", ref camRot.y, 0, 0, fmt))
                camRot.y %= 360f;
            ImGui.SameLine();
            ImGui.SliderFloat("Yaw", ref camRot.y, -180f, 180f);
            ImGui.SetNextItemWidth(48f);
            if (ImGui.InputFloat("##rollin", ref camRot.z, 0, 0, fmt))
                camRot.z %= 360f;
            ImGui.SameLine();
            ImGui.SliderFloat("Roll", ref camRot.z, -180f, 180f);
            MainCam.transform.localEulerAngles = camRot;
            if (ImGui.Button("Set as default##rot")) CamInfo.homeRot = MainCam.transform.localRotation;
        }
        ImGui.End();

        if (ImGui.Begin("Keybinds")) {
            ImGui.Text("W/A/S/D/E/Q: Camera Position");
            ImGui.Text("Arrow Keys: Camera Rotation");
        }
        ImGui.End();

        bool updateTracks = false;
        bool updateNotes = false;
        bool updateVisuals = false;

        if (ImGui.Begin("MIDI Controls")) {
            ImGui.PushItemWidth(128f);
            if (ImGui.InputFloat("Track Start Radius", ref StartRadius, 1.0f, 5.0f)) updateNotes = AutoReload;
            if (ImGui.InputFloat("Track Radius Delta", ref DeltaRadius, 1.0f, 5.0f)) updateNotes = AutoReload;
            if (ImGui.SliderFloat("Angle Range", ref AngleRange, 0, 360f, "%.1f deg")) {
                for (int i = 0; i < Tracks.Length; i++) { ref TrackInfo track = ref Tracks[i]; track.angleRange = AngleRange; }
                updateNotes = AutoReload;
            }
            if (ImGui.SliderFloat("Angle Offset", ref AngleOffset, -180f, 180f, "%.1f deg")) updateTracks = AutoReload;
            ImGui.PopItemWidth();
            if (ImGui.Checkbox("Align Ends", ref AlignEnds)) updateNotes = AutoReload;

            if (ImGui.TreeNode("Tracks")) {
                float buttonDim = ImGui.GetFrameHeight();
                Vector2 buttonSize = new Vector2(buttonDim, buttonDim);
                for (int i = 0; i < Tracks.Length; i++) {
                    if (ImGui.Button(string.Format("^##trUp{0:d}", i), buttonSize) && i != 0) {
                        (Tracks[i-1], Tracks[i]) = (Tracks[i], Tracks[i-1]);
                        updateTracks = AutoReload;
                    }
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);
                    if (ImGui.Button(string.Format("v##trDown{0:d}", i), buttonSize) && i != Tracks.Length - 1) {
                        (Tracks[i], Tracks[i+1]) = (Tracks[i+1], Tracks[i]);
                        updateTracks = AutoReload;
                    }
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);
                    if (ImGui.Checkbox(string.Format("##chtr{0:d}", i), ref Tracks[i].enabled))
                        updateTracks = AutoReload;
                    ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.x);
                    if (ImGui.TreeNode(string.Format("{0:s}##tr{1:d}", Midi.Tracks[Tracks[i].midiTrack].name, i))) {
                        ref TrackInfo track = ref Tracks[i];
                        // --------------- individual track options -----------------------
                        ImGui.PushItemWidth(96.0f);
                        Vector4 color = track.trackColor;
                        if (ImGui.ColorEdit4("Track Color", ref color, ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoInputs)) {
                            track.trackColor = color;
                            updateNotes = AutoReload;
                        }
                        if (ImGui.SliderFloat("Angle Offset", ref track.angleOffset, -180f, 180f, "%.1f deg")) updateTracks = AutoReload;
                        if (ImGui.SliderFloat("Angle Range", ref track.angleRange, 0, 360f, "%.1f deg")) updateNotes = AutoReload;
                        int sides = (int)track.noteSides;
                        if (ImGui.SliderInt("Note Sides", ref sides, 3, 8)) {
                            track.noteSides = (uint)sides;
                            track.updateMeshes = true;
                            updateNotes = AutoReload;
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
                if (ImGui.SliderInt("Note Sides", ref SideCount, 3, 8)) (updateTracks, updateNotes) = (AutoReload, AutoReload);
                if (ImGui.SliderFloat("Note Rotation", ref NoteRotation, -180f, 180f, "%.1f deg")) updateNotes = AutoReload;
                if (ImGui.InputFloat("Note Size", ref NoteSize)) updateNotes = AutoReload;
                if (ImGui.SliderFloat("Note Alpha", ref NoteAlpha, 0f, 1f, "%.3f", 3)) updateNotes = AutoReload;
                ImGui.InputFloat("Fade Time", ref NoteFadeTime);
                NoteFadeTime = math.max(NoteFadeTime, 0.00048828125f); // things break if NoteFadeTime is 0. 1/2048 so the display rounds down to 0.000
                ImGui.PopItemWidth();
                ImGui.TreePop();
            }
        }
        ImGui.End();

        if (updateTracks) needsTrackUpdate = Time.unscaledTime;
        if (updateNotes) needsNoteUpdate = Time.unscaledTime;
        if (updateVisuals) needsReloadVisuals = Time.unscaledTime;
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
        Config.Set("c2d.cam.fov", CamInfo.fov);
        Config.Set("c2d.cam.homePos", CamInfo.homePos);
        Config.Set("c2d.cam.homeRot", CamInfo.homeRot);

        List<Color> trackColors = new List<Color>();
        trackColors.AddRange(TrackColors);
        for (int i = 0; i < Tracks.Length; i++) {
            if (i < trackColors.Count - 1) {
                trackColors[i] = Tracks[i].trackColor;
            } else {
                trackColors.Add(Tracks[i].trackColor);
            }
        }
        Config.Set("c2d.trackColors", trackColors.ToArray());
    }

    protected override void ReadConfig() {
        base.ReadConfig();

        static void TryGet<T>(string id, ref T val) where T : unmanaged {
            T? res = Config.Get<T>(id);
            if (res.HasValue) val = res.Value;
        }

        TryGet("c2d.startRadius", ref StartRadius);
        TryGet("c2d.deltaRadius", ref DeltaRadius);
        TryGet("c2d.angleOffset", ref AngleOffset);
        TryGet("c2d.angleRange", ref AngleRange);
        TryGet("c2d.alignEnds", ref AlignEnds);
        TryGet("c2d.velFactor", ref VelocityFactor);
        TryGet("c2d.noteSides", ref SideCount);
        TryGet("c2d.noteRotation", ref NoteRotation);
        TryGet("c2d.noteSize", ref NoteSize);
        TryGet("c2d.noteAlpha", ref NoteAlpha);
        TryGet("c2d.noteFadeTime", ref NoteFadeTime);
        TryGet("c2d.cam.fov", ref CamInfo.fov);
        TryGet("c2d.cam.homePos", ref CamInfo.homePos);
        TryGet("c2d.cam.homeRot", ref CamInfo.homeRot);

        List<Color> trackColors = new List<Color>();
        Config.Get("c2d.trackColors", trackColors);
        if (trackColors.Count > 0) TrackColors = trackColors;

        MainCam.transform.localPosition = CamInfo.homePos;
        MainCam.transform.localRotation = CamInfo.homeRot;
        MainCam.fieldOfView = Camera.HorizontalToVerticalFieldOfView(CamInfo.fov, MainCam.aspect);
    }
}
