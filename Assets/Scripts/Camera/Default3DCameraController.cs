using ImGuiNET;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// <see cref="CameraController"/> implementing 3-dimensional movement for a camera.
/// </summary>
public class Default3DCameraController : CameraController {

    private Vector3 homePos = new Vector3(0f, 0f, -8f);
    private Quaternion homeRot = Quaternion.identity;
    private float fov = 60f;

    /// <summary>The camera being controlled by this.</summary>
    public Camera MainCam;

    protected override Color GetBGColor() {
        return MainCam?.backgroundColor ?? Color.black;
    }

    protected override void SetBGColor(Color color) {
        if (MainCam != null) MainCam.backgroundColor = color;
    }

    private void Start() {
        if (MainCam) {
            MainCam.transform.localPosition = homePos;
            MainCam.transform.localRotation = homeRot;
            MainCam.fieldOfView = Camera.HorizontalToVerticalFieldOfView(fov, MainCam.aspect);
        }
    }

    protected override void Restart() {
        if (MainCam != null) {
            MainCam.transform.localPosition = homePos;
            MainCam.transform.localRotation = homeRot;
        }
    }

    protected override void DrawGUI() {
        if (MainCam == null) return;
        if (ImGui.Begin(MainCam.name)) {
            Vector4 bgColor = BGColor;
            if (ImGui.ColorEdit4("Background Color", ref bgColor, ImGuiColorEditFlags.NoAlpha))
                BGColor = bgColor;

            ImGui.Text("Position");
            Vector3 camPos = MainCam.transform.localPosition;
            ImGui.InputFloat("X", ref camPos.x, 2f, 20f);
            ImGui.InputFloat("Y", ref camPos.y, 2f, 20f);
            ImGui.InputFloat("Z", ref camPos.z, 2f, 20f);
            MainCam.transform.localPosition = camPos;
            if (ImGui.Button("Set as default##pos")) homePos = camPos;

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
            if (ImGui.Button("Set as default##rot")) homeRot = MainCam.transform.localRotation;
        }
        ImGui.End();

        if (ImGui.Begin("Keybinds")) {
            ImGui.Text("W/A/S/D/E/Q: Camera Position");
            ImGui.Text("Arrow Keys: Camera Rotation");
        }
        ImGui.End();
    }

    protected override void ReadConfig() {
        BGColor = Config.Get<Color>("bg.color") ?? Color.black;
        string tag = ConfigTag;
        Config.TryGet(tag+".cam.fov", ref fov);
        Config.TryGet(tag+".cam.homePos", ref homePos);
        Config.TryGet(tag+".cam.homeRot", ref homeRot);

        if (MainCam != null) {
            MainCam.transform.localPosition = homePos;
            MainCam.transform.localRotation = homeRot;
            MainCam.fieldOfView = Camera.HorizontalToVerticalFieldOfView(fov, MainCam.aspect);
        }
    }

    protected override void WriteConfig() {
        Config.Set("bg.color", BGColor);
        string tag = ConfigTag;
        Config.Set(tag+".cam.fov", fov);
        Config.Set(tag+".cam.homePos", homePos);
        Config.Set(tag+".cam.homeRot", homeRot);
    }

    private void Update() {
        UpdateInputs();
    }

    private void UpdateInputs() {
        float speed = (float)(2 * FrameDeltaTime);
        Vector3 ds = Vector3.zero;
        float rotSpeed = (float)(60 * FrameDeltaTime);
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
}