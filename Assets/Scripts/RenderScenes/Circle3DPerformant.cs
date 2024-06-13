using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A version of <see cref="Circle3D"/> that has certain features disabled to increase playback performance.<br/>
/// Mainly useful for visualizing really large midis.
/// </summary>
/// <remarks>
/// Currently the only disabled feature is individual length scales for tracks.<br/>
/// This visualization improves performance by moving the camera instead of the tracks,
/// which requires every track to have the same length scale.
/// </remarks>
public class Circle3DPerformant : Circle3D {

    public Circle3DPerformant() { ConfigTag = "c3d_p"; }

    [SerializeField] private Camera Camera;

    protected override void ResetTracks() {
        for (int j = 0; j < Tracks.Length; j++) {
            ref TrackInfo info = ref Tracks[j];

            Transform tfm = info.obj.transform;
            bool ignore = IgnoreTrack(info);
            info.obj.SetActive(!ignore);
            if (ignore) continue;

            tfm.localEulerAngles = new Vector3(AngleOffset + info.angleOffset, 0f, 0f);
            /*Vector3 scale = tfm.localScale;
            scale.x = info.lengthFactor;
            tfm.localScale = scale;*/

            if (SideCount != SideCountPrev) {
                info.noteSides = (uint)SideCount;
                info.updateMeshes = true;
            }
        }
        SideCountPrev = (uint)SideCount;
    }


    protected override void MovePlay(decimal ticks, decimal microseconds) {
        Camera.transform.Translate((float)ticks * transform.localScale.x, 0, 0, Space.World);
    }
}
