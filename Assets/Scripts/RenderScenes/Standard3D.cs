using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ImGuiNET;
using TrackInfo = Base3DTrackInfo;

public class Standard3D : Base3D<Standard3D.TrackInfo> {

    public class TrackInfo : Base3DTrackInfo {
        public int pitchOffset = 0;
    }

    float ZSpacing = 16f;
    float NoteVSpacing = 2f;

    protected override int GetSceneIndex() => 1;

    protected override string GetSceneName() => "Standard 3D";

    protected override void ResetTracks() {
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

    protected override void ResetNotes(bool justColors) {
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo trackInfo = ref Tracks[j];

            // will be null if the mesh doesn't need to be updated
            Mesh newMesh = trackInfo.updateMeshes? GeometryUtil.GetNSidedPrismMesh(trackInfo.noteSides) : null;

            for (int i = 0; i < trackInfo.obj.transform.childCount; i++) {
                var note = Midi.Tracks[trackInfo.midiTrack].notes[i];
                Transform obj = trackInfo.obj.transform.GetChild(i);

                SetNoteState(trackInfo, i, NoteState.Unplayed);
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

    protected override void DrawGUI() {
        base.DrawGUI();

        bool updateTracks = false;
        bool updateNotes = false;
        bool updateVisuals = false;

        if (ImGui.Begin("MIDI Controls")) {
            ImGui.SetNextItemWidth(128f);
            if (ImGui.InputFloat("Track depth spacing", ref ZSpacing, 0.5f, 5.0f)) updateTracks = true;

            if (ImGui.TreeNode("Tracks")) {
                for (int i = 0; i < Tracks.Length; i++) {
                    if (ImGui.TreeNode(string.Format("{0:s}##tr{1:d}", Midi.Tracks[Tracks[i].midiTrack].name, i))) {
                        ref TrackInfo track = ref Tracks[i];
                        // --------------- individual track options -----------------------
                        if (ImGui.InputInt("Pitch offset", ref track.pitchOffset)) updateTracks = true;
                        ImGui.TreePop();
                    }
                }
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Note Options")) {
                ImGui.SetNextItemWidth(128f);
                if (ImGui.InputFloat("Vertical Spacing", ref NoteVSpacing)) updateTracks = updateNotes = true;
            }
        }
        ImGui.End();

        if (AutoReload) {
            if (updateTracks) LastTrackUpdate = Time.unscaledTime;
            if (updateNotes) LastNoteUpdate = Time.unscaledTime;
            if (updateVisuals) LastReloadVisuals = Time.unscaledTime;
        }
    }

    protected override string GetConfigTag() => "s3d";

    protected override void WriteConfig() {
        base.WriteConfig();

        Config.Set("s3d.zSpacing", ZSpacing);
        Config.Set("s3d.noteVSpacing", NoteVSpacing);
    }

    protected override void ReadConfig() {
        base.ReadConfig();

        Config.TryGet("s3d.zSpacing", ref ZSpacing);
        Config.TryGet("s3d.noteVSpacing", ref NoteVSpacing);
    }
}
