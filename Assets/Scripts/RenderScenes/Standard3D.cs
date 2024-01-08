using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ImGuiNET;

public class Standard3D : MidiRenderer {
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
        public int pitchOffset;
        public float lengthFactor;
        public List<int> nowPlaying;
        public int playIndex;
        public uint noteSides;
        public bool updateMeshes;
    }

    static readonly float GlobalScale = 1/128.0f;

    GameObject NotePrefab;

    TrackInfo[] Tracks = new TrackInfo[0];
    CameraInfo CamInfo = new CameraInfo();

    float ZSpacing = 16f;
    float VelocityFactor = 0.75f;
    int SideCount = 4;
    uint SideCountPrev = 0;
    float NoteRotation = 0f;
    float NoteHeight = 10f;
    float NoteVSpacing = 2f;
    float NoteHSpacing = 2f;
    float PlayedAlpha = 0.05f;

    float needsTrackUpdate = -1f;
    float needsNoteUpdate = -1f;
    float needsReloadVisuals = -1f;

    protected override void OnEnable() {
        CamInfo.homePos = new Vector3(0f, 0f, -8f);
        CamInfo.homeRot = Quaternion.identity;
        CamInfo.fov = 60f;
        base.OnEnable();
    }

    // Start is called before the first frame update
    protected override void Start() {
        base.Start();

        transform.localScale = new Vector3(GlobalScale, GlobalScale, GlobalScale);

        MainCam.transform.localPosition = CamInfo.homePos;
        MainCam.transform.localRotation = CamInfo.homeRot;
        MainCam.fieldOfView = Camera.HorizontalToVerticalFieldOfView(CamInfo.fov, MainCam.aspect);

        NotePrefab = Resources.Load("Note3D") as GameObject;
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

        if (!IsPlaying) return;

        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo track = ref Tracks[j];
            Track midiTrack = Midi.Tracks[track.midiTrack];
            for (int i = 0; i < track.nowPlaying.Count; i++) {
                int noteI = track.nowPlaying[i];
                if ((long)midiTrack.notes[noteI].endTick < CurrentTick) {
                    var mat = track.obj.transform.GetChild(noteI).GetComponent<MeshRenderer>().material;
                    mat.color = track.trackColor.WithAlpha(PlayedAlpha);
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                    track.nowPlaying.RemoveAt(i--);
                }
            }
            while (true) {
                if (track.playIndex >= midiTrack.notes.Count) break;
                MidiNote note = midiTrack.notes[track.playIndex];
                if ((long)note.startTick < CurrentTick) {
                    track.nowPlaying.Add(track.playIndex);
                    var mat = track.obj.transform.GetChild(track.playIndex).GetComponent<MeshRenderer>().material;
                    mat.color = track.trackColor.LerpWith(Color.white, 0.5f, false);
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
            foreach (MidiNote note in track.notes) {
                GameObject noteGO = Instantiate(NotePrefab);
                noteGO.transform.SetParent(go.transform, false);
            }

            TrackInfo info = new TrackInfo();
            info.obj = go;
            info.trackColor = TrackColors[i % TrackColors.Count];
            info.enabled = true;
            info.midiTrack = i;
            info.pitchOffset = 0;
            info.lengthFactor = 1;
            info.nowPlaying = new List<int>();
            info.noteSides = (uint)SideCount;
            info.updateMeshes = true;
            Tracks[i] = info;
        }

        UpdateTracks();
        UpdateNotes();
    }

    private void UpdateTracks() {
        int i = 0;
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo info = ref Tracks[j];
            info.obj.SetActive(info.enabled);
            if (!info.enabled) continue;
            float posX = info.obj.transform.localPosition.x;
            info.obj.transform.localPosition = new Vector3(posX, info.pitchOffset * (NoteHeight + NoteVSpacing), ZSpacing * i);
            Vector3 scale = info.obj.transform.localScale;
            scale.x = info.lengthFactor;
            info.obj.transform.localScale = scale;

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
            Mesh newMesh = trackInfo.updateMeshes? GetNSidedPrismMesh(trackInfo.noteSides) : null;

            for (int i = 0; i < trackInfo.obj.transform.childCount; i++) {
                var note = Midi.Tracks[trackInfo.midiTrack].notes[i];
                Transform obj = trackInfo.obj.transform.GetChild(i);

                obj.GetComponent<MeshRenderer>().material.color = trackInfo.trackColor.MultiplyRGB(1f - VelocityFactor * (1f - note.velocity/127f));
                if (justColors) continue;

                if (newMesh is not null) obj.GetComponent<MeshFilter>().mesh = newMesh;

                float noteLength = note.lengthTicks;
                float noteX = note.startTick + noteLength * 0.5f + NoteHSpacing * 0.5f;
                float noteY = (note.pitch - Midi.NoteRange/2) * (NoteHeight + NoteVSpacing);
                obj.localPosition = new Vector3(noteX, noteY);
                obj.localScale = new Vector3(noteLength - NoteHSpacing, NoteHeight, NoteHeight);
                obj.localEulerAngles = new Vector3(NoteRotation, 0);
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

    protected override void MovePlay(double ticks) {
        //MainCam.transform.Translate((float)ticks, 0, 0, Space.World);
        foreach (TrackInfo info in Tracks) {
            Vector3 ds = new Vector3((float)ticks * info.lengthFactor, 0, 0);
            info.obj.transform.localPosition -= ds;
        }
    }

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
            if (ImGui.InputFloat("Track depth spacing", ref ZSpacing, 0.5f, 5.0f)) updateTracks = AutoReload;
            ImGui.SliderFloat("Played Note Alpha", ref PlayedAlpha, 0f, 1f);
            ImGui.PopItemWidth();

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
                        Vector4 color = track.trackColor;
                        if (ImGui.ColorEdit4("Track color", ref color, ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoInputs)) {
                            track.trackColor = color;
                            updateNotes = AutoReload;
                        }
                        if (ImGui.InputInt("Pitch offset", ref track.pitchOffset)) updateTracks = AutoReload;
                        if (ImGui.InputFloat("Length Factor", ref track.lengthFactor)) updateTracks = AutoReload;
                        int sides = (int)track.noteSides;
                        if (ImGui.SliderInt("Note Sides", ref sides, 3, 8)) { 
                            track.noteSides = (uint)sides; 
                            track.updateMeshes = true; 
                            updateNotes = AutoReload; 
                        }
                        ImGui.TreePop();
                    }
                }
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Note Options")) {
                ImGui.PushItemWidth(128f);
                if (ImGui.SliderFloat("Velocity Intensity", ref VelocityFactor, 0f, 1f)) updateNotes = AutoReload;
                if (ImGui.SliderInt("Note Sides", ref SideCount, 3, 8)) (updateTracks, updateNotes) = (AutoReload, AutoReload);
                if (ImGui.SliderFloat("Note Rotation", ref NoteRotation, -180f, 180f, "%.1f deg")) updateNotes = AutoReload;
                if (ImGui.InputFloat("Height", ref NoteHeight)) (updateTracks, updateNotes) = (AutoReload, AutoReload);
                if (ImGui.InputFloat("Vertical Spacing", ref NoteVSpacing)) (updateTracks, updateNotes) = (AutoReload, AutoReload);
                Vector3 newScale = transform.localScale / GlobalScale;
                if (ImGui.InputFloat("Length Factor", ref newScale.x)) transform.localScale = newScale * GlobalScale;
                if (ImGui.InputFloat("Horizontal Spacing", ref NoteHSpacing)) updateNotes = AutoReload;
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

        Config.Set("s3d.zSpacing", ZSpacing);
        Config.Set("s3d.velFactor", VelocityFactor);
        Config.Set("s3d.noteSides", SideCount);
        Config.Set("s3d.noteRotation", NoteRotation);
        Config.Set("s3d.noteHeight", NoteHeight);
        Config.Set("s3d.noteVSpacing", NoteVSpacing);
        Config.Set("s3d.noteHSpacing", NoteHSpacing);
        Config.Set("s3d.lengthScale", transform.localScale.x / GlobalScale);
        Config.Set("s3d.playedAlpha", PlayedAlpha);
        Config.Set("s3d.cam.fov", CamInfo.fov);
        Config.Set("s3d.cam.homePos", CamInfo.homePos);
        Config.Set("s3d.cam.homeRot", CamInfo.homeRot);
        
        List<Color> trackColors = new List<Color>();
        trackColors.AddRange(TrackColors);
        for (int i = 0; i < Tracks.Length; i++) {
            if (i < trackColors.Count - 1) {
                trackColors[i] = Tracks[i].trackColor;
            } else {
                trackColors.Add(Tracks[i].trackColor);
            }
        }
        Config.Set("s3d.trackColors", trackColors.ToArray());
    }

    protected override void ReadConfig() {
        base.ReadConfig();

        static void TryGet<T> (string id, ref T val) where T : unmanaged {
            T? res = Config.Get<T>(id);
            if (res.HasValue) val = res.Value;
        }

        TryGet("s3d.zSpacing", ref ZSpacing);
        TryGet("s3d.velFactor", ref VelocityFactor);
        TryGet("s3d.noteSides", ref SideCount);
        TryGet("s3d.noteRotation", ref NoteRotation);
        TryGet("s3d.noteHeight", ref NoteHeight);
        TryGet("s3d.noteVSpacing", ref NoteVSpacing);
        TryGet("s3d.noteHSpacing", ref NoteHSpacing);
        Vector3 scale = transform.localScale / GlobalScale;
        TryGet("s3d.lengthScale", ref scale.x); transform.localScale = scale * GlobalScale;
        TryGet("s3d.playedAlpha", ref PlayedAlpha);
        TryGet("s3d.cam.fov", ref CamInfo.fov);
        TryGet("s3d.cam.homePos", ref CamInfo.homePos);
        TryGet("s3d.cam.homeRot", ref CamInfo.homeRot);

        List<Color> trackColors = new List<Color>();
        Config.Get("s3d.trackColors", trackColors);
        if (trackColors.Count > 0) TrackColors = trackColors;

        MainCam.transform.localPosition = CamInfo.homePos;
        MainCam.transform.localRotation = CamInfo.homeRot;
        MainCam.fieldOfView = Camera.HorizontalToVerticalFieldOfView(CamInfo.fov, MainCam.aspect);
    }
}
