﻿using ImGuiNET;
using SFB;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

public class AudioPlayer : OmidivComponent {

    protected static FileInfo AudioPath;
    protected static AudioClip AudioClip;
    protected static int AudioOffset;
    private static int audioOffsetPrev;
    protected static bool AudioOffsetChanged;
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
                    MidiScene.NeedsStopPlay = true;
                    MidiScene.NeedsAudioReload = true;
                    MidiScene.NeedsRestart = true;
                }
            });
            bOpenAudio = false;
        }

        if (AudioOffset != audioOffsetPrev) {
            float diff = (AudioOffset - audioOffsetPrev)/1000f;
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

        if (IsPlaying) AudioTime += MidiScene.DeltaTime;
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

    protected override void ReadConfig() { }
    protected override void WriteConfig() { }

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

    protected override void Reset_() { }

    protected override void Restart() {
        Sound.Stop();
        AudioTime = AudioOffset / 1000f;
        IsPaused = false;
    }

    protected override void ReloadMidi() { }

    protected override void ReloadVisuals() { }

    protected override void ReloadAudio() {
        LoadAudio();
        AudioTime = AudioOffset / 1000f;
    }

    private void LoadAudio() {
        AudioImporter.Import(AudioPath.FullName);
        if (AudioImporter.isError) {
            Debug.Log(AudioImporter.error);
        }
    }
}