using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A version of <see cref="Standard3D"/> that has certain features disabled to increase playback performance.<br/>
/// Mainly useful for visualizing really large midis.
/// </summary>
/// <remarks>
/// Currently the only disabled feature is individual length scales for tracks.<br/>
/// This visualization improves performance by moving the camera instead of the tracks,
/// which requires every track to have the same length scale.
/// </remarks>
public class Standard3DPerformant : Standard3D {

    public Standard3DPerformant() { ConfigTag = "s3d_p"; }

    [SerializeField] private Camera Camera;

    protected override void ResetTracks() {
        int i = 0;
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo info = ref Tracks[j];

            bool ignore = IgnoreTrack(info);
            info.obj.SetActive(!ignore);
            if (ignore) continue;

            float posX = info.obj.transform.localPosition.x;
            info.obj.transform.localPosition = new Vector3(posX, info.pitchOffset * (NoteHeight + NoteVSpacing), ZSpacing * i);
            /*Vector3 scale = info.obj.transform.localScale;
            scale.x = info.lengthFactor;
            info.obj.transform.localScale = scale;*/

            if (SideCount != SideCountPrev) {
                info.noteSides = (uint)SideCount;
                info.updateMeshes = true;
            }

            i++;
        }
        SideCountPrev = (uint)SideCount;
    }


    protected override void MovePlay(decimal ticks, decimal microseconds) {
        Camera.transform.Translate((float)ticks * transform.localScale.x, 0, 0, Space.World);
    }
}
