using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class FFmpegWrapper {
    static Dictionary<string, string> CmdParams = new Dictionary<string, string>() {
        {"crf", "0"},
        {"crf_renc", "23"},
        {"cq", "0"},
        {"file_ext", "mkv"},
        {"file_ext_renc", "mp4"},
        {"framerate", "30"},
        {"hwcodec", "h264"},
        //{"hwnd", ""},
        {"outdir", ""},
        {"outfile", ""},
        {"preset", "ultrafast"},
        {"preset_renc", "slow"},
        {"renc_scale", "1"},
        {"renc_size", ""},
        {"show_mouse", "0"},
        {"title", "OMIDIV"},
        {"vcodec", "libx264"},
        {"vcodec_renc", "libx264"},
        {"window_size", ""},
        {"window_x", ""},
        {"window_y", ""},
    };

    public static void SetCmdParam(string name, string value) {
        CmdParams[name] = value;
    }

    public static string GetCmdParam(string name) {
        return CmdParams[name];
    }

    public static string GetExecutableDir() {
        return Application.streamingAssetsPath + "/ffmpeg/";
    }

    public static string GetExecutablePath() {
        string path_ext = "";
        switch (Application.platform) {
        case RuntimePlatform.WindowsPlayer:
        case RuntimePlatform.WindowsEditor:
            path_ext = "ffmpeg.exe"; break;
        case RuntimePlatform.LinuxPlayer:
        case RuntimePlatform.LinuxEditor:
        case RuntimePlatform.OSXPlayer:
        case RuntimePlatform.OSXEditor:
            path_ext = "ffmpeg"; break;
        default:
            throw new PlatformNotSupportedException();
        }
        return GetExecutableDir() + path_ext;
    }

    private static bool? _ExecExists = null;
    public static bool ExecutableExists(bool actuallyCheck = false) {
        if (_ExecExists == null || actuallyCheck) {
            _ExecExists = System.IO.File.Exists(GetExecutablePath());
        }
        return _ExecExists.Value;
    }

    static Process ffmpeg = null;
    private static uint _framerate = 30;
    public static uint FrameRate { get => _framerate; set { _framerate = value; CmdParams["framerate"] = value.ToString(); } }
    public static bool IsRecording { get; set; } = false;
    public static bool Reencode { get; set; } = true;
    public static bool IsReencoding { get; set; } = false;
    public static bool UseHardware { get; set; }
    public static bool DeleteTempFile { get; set; } = true;

    public static bool IsRunning() {
        if (ffmpeg is null) return false;

        try {
            Process.GetProcessById(ffmpeg.Id);
        } catch (Exception e) when (e is ArgumentException or InvalidOperationException) {
            return false;
        }
        return true;
    }

    public static readonly string ArgsWinDef =
        "-f gdigrab " +
        "-framerate %framerate% " +
        "-draw_mouse %show_mouse% " +
#if UNITY_EDITOR || true
        "-video_size %window_size% " +
        "-offset_x %window_x% " +
        "-offset_y %window_y% " + 
        "-i desktop " +
#else
        "-i title=%title% " +
#endif
        "-c:v %vcodec% " +
        "-crf %crf% " +
        "-preset %preset% " +
        "%outfile%.%file_ext%";
    public static string ArgsWin { get; set; } = ArgsWinDef;

    public static readonly string ArgsHWWinDef =
        "-init_hw_device d3d11va " +
        "-filter_complex " +
            "ddagrab=" +
                "video_size=%window_size%" +
                ":offset_x=%window_x%" +
                ":offset_y=%window_y%" +
                ":draw_mouse=%show_mouse%" +
                ":framerate=%framerate% " +
        "-c:v %hwcodec%_nvenc " +
        "-cq:v %cq% " +
        "%outfile%.%file_ext%";
    public static string ArgsHWWin { get; set; } = ArgsHWWinDef;

    public static readonly string ArgsRencWinDef =
        "-i %outfile%_tmp.%file_ext% " +
        "-vf " +
            "scale=%renc_size% " +
        "-c:v %vcodec_renc% " +
        "-crf %crf_renc% " +
        "-preset %preset_renc% " +
        "-pix_fmt yuv420p " +
        "%outfile%.%file_ext_renc%";
    public static string ArgsRencWin { get; set; } = ArgsRencWinDef;

    enum CmdVariant {
        Standard, HWAccel, Reencode
    }

    public static string OutDir { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)+"/OMIDIV/";
    public static string OutFile { get; set; } = "";
    static string OutFileBase { get; set; } = "";

    static string GetNowString() {
        return DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
    }

    static string GetCmdString(bool reencoding) {
        switch (Application.platform) {
        case RuntimePlatform.WindowsEditor:
        case RuntimePlatform.WindowsPlayer:
            return reencoding ? ArgsRencWin : (UseHardware ? ArgsHWWin : ArgsWin);
        }

        throw new NotImplementedException();
    }

    static string ParseParams(string input) {
        // checks if input contains %param% and it isnt escaped (%%param%)
        int containsParam(string param) {
            int ix = input.IndexOf("%"+param+"%");
            if (ix < 0) return ix;
            if (ix > 0 && input[ix-1] == '%') return -1;
            return ix;
        }

        // replace unescaped params
        bool keepSearching = true;
        while (keepSearching) {
            keepSearching = false;
            foreach (var item in CmdParams) {
                // if it contains %key%
                while (containsParam(item.Key) is int ix && ix > 0) {
                    input = input.Remove(ix, item.Key.Length + 2);
                    input = input.Insert(ix, item.Value);
                    keepSearching = true; // keep iterating if a replacement happened
                }
            }
        }

        // unescape escaped % (%% => %)
        char after = '\0';
        for (int i = input.Length - 1; i >= 0; i--) {
            if (input[i] == '%' && after == '%') {
                input = input.Remove(i, 1);
                i++;
                after = '\0';
            } else
                after = input[i];
        }

        return input;
    }

    static string GetParsedCmdString(bool reencoding) {
        return ParseParams(GetCmdString(reencoding));
    }

    public static void InitParams() {
        //CmdParams["hwnd"] = WindowHelper.GetWindowHandle().ToString();

        if (!(OutDir.EndsWith("/") || OutFile.EndsWith("\\"))) OutDir += "/";
        OutFileBase = OutDir + GetNowString();
        OutFile = Reencode ? OutFileBase + "_tmp" : OutFileBase;
        CmdParams["outdir"] = OutDir;
        CmdParams["outfile"] = OutFile;

        int sw, sh, sx, sy;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        {
            Rect client = WindowHelper.GetClientRect();
            sw = (int)client.width;
            sh = (int)client.height;
            sx = (int)client.x;
            sy = (int)client.y;
        }
#elif UNITY_EDITOR
        // output width and height must be even
        sw = Screen.width;
        sh = Screen.height;
        // unity reports the entire game container position as window position, but reports simulated size as width/height.
        // thus i have to use these arbitrary measured values of where the game appears on my screen.
        sx = 1143;
        sy = 336;
#else
        sw = Screen.width;
        sh = Screen.height;
        sx = Screen.mainWindowPosition.x;
        sy = Screen.mainWindowPosition.y;
#endif
        sw = sw / 2 * 2;
        sh = sh / 2 * 2;
        float rencScale = float.Parse(CmdParams["renc_scale"]);
        int rw = (int)(sw * rencScale) / 2 * 2;
        int rh = (int)(sh * rencScale) / 2 * 2;
        CmdParams["window_size"] = $"{sw}x{sh}";
        CmdParams["window_x"] = sx.ToString();
        CmdParams["window_y"] = sy.ToString();
        CmdParams["renc_size"] = $"{rw}x{rh}";
    }

    public static void LoadConfig() {

        // since you cant pass references to properties, i have to use an action instead
        void getString(string key, Action<string> found) {
            if (PlayerPrefs.HasKey(key)) found(PlayerPrefs.GetString(key));
        }
        /*void getInt(string key, Action<int> found) {
            if (PlayerPrefs.HasKey(key)) found(PlayerPrefs.GetInt(key));
        }*/
        void getBool(string key, Action<bool> found) {
            if (PlayerPrefs.HasKey(key))
                found(Convert.ToBoolean(PlayerPrefs.GetInt(key)));
        }

        // key array has to be copied. iterating directly though CmdParams.Keys isn't allowed since this would modify the enumerator
        string[] keys = new string[CmdParams.Count];
        CmdParams.Keys.CopyTo(keys, 0);
        foreach (string key in keys) {
            getString("vrec.ffmpeg1." + key, (string val) => { CmdParams[key] = val; });
        }

        // some things don't need to be remembered
        //CmdParams["hwnd"] = "";
        CmdParams["outfile"] = "";
        CmdParams["window_size"] = "";
        CmdParams["window_x"] = "";
        CmdParams["window_y"] = "";

        getBool("vrec.ffmpeg1.renc", (bool val) => { Reencode = val; });
        getBool("vrec.ffmpeg1.useHW", (bool val) => { UseHardware = val; });
        getBool("vrec.ffmpeg1.delTempFile", (bool val) => { DeleteTempFile = val; });

        getString("vrec.ffmpeg1.argswin", (string val) => { ArgsWin = val; });
        getString("vrec.ffmpeg1.argshwwin", (string val) => { ArgsHWWin = val; });
        getString("vrec.ffmpeg1.argsrencwin", (string val) => { ArgsRencWin = val; });
    }

    public static void SaveConfig() {
        foreach (var item in CmdParams) 
            PlayerPrefs.SetString("vrec.ffmpeg1." + item.Key, item.Value);

        PlayerPrefs.SetInt("vrec.ffmpeg1.renc", Convert.ToInt32(Reencode));
        PlayerPrefs.SetInt("vrec.ffmpeg1.useHW", Convert.ToInt32(UseHardware));
        PlayerPrefs.SetInt("vrec.ffmpeg1.delTempFile", Convert.ToInt32(DeleteTempFile));

        PlayerPrefs.SetString("vrec.ffmpeg1.argswin", ArgsWin);
        PlayerPrefs.SetString("vrec.ffmpeg1.argshwwin", ArgsHWWin);
        PlayerPrefs.SetString("vrec.ffmpeg1.argsrencwin", ArgsRencWin);
    }

    private static string ffmpegErrOut = "";
    private static DataReceivedEventHandler ffmpegErrorHandler = new DataReceivedEventHandler(
        (sender, e) => {
            if (e.Data == null) {
                Console.Error.WriteLine(ffmpegErrOut);
                UnityEngine.Debug.LogError(ffmpegErrOut);
                ffmpegErrOut = "";
            } else ffmpegErrOut += e.Data + "\n";
        });

    public static void StartRecording() {
        InitParams();

        if (ffmpeg != null && !ffmpeg.HasExited) ffmpeg.Kill();

        ffmpeg = new Process();
        ffmpeg.StartInfo.FileName = GetExecutablePath();
        ffmpeg.StartInfo.UseShellExecute = false;
        ffmpeg.StartInfo.CreateNoWindow = true;
        ffmpeg.StartInfo.RedirectStandardInput = true;
        //ffmpeg.StartInfo.RedirectStandardOutput = true;
        ffmpeg.StartInfo.RedirectStandardError = true;
        ffmpeg.ErrorDataReceived += ffmpegErrorHandler;
        ffmpeg.StartInfo.Arguments = GetParsedCmdString(false);

        System.IO.Directory.CreateDirectory(OutDir);

        UnityEngine.Debug.Log("Launching ffmpeg with command: \n" + ffmpeg.StartInfo.Arguments);

        ffmpeg.Start();
        ffmpeg.BeginErrorReadLine();
        IsRecording = true;
    }

    public static async void EndRecording() {
        if (ffmpeg is null) return;

        await Task.Run(() => {
            if (!ffmpeg.HasExited) ffmpeg.StandardInput.Write('q');
            ffmpeg.WaitForExit();
            ffmpeg.Close();
            ffmpeg.Dispose();
            ffmpeg = null;
            IsRecording = false;

            if (Reencode) StartReencoding();
        });

    }

    static void StartReencoding() {
        CmdParams["outfile"] = OutFileBase;

        if (ffmpeg != null && !ffmpeg.HasExited) ffmpeg.Kill();

        ffmpeg = new Process();
        ffmpeg.StartInfo.FileName = GetExecutablePath();
        ffmpeg.StartInfo.UseShellExecute = false;
        ffmpeg.StartInfo.CreateNoWindow = true;
        ffmpeg.StartInfo.RedirectStandardInput = true;
        //ffmpeg.StartInfo.RedirectStandardOutput = true;
        ffmpeg.StartInfo.RedirectStandardError = true;
        ffmpeg.ErrorDataReceived += ffmpegErrorHandler;
        ffmpeg.StartInfo.Arguments = GetParsedCmdString(true);

        UnityEngine.Debug.Log("Launching ffmpeg with command: \n" + ffmpeg.StartInfo.Arguments);

        IsReencoding = true;

        ffmpeg.Start();
        ffmpeg.BeginErrorReadLine();
        ffmpeg.WaitForExit();
        ffmpeg = null;

        IsReencoding = false;

        string temp_file = OutFile + "." + CmdParams["file_ext"];
        if (DeleteTempFile && System.IO.File.Exists(temp_file))
            System.IO.File.Delete(temp_file);
    }

    public static void ForceKill() {
        if (ffmpeg != null && !ffmpeg.HasExited) ffmpeg.Kill();
        ffmpeg = null;
    }
}
