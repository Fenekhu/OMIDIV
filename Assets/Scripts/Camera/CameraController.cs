using ImGuiNET;
using UnityEngine;

public abstract class CameraController : OmidivComponent {
    [SerializeField]
    protected string SceneTag = "";
    public Color BGColor { get { return GetBGColor(); } set { SetBGColor(value); } }

    protected abstract Color GetBGColor();
    protected abstract void SetBGColor(Color color);
}
