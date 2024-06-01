using ImGuiNET;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Diagnostics;

/**
 * <summary>
 * Performs OMIDIV-specific ImGui operations, such as setup, base UI drawing, and events.
 * </summary>
 */
public class ImGuiManager : MonoBehaviour {
    /// <summary>
    /// Called when the UI is drawn. Modify the UI by subscribing to this event.
    /// </summary>
    /// <remarks><see cref="OmidivComponent.DrawGUI"/> is subscribed to this event.</remarks>
    public static event Action Draw;

    /// <summary>
    /// Called with the name of the tab for each main menu tab.<br/>
    /// </summary>
    /// <remarks>
    /// The current version of ImGui doesn't support calling BeginMainMenuBar multiple time to append, instead just overwriting.
    /// Additionally, it doesn't support appending with BeginMenu with the same name (at least not in the current version).
    /// </remarks>
    /// <example><code>
    /// void MyDrawMenuItems(string menuName) {
    ///     switch (menuName) {
    ///     case "File":
    ///         if (ImGui.MenuItem("Quit")) Application.Quit(); break;
    ///     ...
    ///     }
    /// }
    /// </code></example>
    /// <seealso cref="AddMainMenuTab(string)"/>
    public static event Action<string> DrawMainMenuItems;

    /// <summary>Whether the GUI is enabled.</summary>
    /// <remarks>Draw will not be called if not enabled.</remarks>
    public static bool IsEnabled { get; set; } = true;

    /// <summary>Whether debug stuff should be drawn.</summary>
    public static bool IsDebugEnabled { get; set; }

    private static HashSet<string> mainMenuTabs = new HashSet<string>();

    /// <summary>Adds a tab to the main menu bar if it doesn't already exist.</summary>
    /// <param name="name">The tab title.</param>
    /// <remarks>
    /// It is recommended to call this in <c>OnEnable</c>, and <see cref="RemoveMainMenuTab(string)"/> in <c>OnDisable</c>.<br/>
    /// </remarks>
    public static void AddMainMenuTab(string name) => mainMenuTabs.Add(name);
    /// <summary>Removes a tab from the main menu bar.</summary>
    /// <param name="name">The tab title to remove.</param>
    /// <remarks>Not removing a tab could result in the tab remaining in subsequent scenes.</remarks>
    public static void RemoveMainMenuTab(string name) => mainMenuTabs.Remove(name);

    /// <summary>
    /// A class for counting the number of times <see cref="Update"/> is called per second and averaging it.
    /// </summary>
    private class XPSCounter {
        private readonly float[] spxArr;
        private int index = 0;
        private Stopwatch clock;
        
        /// <summary>Average amount of seconds between <see cref="Update"/> calls.</summary>
        public float avgSpx { get { return spxArr.Sum() / spxArr.Length; } }

        /// <summary>Average <see cref="Update"/> calls per second.</summary>
        public float avgXps { get { return 1 / avgSpx; } }

        /// <param name="count">The number of values to average over.</param>
        public XPSCounter(int count) {
            spxArr = new float[count];
            clock = new Stopwatch();
        }

        /// <summary>Starts (or restarts) the clock.</summary>
        public void Start() => clock.Restart();

        /// <summary>Adds the time elapsed since <see cref="Start"/> or <see cref="Update"/> to the average.</summary>
        public void Update() {
            spxArr[index] = (float)((double)clock.ElapsedTicks / Stopwatch.Frequency); // lose less precision before the division
            clock.Restart();
            index = (index+1) % spxArr.Length;
        }
    }

    private XPSCounter tpsCounter = new XPSCounter(64);
    private XPSCounter upsCounter = new XPSCounter(64);
    private XPSCounter fpsCounter = new XPSCounter(64);
    private float avgTps, avgUps, avgFps; // display values, which update every 0.5 second instead of every frame.
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
        SetupStyle();
        DontDestroyOnLoad(gameObject);
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
            // update xps displays
            if (Time.realtimeSinceStartup - lastXPSRefresh > 0.5f) {
                avgTps = tpsCounter.avgXps;
                avgUps = upsCounter.avgXps;
                avgFps = fpsCounter.avgXps;
                lastXPSRefresh = Time.realtimeSinceStartup;
            }
            
            // draw xps displays
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

            // draw misc debug window
            nww = ImGui.GetFontSize() * 24;
            nwh = ImGui.GetFontSize() * 6;
            ImGui.SetNextWindowSize(new Vector2(nww, nwh));
            ImGui.SetNextWindowPos(Vector2.zero);
            if (ImGui.Begin("debug"/*, ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs*/)) {
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

        //ImGui.ShowDemoWindow();
    }

    /// <summary>Fires the Draw event if IsEnabled.</summary>
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



    private void SetupStyle() {
        ImGuiStylePtr style = ImGui.GetStyle();

        style.ChildRounding = 0;
        style.FrameRounding = 0;
        style.GrabRounding = 0;
        style.PopupRounding = 0;
        style.ScrollbarRounding = 0;
        style.TabRounding = 0;
        style.WindowRounding = 0;

        // colors
        {
            var colors = style.Colors;
            colors[(int)ImGuiCol.Text]                   = new(1.00f, 1.00f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.TextDisabled]           = new(0.50f, 0.50f, 0.50f, 1.00f);
            colors[(int)ImGuiCol.WindowBg]               = new(0.06f, 0.06f, 0.06f, 0.94f);
            colors[(int)ImGuiCol.ChildBg]                = new(0.00f, 0.00f, 0.00f, 0.00f);
            colors[(int)ImGuiCol.PopupBg]                = new(0.08f, 0.08f, 0.08f, 0.94f);
            colors[(int)ImGuiCol.Border]                 = new(0.43f, 0.43f, 0.50f, 0.50f);
            colors[(int)ImGuiCol.BorderShadow]           = new(0.00f, 0.00f, 0.00f, 0.00f);
            colors[(int)ImGuiCol.FrameBg]                = new(0.48f, 0.16f, 0.27f, 0.54f);
            colors[(int)ImGuiCol.FrameBgHovered]         = new(0.98f, 0.26f, 0.54f, 0.40f);
            colors[(int)ImGuiCol.FrameBgActive]          = new(0.98f, 0.26f, 0.54f, 0.67f);
            colors[(int)ImGuiCol.TitleBg]                = new(0.04f, 0.04f, 0.04f, 1.00f);
            colors[(int)ImGuiCol.TitleBgActive]          = new(0.48f, 0.16f, 0.27f, 1.00f);
            colors[(int)ImGuiCol.TitleBgCollapsed]       = new(0.00f, 0.00f, 0.00f, 0.51f);
            colors[(int)ImGuiCol.MenuBarBg]              = new(0.14f, 0.14f, 0.14f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarBg]            = new(0.02f, 0.02f, 0.02f, 0.53f);
            colors[(int)ImGuiCol.ScrollbarGrab]          = new(0.31f, 0.31f, 0.31f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabHovered]   = new(0.41f, 0.41f, 0.41f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabActive]    = new(0.51f, 0.51f, 0.51f, 1.00f);
            colors[(int)ImGuiCol.CheckMark]              = new(0.98f, 0.26f, 0.54f, 1.00f);
            colors[(int)ImGuiCol.SliderGrab]             = new(0.87f, 0.24f, 0.48f, 1.00f);
            colors[(int)ImGuiCol.SliderGrabActive]       = new(0.98f, 0.26f, 0.54f, 1.00f);
            colors[(int)ImGuiCol.Button]                 = new(0.98f, 0.26f, 0.54f, 0.40f);
            colors[(int)ImGuiCol.ButtonHovered]          = new(0.98f, 0.26f, 0.54f, 1.00f);
            colors[(int)ImGuiCol.ButtonActive]           = new(0.98f, 0.04f, 0.47f, 1.00f);
            colors[(int)ImGuiCol.Header]                 = new(0.98f, 0.26f, 0.54f, 0.31f);
            colors[(int)ImGuiCol.HeaderHovered]          = new(0.98f, 0.26f, 0.54f, 0.80f);
            colors[(int)ImGuiCol.HeaderActive]           = new(0.98f, 0.26f, 0.54f, 1.00f);
            colors[(int)ImGuiCol.Separator]              = new(0.43f, 0.43f, 0.50f, 0.50f);
            colors[(int)ImGuiCol.SeparatorHovered]       = new(0.75f, 0.09f, 0.36f, 0.78f);
            colors[(int)ImGuiCol.SeparatorActive]        = new(0.75f, 0.09f, 0.36f, 1.00f);
            colors[(int)ImGuiCol.ResizeGrip]             = new(0.98f, 0.26f, 0.54f, 0.25f);
            colors[(int)ImGuiCol.ResizeGripHovered]      = new(0.98f, 0.26f, 0.54f, 0.67f);
            colors[(int)ImGuiCol.ResizeGripActive]       = new(0.98f, 0.26f, 0.54f, 0.95f);
            colors[(int)ImGuiCol.Tab]                    = new(0.57f, 0.18f, 0.32f, 0.86f);
            colors[(int)ImGuiCol.TabHovered]             = new(0.98f, 0.26f, 0.54f, 0.80f);
            colors[(int)ImGuiCol.TabActive]              = new(0.67f, 0.20f, 0.38f, 1.00f);
            colors[(int)ImGuiCol.TabUnfocused]           = new(0.14f, 0.05f, 0.08f, 0.97f);
            colors[(int)ImGuiCol.TabUnfocusedActive]     = new(0.42f, 0.13f, 0.24f, 1.00f);
            colors[(int)ImGuiCol.PlotLines]              = new(0.61f, 0.61f, 0.61f, 1.00f);
            colors[(int)ImGuiCol.PlotLinesHovered]       = new(0.35f, 1.00f, 0.47f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogram]          = new(0.00f, 0.90f, 0.76f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogramHovered]   = new(0.00f, 1.00f, 0.67f, 1.00f);
            colors[(int)ImGuiCol.TextSelectedBg]         = new(0.98f, 0.26f, 0.54f, 0.35f);
            colors[(int)ImGuiCol.DragDropTarget]         = new(0.00f, 0.93f, 1.00f, 0.90f);
            colors[(int)ImGuiCol.NavHighlight]           = new(0.98f, 0.26f, 0.54f, 1.00f);
            colors[(int)ImGuiCol.NavWindowingHighlight]  = new(1.00f, 1.00f, 1.00f, 0.70f);
            colors[(int)ImGuiCol.NavWindowingDimBg]      = new(0.80f, 0.80f, 0.80f, 0.20f);
            colors[(int)ImGuiCol.ModalWindowDimBg]       = new(0.80f, 0.80f, 0.80f, 0.35f);
        }
    }
}
