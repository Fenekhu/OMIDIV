using ImGuiNET;
using SFB;
using System.IO;
using UnityEngine;

/// <summary>
/// Controls audio playing and gives it a UI.
/// </summary>
public class AudioPlayer : OmidivComponent {

    protected static FileInfo AudioPath;
    protected static AudioClip AudioClip;
    /// <summary>Milliseconds</summary>
    protected static int AudioOffset;
    private static int audioOffsetPrev;
    protected static bool AudioOffsetChanged;
    /// <summary>Seconds</summary>
    protected static float AudioTime = 0;
    protected static bool IsPaused = false;

    protected NAudioImporter AudioImporter;
    protected AudioSource Sound;
    private bool bOpenAudio = false;

    protected void SetAudio(AudioClip clip) {
        AudioClip = clip;
        Sound.clip = clip;
    }

    protected override void OnEnable() {
        base.OnEnable();
        ImGuiManager.DrawMainMenuItems += DrawMainMenuItems;
    }

    protected override void OnDisable() {
        base.OnDisable();
        ImGuiManager.DrawMainMenuItems -= DrawMainMenuItems;
    }

    protected void Start() {
        if (Sound == null) {
            Sound = gameObject.AddComponent<AudioSource>();
        }
        if (AudioImporter is null) {
            AudioImporter = gameObject.AddComponent<NAudioImporter>();
            AudioImporter.Loaded += SetAudio;
        }
        if (AudioClip != null) Sound.clip = AudioClip;
    }

    protected void Update() {
        if (bOpenAudio) {
            StandaloneFileBrowser.OpenFilePanelAsync("Open Audio", "", "", false, (string[] res) => {
                if (res.Length > 0) {
                    AudioPath = new FileInfo(res[0]);
                    SceneController.NeedsStopPlay = true;
                    SceneController.NeedsAudioReload = true;
                    SceneController.NeedsRestart = true;
                }
            });
            bOpenAudio = false;
        }

        if (AudioOffset != audioOffsetPrev) {
            float diff = (AudioOffset - audioOffsetPrev)/1000f; // seconds
            AudioTime += diff;
            if (IsPlaying) {
                if (AudioTime < 0) {
                    Sound.time = 0;
                    Sound.PlayDelayed(-AudioTime);
                } else {
                    Sound.time = AudioTime;
                }
            }

            audioOffsetPrev = AudioOffset;
            AudioOffsetChanged = true;
        } else {
            AudioOffsetChanged = false;
        }

        // this doesn't need to use MidiScene.DeltaTime because audio isnt recorded.
        if (IsPlaying) AudioTime += Time.unscaledDeltaTime;
    }

    protected override void DrawGUI() {
        if (ImGui.Begin("Audio Controls")) {
            ImGui.PushItemWidth(128.0f);
            float v = Sound.volume;
            if (ImGui.SliderFloat("Volume", ref v, 0.0f, 1.0f)) {
                Sound.volume = v;
            }
            ImGui.InputInt("Delay (ms, +/-)", ref AudioOffset, 100, 2000);
            ImGui.PopItemWidth();
        }
        ImGui.End();
    }

    protected void DrawMainMenuItems(string menuName) {
        if (menuName == "File") {
            bOpenAudio = ImGui.MenuItem("Open Audio");
            ImGui.Separator();
        }
    }

    protected override void OnPlayStart() {
        IsPaused = false;
        if (AudioTime < 0) {
            Sound.time = 0;
            Sound.PlayDelayed(-AudioTime);
        } else {
            Sound.time = AudioTime;
            Sound.Play();
        }
    }
    protected override void OnPlayStop() {
        if (Sound != null) Sound.Stop();
        IsPaused = true;
    }

    protected override void Restart() {
        IsPaused = false;
        AudioTime = AudioOffset / 1000f;
    }

    protected override void LoadAudio() {
        LoadAudio_();
        AudioTime = AudioOffset / 1000f;
    }

    private void LoadAudio_() {
        AudioImporter.Import(AudioPath.FullName);
        if (AudioImporter.isError) {
            Debug.Log(AudioImporter.error);
        }
    }
}