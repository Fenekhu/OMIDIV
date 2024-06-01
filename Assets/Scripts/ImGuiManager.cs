using ImGuiNET;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Diagnostics;

public class ImGuiManager : MonoBehaviour {
    public static event Action Draw;
    public static event Action<string> DrawMainMenuItems;

    public static bool IsEnabled { get; set; } = true;
    public static bool IsDebugEnabled { get; set; }
    private static HashSet<string> mainMenuTabs = new HashSet<string>();
    public static void AddMainMenuTab(string name) => mainMenuTabs.Add(name);
    public static void RemoveMainMenuTab(string name) => mainMenuTabs.Remove(name);
    private class XPSCounter {
        private readonly float[] spxArr;
        private int index = 0;
        private Stopwatch clock;
        public float avgSpx { get { return spxArr.Sum() / spxArr.Length; } }
        public float avgXps { get { return 1 / avgSpx; } }

        public XPSCounter(int count) {
            spxArr = new float[count];
            clock = new Stopwatch();
        }

        public void Start() => clock.Restart();

        public void Update() {
            spxArr[index] = (float)((double)clock.ElapsedTicks / Stopwatch.Frequency); // lose less precision before the division
            clock.Restart();
            index = (index+1) % spxArr.Length;
        }
    }

    XPSCounter tpsCounter = new XPSCounter(64);
    XPSCounter upsCounter = new XPSCounter(64);
    XPSCounter fpsCounter = new XPSCounter(64);
    private float avgTps, avgUps, avgFps;
    private float lastXPSRefresh = 0.0f;

    private void OnEnable() {
        ImGuiUn.Layout += DrawGUI_;
        Draw += DrawGUI;
        DrawMainMenuItems += DrawMainMenuItems_;
        AddMainMenuTab("File");
        AddMainMenuTab("View");
    }

    private void OnDisable() {
        ImGuiUn.Layout -= DrawGUI_;
        Draw -= DrawGUI;
        DrawMainMenuItems -= DrawMainMenuItems_;
        RemoveMainMenuTab("File");
        RemoveMainMenuTab("View");
    }

    private void Start() {
        tpsCounter.Start();
        upsCounter.Start();
        fpsCounter.Start();
    }

    private void Update() {
        upsCounter.Update();

        if (Keyboard.current.f1Key.wasPressedThisFrame) {
            IsEnabled = !IsEnabled;
        }

        if (Keyboard.current.f3Key.wasPressedThisFrame) {
            IsDebugEnabled = !IsDebugEnabled;
        }
    }

    private void FixedUpdate() {
        tpsCounter.Update();
    }

    private void DrawGUI() {
        fpsCounter.Update();

        if (IsDebugEnabled) {
            if (Time.realtimeSinceStartup - lastXPSRefresh > 0.5f) {
                avgTps = tpsCounter.avgXps;
                avgUps = upsCounter.avgXps;
                avgFps = fpsCounter.avgXps;
                lastXPSRefresh = Time.realtimeSinceStartup;
            }

            float nww = ImGui.GetFontSize() * 18;
            float nwh = ImGui.GetFontSize() * 6;
            ImGui.SetNextWindowSize(new Vector2(nww, nwh));
            ImGui.SetNextWindowPos(new Vector2(Screen.width - nww, Screen.height - nwh));
            if (ImGui.Begin("fps", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs)) {
                string tpsStr = string.Format("{0:F2} tps ({1:F2})", avgTps, 1/Time.fixedDeltaTime);
                ImGui.Text(tpsStr);
                ImGui.Text(string.Format("{0:F2} ups", avgUps).PadLeft(tpsStr.Length));
                ImGui.Text(string.Format("{0:F2} fps", avgFps).PadLeft(tpsStr.Length));
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

        if (ImGui.BeginMainMenuBar()) {
            foreach (string name in mainMenuTabs) {
                if (ImGui.BeginMenu(name)) {
                    DrawMainMenuItems?.Invoke(name);
                    ImGui.EndMenu();
                }
            }
            ImGui.EndMainMenuBar();
        }
    }

    private void DrawGUI_() {
        if (IsEnabled) Draw?.Invoke();
    }

    private void DrawMainMenuItems_(string name) {
        switch (name) {
        case "File":
            if (ImGui.MenuItem("Quit")) Application.Quit();
            ImGui.Separator();
            break;
        case "View":
            foreach (var kvp in SceneRegistry.Scenes) {
                if (ImGui.MenuItem(kvp.Value, kvp.Key != SceneManager.GetActiveScene().buildIndex))
                    StartCoroutine(CoroutineUtils.LoadScene(kvp.Key));
            }
            break;
        }
    }

    /*public static bool RawImageControlInner(RawImage img) {
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
    }*/
    }
}
