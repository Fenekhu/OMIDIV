using ImGuiNET;
using UnityEngine;

public class DirectionalLightController : OmidivComponent {
    [SerializeField] private GameObject Light;

    protected override void DrawGUI() {
        if (ImGui.Begin("Misc Controls")) {
            if (ImGui.TreeNode("Lighting")) {
                ImGui.Text("Direction");
                Vector3 lightDir = Light.transform.localEulerAngles;
                string fmt = "%.1f";
                ImGui.SetNextItemWidth(48f);
                if (ImGui.InputFloat("##xin", ref lightDir.x, 0, 0, fmt))
                    lightDir.x %= 360f;
                ImGui.SameLine();
                ImGui.SliderFloat("X", ref lightDir.x, 0f, 360f);
                ImGui.SetNextItemWidth(48f);
                if (ImGui.InputFloat("##yin", ref lightDir.y, 0, 0, fmt))
                    lightDir.y %= 360f;
                ImGui.SameLine();
                ImGui.SliderFloat("Y", ref lightDir.y, 0f, 360f);
                ImGui.SetNextItemWidth(48f);
                if (ImGui.InputFloat("##zin", ref lightDir.z, 0, 0, fmt))
                    lightDir.z %= 360f;
                ImGui.SameLine();
                ImGui.SliderFloat("Z", ref lightDir.z, 0f, 360f);
                Light.transform.localEulerAngles = lightDir;
                ImGui.TreePop();
            }
        }
        ImGui.End();
    }
    protected override void ReadConfig() {

    }

    protected override void WriteConfig() {

    }
}