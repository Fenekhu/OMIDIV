using UnityEngine;

/// <summary>
/// <see cref="OmidivComponent"/> that provides a GUI and input controls for a camera.
/// </summary>
public abstract class CameraController : OmidivComponent {
    public Color BGColor { get { return GetBGColor(); } set { SetBGColor(value); } }

    protected abstract Color GetBGColor();
    protected abstract void SetBGColor(Color color);
}
