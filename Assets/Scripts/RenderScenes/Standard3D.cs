using UnityEngine;
using ImGuiNET;
using static MidiManager;

/// <summary>
/// The standard 3D note visualization.
/// </summary>
public class Standard3D : Base3D<Standard3D.TrackInfo> {

    public Standard3D() { ConfigTag = "s3d"; }

    public class TrackInfo : Base3DTrackInfo {
        public int pitchOffset = 0;
    }

    protected float ZSpacing = 16f;
    protected float NoteVSpacing = 2f;


    protected override void ResetTracks() {
        int i = 0;
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo info = ref Tracks[j];

            bool ignore = IgnoreTrack(info);
            info.obj.SetActive(!ignore);
            if (ignore) continue;

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
            if (IgnoreTrack(trackInfo)) continue;

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

    protected override void DrawGeneralMidiControls(ref bool updateTracks, ref bool updateNotes) {
        ImGui.SetNextItemWidth(128f);
        if (ImGui.InputFloat("Track depth spacing", ref ZSpacing, 0.5f, 5.0f)) updateTracks = true;
    }
    protected override void DrawIndividualTrackControls(ref TrackInfo trackInfo, ref bool updateTracks, ref bool updateNotes) {
        if (ImGui.InputInt("Pitch offset", ref trackInfo.pitchOffset, 1, 12)) updateTracks = true;
    }
    protected override void DrawNoteControls(ref bool updateTracks, ref bool updateNotes) {
        ImGui.SetNextItemWidth(128f);
        if (ImGui.InputFloat("Vertical Spacing", ref NoteVSpacing)) updateTracks = updateNotes = true;
    }
    protected override void TrackListChanged(ref bool updateTracks, ref bool updateNotes) {
        updateTracks = true;
    }

    protected override void WriteConfig() {
        base.WriteConfig();

        Config.Set(ConfigTag+".zSpacing", ZSpacing);
        Config.Set(ConfigTag+".noteVSpacing", NoteVSpacing);
    }

    protected override void ReadConfig() {
        base.ReadConfig();

        Config.TryGet(ConfigTag+".zSpacing", ref ZSpacing);
        Config.TryGet(ConfigTag+".noteVSpacing", ref NoteVSpacing);
    }
}
