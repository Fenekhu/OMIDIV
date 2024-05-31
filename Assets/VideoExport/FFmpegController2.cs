using ImGuiNET;
using SFB;
using System;
using System.Collections;
using UnityEngine;

public class FFmpegController2 : RecorderController {
    private int _framerate = 60;

    private int _autoHideUI = 0;
    public bool AutoHideUI { get { return _autoHideUI != 0; } set { _autoHideUI = value ? 1 : 0; } }

    protected override void OnEnable() {
        base.OnEnable();
        FFmpegWrapper2.LoadConfig();
        FFmpegWrapper2.InitParams();
        _framerate = (int)FFmpegWrapper2.FrameRate;
        _autoHideUI = PlayerPrefs.GetInt("vrec.ffmpeg2.autoHideUI", _autoHideUI);
    }

    protected override void OnDisable() {
        base.OnDisable();
        FFmpegWrapper2.SaveConfig();
        PlayerPrefs.SetInt("vrec.ffmpeg2.autoHideUI", _autoHideUI);
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        FFmpegWrapper2.ForceKill();
    }

    private IEnumerator FrameTrigger() {
        while (GetStatus() == Status.Recording) {
            FireOnBeforeFrame();
            yield return new WaitForEndOfFrame();
            if (GetStatus() == Status.Recording) FireOnAfterFrame();
        }
    }

    public override Status GetStatus() {
        if (FFmpegWrapper2.IsReencoding) return Status.Processing;
        if (FFmpegWrapper2.IsRecording) return Status.Recording;
        return Status.Standby;
    }

    public override float GetFramerate() {
        return FFmpegWrapper2.FrameRate;
    }

    public override void StartRecording() {
        if (AutoHideUI) ImGuiManager.IsEnabled = false;
        FFmpegWrapper2.StartRecording();
        FireOnRecordingBegin();
        StartCoroutine(FrameTrigger());
    }

    public override void StopRecording() {
        FFmpegWrapper2.EndRecording();
        FireOnRecordingEnd();
    }

    protected override void DrawGUI() {
        bool _setOutDir = false;
        bool _openFfmpegDir = false;

        if (ImGui.Begin("Video Recorder")) {

            if (!FFmpegWrapper2.ExecutableExists()) {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "FFmpeg executable not found.");
                ImGui.Text("Please place your ffmpeg executable \nin the following directory:");
                ImGui.TextDisabled(FFmpegWrapper2.GetExecutableDir());
                ImGui.Text("Then press Refresh.");
                ImGui.Text("The executable must be called:");
                ImGui.Text("On Windows: ffmpeg.exe");
                ImGui.Text("On Mac/Linux: ffmpeg");

                _openFfmpegDir = ImGui.Button("Open that folder");
                if (ImGui.Button("Refresh"))
                    FFmpegWrapper2.ExecutableExists(true);
            } else {
                bool _recording = RecordingEnabled;
                if (ImGui.Checkbox("Record on play", ref _recording))
                    RecordingEnabled = _recording;

                bool _b_ahui = AutoHideUI;
                if (ImGui.Checkbox("Auto-hide UI", ref _b_ahui))
                    AutoHideUI = _b_ahui;
                ImGui.TextDisabled("The UI WILL be recorded if visible.");

                ImGui.Text(" ");

                ImGui.Text("Save folder:");
                ImGui.TextDisabled(FFmpegWrapper2.OutDir);
                _setOutDir = ImGui.Button("Set...");

                ImGui.Text(" ");

                ImGui.SetNextItemWidth(50);
                if (ImGui.InputInt("Framerate", ref _framerate, 0, 0) && _framerate > 0)
                    FFmpegWrapper2.FrameRate = (uint)_framerate;

                if (ImGui.TreeNode("Advanced")) {
                    ImGui.SetNextItemWidth(50);
                    string _initialFileExt = FFmpegWrapper2.GetCmdParam("file_ext");
                    if (ImGui.InputText("Recording file extension", ref _initialFileExt, 8))
                        FFmpegWrapper2.SetCmdParam("file_ext", _initialFileExt);

                    ImGui.SetNextItemWidth(50);
                    float _videoScale = FFmpegWrapper2.VideoScale;
                    if (ImGui.InputFloat("Reencoded output scale", ref _videoScale, 0, 0))
                        FFmpegWrapper2.VideoScale = _videoScale;

                    ImGui.SetNextItemWidth(100);
                    string _swCodec = FFmpegWrapper2.GetCmdParam("vcodec");
                    if (ImGui.InputText("Encoding Codec", ref _swCodec, 16))
                        FFmpegWrapper2.SetCmdParam("vcodec", _swCodec);

                    ImGui.SetNextItemWidth(50);
                    int _initialCRF = int.Parse(FFmpegWrapper2.GetCmdParam("crf"));
                    if (ImGui.InputInt("Encoding CRF", ref _initialCRF, 0, 0))
                        FFmpegWrapper2.SetCmdParam("crf", _initialCRF.ToString());

                    ImGui.SetNextItemWidth(100);
                    string _preset = FFmpegWrapper2.GetCmdParam("preset");
                    if (ImGui.InputText("Encoding preset", ref _preset, 12))
                        FFmpegWrapper2.SetCmdParam("preset", _preset);

                    ImGui.TreePop();

                    if (ImGui.TreeNode("Super Advanced")) {
                        ImGuiInputTextFlags flags = ImGuiInputTextFlags.Multiline;
                        Vector2 boxSize = new Vector2(0, ImGui.GetTextLineHeight() * 3);

                        ImGui.Text("Encoding command");
                        string execArgs = FFmpegWrapper2.ExecArgs;
                        if (ImGui.InputTextMultiline("##execArgs", ref execArgs, 512, boxSize, flags))
                            FFmpegWrapper2.ExecArgs = execArgs;
                        if (ImGui.Button("Reset##execArgs"))
                            FFmpegWrapper2.ExecArgs = FFmpegWrapper2.ExecArgsDef;

                        ImGui.TreePop();
                    }
                }
            }
        }

        if (_setOutDir) {
            string dir = FFmpegWrapper2.OutDir;
            while (dir.EndsWith("/") || dir.EndsWith("\\")) dir = dir[..^1];
            StandaloneFileBrowser.OpenFolderPanelAsync("Video output directory", FFmpegWrapper2.OutDir, false, (string[] res) => {
                if (res.Length > 0) {
                    if (!(res[0].EndsWith("/") || res[0].EndsWith("\\"))) res[0] += "/";
                    FFmpegWrapper2.OutDir = res[0];
                }
            });
        }

        if (_openFfmpegDir) {
            switch (Application.platform) {
            case RuntimePlatform.LinuxEditor:
            case RuntimePlatform.LinuxPlayer:
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.WindowsPlayer:
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() {
                    FileName = FFmpegWrapper2.GetExecutableDir(),
                    UseShellExecute = true,
                    Verb = "open"
                }); break;
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.OSXPlayer:
                System.Diagnostics.Process.Start("open", $"\"{FFmpegWrapper2.GetExecutableDir()}\"");
                break;
            default:
                throw new NotImplementedException();
            }
        }
    }
}
