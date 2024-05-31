using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using ImGuiNET;

public class PostProcessingController : OmidivComponent {
    [SerializeField] protected Volume GlobalVolume;
    protected Bloom Bloom;

    private void Start() {
        if (GlobalVolume != null) {
            if (GlobalVolume.profile.TryGet(out Bloom bloom)) {
                Bloom = bloom;
            }
        }
    }

    protected override void DrawGUI() {
        if (ImGui.Begin("Misc Controls")) {
            if (Bloom) {
                bool _bloom = Bloom.active;
                if (ImGui.Checkbox("Bloom", ref _bloom))
                    Bloom.active = _bloom;
                if (Bloom.active && ImGui.TreeNode("Bloom settings")) {
                    float _threshold = Bloom.threshold.value;
                    if (ImGui.InputFloat("Threshold", ref _threshold))
                        Bloom.threshold.value = _threshold;
                    float _intensity = Bloom.intensity.value;
                    if (ImGui.InputFloat("Intensity", ref _intensity))
                        Bloom.intensity.value = _intensity;
                    float _scatter = Bloom.scatter.value;
                    if (ImGui.SliderFloat("Scatter", ref _scatter, Bloom.scatter.min, Bloom.scatter.max))
                        Bloom.scatter.value = _scatter;
                    ImGui.TreePop();
                }
            }
        }
        ImGui.End();
    }
    protected override void ReadConfig() {
        if (Bloom) {
            Bloom.active = Config.Get<bool>("gvol.bloom.active") ?? Bloom.active;
            Bloom.threshold.value = Config.Get<float>("gvol.bloom.threshold") ?? Bloom.threshold.value;
            Bloom.intensity.value = Config.Get<float>("gvol.bloom.intensity") ?? Bloom.intensity.value;
            Bloom.scatter.value = Config.Get<float>("gvol.bloom.scatter") ?? Bloom.scatter.value;
        }
    }
    protected override void WriteConfig() {
        if (Bloom) {
            Config.Set("gvol.bloom.active", Bloom.active);
            Config.Set("gvol.bloom.threshold", Bloom.threshold.value);
            Config.Set("gvol.bloom.intensity", Bloom.intensity.value);
            Config.Set("gvol.bloom.scatter", Bloom.scatter.value);
        }
    }
}