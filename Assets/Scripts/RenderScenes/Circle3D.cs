using ImGuiNET;
using Unity.Mathematics;
using UnityEngine;
using static MidiManager;

/// <summary>
/// A visualization similar to <see cref="Standard3D"/>, except notes are placed radially instead of vertically.
/// </summary>
public class Circle3D : Base3D<Circle3D.TrackInfo> {

    public class TrackInfo : Base3DTrackInfo {
        public float angleOffset = 0;
    }

    public Circle3D() { ConfigTag = "c3d"; }

    float StartRadius = 300.0f;
    float DeltaRadius = -16f;
    float AngleOffset = 0f;

    protected override void ResetTracks() {
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo info = ref Tracks[j];

            Transform tfm = info.obj.transform;
            bool ignore = IgnoreTrack(info);
            info.obj.SetActive(!ignore);
            if (ignore) continue;

            tfm.localEulerAngles = new Vector3(AngleOffset + info.angleOffset, 0f, 0f);
            Vector3 scale = tfm.localScale;
            scale.x = info.lengthFactor;
            tfm.localScale = scale;

            if (SideCount != SideCountPrev) {
                info.noteSides = (uint)SideCount;
                info.updateMeshes = true;
            }
        }
        SideCountPrev = (uint)SideCount;
    }

    protected override void ResetNotes(bool justColors = false) {
        int j = 0;
        for (int rj = 0; rj < Tracks.Length; rj++) {
            ref TrackInfo trackInfo = ref Tracks[rj];
            if (IgnoreTrack(trackInfo)) continue;

            // will be null if the mesh doesn't need to be updated
            Mesh newMesh = trackInfo.updateMeshes? GeometryUtil.GetNSidedPrismMesh(trackInfo.noteSides) : null;

            Track midiTrack = Midi.Tracks[trackInfo.midiTrack];
            float trackRadius = StartRadius + j * DeltaRadius;

            for (int i = 0; i < trackInfo.obj.transform.childCount; i++) {
                var note = midiTrack.notes[i];
                Transform obj = trackInfo.obj.transform.GetChild(i);

                obj.GetComponent<MeshRenderer>().material.color = trackInfo.trackColor.MultiplyRGB(1f - VelocityFactor * (1f - note.velocity/127f));
                if (justColors) continue;

                if (newMesh is not null) obj.GetComponent<MeshFilter>().mesh = newMesh;

                float noteLength = note.lengthTicks;
                float noteTheta = math.remap(midiTrack.pitchRange.lower, midiTrack.pitchRange.upper + 1f, -Mathf.PI, Mathf.PI, note.pitch);
                float noteX = note.startTick + noteLength * 0.5f + NoteHSpacing * 0.5f;
                float noteY = trackRadius * Mathf.Sin(noteTheta);
                float noteZ = trackRadius * Mathf.Cos(noteTheta);
                obj.localPosition = new Vector3(noteX, noteY, noteZ);
                obj.localScale = new Vector3(noteLength - NoteHSpacing, NoteHeight, NoteHeight);
                obj.localEulerAngles = new Vector3(NoteRotation - Mathf.Rad2Deg * noteTheta, 0);
            }
            trackInfo.updateMeshes = false;

            j++;
        }
    }

    protected override void DrawGeneralMidiControls(ref bool updateTracks, ref bool updateNotes) {
        ImGui.PushItemWidth(128f);
        if (ImGui.InputFloat("Track Start Radius", ref StartRadius, 1.0f, 5.0f)) updateNotes = true;
        if (ImGui.InputFloat("Track Radius Delta", ref DeltaRadius, 1.0f, 5.0f)) updateNotes = true;
        if (ImGui.SliderFloat("Angle Offset", ref AngleOffset, -180f, 180f, "%.1f deg")) updateTracks = true;
        ImGui.PopItemWidth();
    }
    protected override void DrawIndividualTrackControls(ref TrackInfo trackInfo, ref bool updateTracks, ref bool updateNotes) {
        if (ImGui.SliderFloat("Angle Offset", ref trackInfo.angleOffset, -180f, 180f, "%.1f deg")) updateNotes = true;
    }
    protected override void TrackListChanged(ref bool updateTracks, ref bool updateNotes) {
        updateTracks = updateNotes = true;
    }

    protected override void WriteConfig() {
        base.WriteConfig();

        Config.Set("c3d.startRadius", StartRadius);
        Config.Set("c3d.deltaRadius", DeltaRadius);
        Config.Set("c3d.angleRadius", AngleOffset);
    }

    protected override void ReadConfig() {
        base.ReadConfig();

        Config.TryGet("c3d.startRadius", ref StartRadius);
        Config.TryGet("c3d.deltaRadius", ref DeltaRadius);
        Config.TryGet("c3d.angleOffset", ref AngleOffset);
    }
}
