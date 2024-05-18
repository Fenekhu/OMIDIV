using ImGuiNET;
using SFB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class FFmpegController : RecorderController {

    private int _framerate = 0;

    void OnEnable() {
        FFmpegWrapper.LoadConfig();
        FFmpegWrapper.InitParams();
        _framerate = (int)FFmpegWrapper.FrameRate;
    }

    void OnDisable() {
        FFmpegWrapper.SaveConfig();
    }

    private void OnDestroy() {
        FFmpegWrapper.ForceKill();
    }

    private IEnumerator FrameTrigger() {
        while (GetStatus() == Status.Recording) {
            FireOnBeforeFrame();
            yield return new WaitForEndOfFrame();
            if (GetStatus() == Status.Recording) FireOnAfterFrame();
        }
    }

    public override Status GetStatus() {
        if (FFmpegWrapper.IsReencoding) return Status.Processing;
        if (FFmpegWrapper.IsRecording) return Status.Recording;
        return Status.Standby;
    }

    public override float GetFramerate() {
        return FFmpegWrapper.FrameRate;
    }

    public override void StartRecording() {
        FFmpegWrapper.StartRecording();
        FireOnRecordingBegin();
        StartCoroutine(FrameTrigger());
    }

    public override void StopRecording() {
        FFmpegWrapper.EndRecording();
        FireOnRecordingEnd();
    }

    public override void DrawGUI() {
        bool _setOutDir = false;
        bool _openFfmpegDir = false;

        if (ImGui.Begin("Video Recorder")) {

            if (!FFmpegWrapper.ExecutableExists()) {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "FFmpeg executable not found.");
                ImGui.Text("Please place your ffmpeg executable \nin the following directory:");
                ImGui.TextDisabled(FFmpegWrapper.GetExecutableDir());
                ImGui.Text("Then press Refresh.");
                ImGui.Text("The executable must be called:");
                ImGui.Text("On Windows: ffmpeg.exe");
                ImGui.Text("On Mac/Linux: ffmpeg");

                _openFfmpegDir = ImGui.Button("Open that folder");
                if (ImGui.Button("Refresh"))
                    FFmpegWrapper.ExecutableExists(true);
            } else {
                bool _recording = RecordingEnabled;
                if (ImGui.Checkbox("Record on play", ref _recording))
                    RecordingEnabled = _recording;

                ImGui.Text(" ");

                ImGui.Text("Save folder:");
                ImGui.TextDisabled(FFmpegWrapper.OutDir);
                _setOutDir = ImGui.Button("Set...");

                ImGui.Text(" ");

                bool _useHardware = FFmpegWrapper.UseHardware;
                if (ImGui.Checkbox("Use hardware encoding", ref _useHardware))
                    FFmpegWrapper.UseHardware = _useHardware;

                bool _renc = FFmpegWrapper.Reencode;
                if (ImGui.Checkbox("Re-encode afterwards", ref _renc))
                    FFmpegWrapper.Reencode = _renc;

                ImGui.SetNextItemWidth(50);
                if (ImGui.InputInt("Framerate", ref _framerate, 0, 0) && _framerate > 0)
                    FFmpegWrapper.FrameRate = (uint)_framerate;

                bool _showMouse = FFmpegWrapper.GetCmdParam("show_mouse") != "0";
                if (ImGui.Checkbox("Record Mouse", ref _showMouse))
                    FFmpegWrapper.SetCmdParam("show_mouse", _showMouse ? "1" : "0");

                if (ImGui.TreeNode("Advanced")) {
                    ImGui.SetNextItemWidth(50);
                    string _initialFileExt = FFmpegWrapper.GetCmdParam("file_ext");
                    if (ImGui.InputText("Recording file extension", ref _initialFileExt, 8))
                        FFmpegWrapper.SetCmdParam("file_ext", _initialFileExt);

                    ImGui.SetNextItemWidth(50);
                    string _rencFileExt = FFmpegWrapper.GetCmdParam("file_ext_renc");
                    if (ImGui.InputText("Reencoding file extension", ref _rencFileExt, 8))
                        FFmpegWrapper.SetCmdParam("file_ext_renc", _rencFileExt);

                    bool _delTempFile = FFmpegWrapper.DeleteTempFile;
                    if (ImGui.Checkbox("Delete temp file after re-encoding", ref _delTempFile))
                        FFmpegWrapper.DeleteTempFile = _delTempFile;

                    ImGui.SetNextItemWidth(50);
                    float _rencScale = float.Parse(FFmpegWrapper.GetCmdParam("renc_scale"));
                    if (ImGui.InputFloat("Reencoded output scale", ref _rencScale, 0, 0)) 
                        FFmpegWrapper.SetCmdParam("renc_scale", _rencScale.ToString());

                    ImGui.PushItemWidth(50);

                    int _initialCRF = int.Parse(FFmpegWrapper.GetCmdParam("crf"));
                    if (ImGui.InputInt("Software Recording CRF", ref _initialCRF, 0, 0))
                        FFmpegWrapper.SetCmdParam("crf", _initialCRF.ToString());

                    int _hwCQ = int.Parse(FFmpegWrapper.GetCmdParam("cq"));
                    if (ImGui.InputInt("Hardware Recording cq", ref _hwCQ, 0, 0))
                        FFmpegWrapper.SetCmdParam("cq", _hwCQ.ToString());

                    int _rencCRF = int.Parse(FFmpegWrapper.GetCmdParam("crf_renc"));
                    if (ImGui.InputInt("Reencoding CRF", ref _rencCRF, 0, 0))
                        FFmpegWrapper.SetCmdParam("crf_renc", _rencCRF.ToString());

                    ImGui.PopItemWidth();

                    ImGui.Text("Hardware codec:");
                    int _hwCodec = FFmpegWrapper.GetCmdParam("hwcodec") switch {
                        "h264" => 0,
                        "hevc" => 1,
                        _ => 0,
                    };
                    bool _hwCodecChanged = ImGui.RadioButton("h264", ref _hwCodec, 0);
                    _hwCodecChanged |= ImGui.RadioButton("hevc", ref _hwCodec, 1);
                    if (_hwCodecChanged)
                        FFmpegWrapper.SetCmdParam("hwcodec", _hwCodec switch {
                            0 => "h264",
                            1 => "hevc",
                            _ => "h264"
                        });

                    ImGui.PushItemWidth(100);

                    string _swCodec = FFmpegWrapper.GetCmdParam("vcodec");
                    if (ImGui.InputText("Software codec", ref _swCodec, 16))
                        FFmpegWrapper.SetCmdParam("vcodec", _swCodec);

                    string _rencCodec = FFmpegWrapper.GetCmdParam("vcodec_renc");
                    if (ImGui.InputText("Reencoding codec", ref _rencCodec, 16))
                        FFmpegWrapper.SetCmdParam("vcodec_renc", _rencCodec);

                    string _preset = FFmpegWrapper.GetCmdParam("preset");
                    if (ImGui.InputText("Recording preset", ref _preset, 12))
                        FFmpegWrapper.SetCmdParam("preset", _preset);

                    string _rencPreset = FFmpegWrapper.GetCmdParam("preset_renc");
                    if (ImGui.InputText("Reencoding preset", ref _rencPreset, 12))
                        FFmpegWrapper.SetCmdParam("preset_renc", _rencPreset);

                    ImGui.PopItemWidth();

                    ImGui.TreePop();

                    if (ImGui.TreeNode("Super Advanced")) {
                        ImGuiInputTextFlags flags = ImGuiInputTextFlags.Multiline;
                        Vector2 boxSize = new Vector2(0, ImGui.GetTextLineHeight() * 3);

                        ImGui.Text("Software Recording command");
                        string argswin = FFmpegWrapper.ArgsWin;
                        if (ImGui.InputTextMultiline("##argswin", ref argswin, 512, boxSize, flags))
                            FFmpegWrapper.ArgsWin = argswin;
                        if (ImGui.Button("Reset##argswin"))
                            FFmpegWrapper.ArgsWin = FFmpegWrapper.ArgsWinDef;

                        ImGui.Text("Hardware Recording command");
                        string argshwwin = FFmpegWrapper.ArgsHWWin;
                        if (ImGui.InputTextMultiline("##argshwwin", ref argshwwin, 512, boxSize, flags))
                            FFmpegWrapper.ArgsHWWin = argshwwin;
                        if (ImGui.Button("Reset##argshwwin"))
                            FFmpegWrapper.ArgsHWWin = FFmpegWrapper.ArgsHWWinDef;

                        ImGui.Text("Reencoding command");
                        string argsrencwin = FFmpegWrapper.ArgsRencWin;
                        if (ImGui.InputTextMultiline("##argsrencwin", ref argsrencwin, 512, boxSize, flags))
                            FFmpegWrapper.ArgsHWWin = argsrencwin;
                        if (ImGui.Button("Reset##argsrencwin"))
                            FFmpegWrapper.ArgsRencWin = FFmpegWrapper.ArgsRencWinDef;

                        ImGui.TreePop();
                    }
                }
            }
        }

        if (_setOutDir) {
            string dir = FFmpegWrapper.OutDir;
            while (dir.EndsWith("/") || dir.EndsWith("\\")) dir.Remove(dir.Length - 1);
            StandaloneFileBrowser.OpenFolderPanelAsync("Video output directory", FFmpegWrapper.OutDir, false, (string[] res) => {
                if (res.Length > 0) {
                    if (!(res[0].EndsWith("/") || res[0].EndsWith("\\"))) res[0] += "/"; 
                    FFmpegWrapper.OutDir = res[0];
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
                    FileName = FFmpegWrapper.GetExecutableDir(),
                    UseShellExecute = true,
                    Verb = "open"
                }); break;
            default:
                throw new NotImplementedException();
            }
        }
    }
}
