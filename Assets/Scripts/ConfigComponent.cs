using ImGuiNET;

/// <summary>
/// Adds the Open Config, Save Config, and Save Config As buttons to the File menu.
/// </summary>
public class ConfigComponent : OmidivComponent {

    // The button presses are saved to booleans, because if the functions in Update were called in DrawMainMenuItems,
    // Then Config would fire the ReadConfig and WriteConfig events in the drawing phase (maybe the render thread?), which feels weird.

    private bool OpenConfig = false;
    private bool SaveConfig = false;
    private bool SaveConfigAs = false;

    protected override void OnEnable() {
        base.OnEnable();
        ImGuiManager.DrawMainMenuItems += DrawMainMenuItems;
    }

    protected override void OnDisable() {
        base.OnDisable();
        ImGuiManager.DrawMainMenuItems -= DrawMainMenuItems;
    }

    private void Update() {
        if (OpenConfig) {Config.Open(); OpenConfig = false;}
        if (SaveConfig) {Config.Save(); SaveConfig = false;}
        if (SaveConfigAs) {Config.SaveAs(); SaveConfigAs = false;}
    }


    protected void DrawMainMenuItems(string menuName) {
        if (menuName == "File") {
            OpenConfig = ImGui.MenuItem("Open Config");
            SaveConfig = ImGui.MenuItem("Save Config");
            SaveConfigAs = ImGui.MenuItem("Save Config As");
            ImGui.Separator();
        }
    }
}