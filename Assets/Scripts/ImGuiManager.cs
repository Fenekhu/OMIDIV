using ImGuiNET;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ImGuiManager : MonoBehaviour {
    private static bool drawDebug = false;

    public static bool IsEnabled = true;
    public static bool IsDebugEnabled {  get { return drawDebug && IsEnabled; } set { drawDebug = value; } }

    private static ImGuiManager instance;

    private float[] tpsArr = new float[64];
    private float[] upsArr = new float[64];
    private float[] fpsArr = new float[64];
    private int tpsI = 0;
    private int upsI = 0;
    private int fpsI = 0;
    private float avgTps = 0;
    private float avgUps = 0;
    private float avgFps = 0;
    private float lastTime = -1.0f;
    private float lastXPSUpdate = 0.0f;

    private void OnEnable() {
        //ImGuiUn.Layout += DrawGUI;
        instance = this;
    }

    private void OnDisable() {
        //ImGuiUn.Layout -= DrawGUI;
        instance = null;
    }

    private void Update() {
        upsArr[upsI] = Time.deltaTime;
        upsI = (upsI + 1) % fpsArr.Length;

        if (Keyboard.current.f1Key.wasPressedThisFrame) {
            IsEnabled = !IsEnabled;
        }

        if (Keyboard.current.f3Key.wasPressedThisFrame) {
            IsDebugEnabled = !IsDebugEnabled;
        }
    }

    private void FixedUpdate() {
        if (lastTime != -1.0f) {
            tpsArr[tpsI] = Time.realtimeSinceStartup - lastTime;
            tpsI = (tpsI + 1) % tpsArr.Length;
        }
        lastTime = Time.realtimeSinceStartup;
    }

    private void DrawGUI_() {
        if (!IsEnabled) return;

        fpsArr[fpsI] = Time.deltaTime;
        fpsI = (fpsI + 1) % fpsArr.Length;

        if (IsDebugEnabled) {
            if (Time.realtimeSinceStartup - lastXPSUpdate > 0.5f) {
                avgTps = tpsArr.Sum() / tpsArr.Length;
                avgUps = upsArr.Sum() / upsArr.Length;
                avgFps = fpsArr.Sum() / fpsArr.Length;
                lastXPSUpdate = Time.realtimeSinceStartup;
            }

            float nww = ImGui.GetFontSize() * 18;
            float nwh = ImGui.GetFontSize() * 6;
            ImGui.SetNextWindowSize(new Vector2(nww, nwh));
            ImGui.SetNextWindowPos(new Vector2(Screen.width - nww, Screen.height - nwh));
            if (ImGui.Begin("fps", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs)) {
                string tpsStr = string.Format("{0:F2} tps ({1:F2})", 1/avgTps, 1/Time.fixedDeltaTime);
                ImGui.Text(tpsStr);
                ImGui.Text(string.Format("{0:F2} ups", 1/avgUps).PadLeft(tpsStr.Length));
                ImGui.Text(string.Format("{0:F2} fps", 1/avgFps).PadLeft(tpsStr.Length));
            }
            ImGui.End();

            nww = ImGui.GetFontSize() * 24;
            nwh = ImGui.GetFontSize() * 6;
            ImGui.SetNextWindowSize(new Vector2(nww, nwh));
            ImGui.SetNextWindowPos(Vector2.zero);
            if (ImGui.Begin("debug", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs)) {
                // currently nothing here
                ImGui.Text(" ");
            }
            ImGui.End();
        }
    }

    public static void DrawGUI() {
        instance.DrawGUI_();
    }

    public static bool RawImageControlInner(RawImage img) {
        const float oneOn120 = 1f / 120f;
        const float oneOn12 = 1f / 12f;

        if (img is null) return false;

        (float x, float y, float z) pos = (img.transform.localPosition.x, img.transform.localPosition.y, img.transform.localPosition.z);
        var eulerAngles = img.transform.localEulerAngles;
        float pitch = eulerAngles.x, yaw = eulerAngles.y, roll = eulerAngles.z;
        (float w, float h) scale = (img.transform.localScale.x, img.transform.localScale.y);
        bool enabled = img.enabled;

        bool updatePos = false;
        bool updateRot = false;
        bool updateScl = false;

        ImGui.Text("Position");
        updatePos |= ImGui.InputFloat("X", ref pos.x, oneOn120, oneOn12);
        updatePos |= ImGui.InputFloat("Y", ref pos.y, oneOn120, oneOn12);
        updatePos |= ImGui.InputFloat("Z", ref pos.z, oneOn120, oneOn12);
        ImGui.Text("Orientation");
        updateRot |= ImGui.SliderFloat("Pitch", ref pitch, -180f, 180f, "%.0f deg");
        updateRot |= ImGui.SliderFloat("Yaw", ref yaw, -180f, 180f, "%.0f deg");
        updateRot |= ImGui.SliderFloat("Roll", ref roll, -180f, 180f, "%.0f deg");
        ImGui.Text("Scale");
        updateScl |= ImGui.InputFloat("Width", ref scale.w, oneOn120, oneOn12);
        updateScl |= ImGui.InputFloat("Height", ref scale.h, oneOn120, oneOn12);
        bool updateEnabled = ImGui.Checkbox("Enabled", ref enabled);

        if (updatePos) img.transform.localPosition = new Vector3(pos.x, pos.y, pos.z);
        if (updateRot) img.transform.localEulerAngles = new Vector3(pitch, yaw, roll);
        if (updateScl) img.transform.localScale = new Vector3(scale.w, scale.h, 1);
        if (updateEnabled) img.enabled = enabled;

        return updatePos || updateRot || updateScl || updateEnabled;
    }
}
