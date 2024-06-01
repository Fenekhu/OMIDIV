using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class FFmpegWrapper2 {
    static Dictionary<string, string> CmdParams = new Dictionary<string, string>() {
        {"file_ext", "mp4"},
        {"framerate", "60"},
        {"outdir", ""},
        {"outfile", ""},
        {"crf", "0"},
        {"preset", "fast"},
        {"pix_fmt", "yuv420p"},
        {"vcodec", "libx264"},
        {"video_size", ""},
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

    public static string GetExecutablePath(bool quoted = false) {
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
        string ret = GetExecutableDir() + path_ext;
        if (quoted) ret = "\"" + ret + "\"";
        return ret;
    }

    private static bool? _ExecExists = null;
    public static bool ExecutableExists(bool actuallyCheck = false) {
        if (_ExecExists == null || actuallyCheck) {
            _ExecExists = File.Exists(GetExecutablePath());
        }
        return _ExecExists.Value;
    }

    static Process ffmpeg = null;
    private static uint _framerate = 30;
    public static uint FrameRate { get => _framerate; set { _framerate = value; CmdParams["framerate"] = value.ToString(); } }
    public static bool IsRecording { get; set; } = false;
    public static bool IsReencoding { get; set; } = false;
    public static float VideoScale = 1f;
    public static (int w, int h) VideoSize { get { 
            return 
                ((int)(Screen.width * VideoScale)/2*2, (int)(Screen.height * VideoScale)/2*2); 
        } }

    public static bool IsRunning() {
        if (ffmpeg is null) return false;

        try {
            Process.GetProcessById(ffmpeg.Id);
        } catch (Exception e) when (e is ArgumentException or InvalidOperationException) {
            return false;
        }
        return true;
    }

    public static readonly string ExecArgsDef =
        "-loglevel error " +

        "-f rawvideo " +
        "-vcodec rawvideo " +
        "-framerate %framerate% " +
        "-pix_fmt argb " + 
        "-s %video_size% " +
        "-i - " +

        "-vf \"vflip\" " + 
        "-pix_fmt %pix_fmt% " +
        "-c:v %vcodec% " + 
        "-crf %crf% " +
        "\"%outfile%.%file_ext%\"";
    public static string ExecArgs { get; set; } = ExecArgsDef;
    public static readonly float ArgsVer = 1f;

    public static string OutDir { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)+"/OMIDIV/";
    public static string OutFile { get; set; } = "";

    static string GetNowString() {
        return DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
    }

    static string GetCmdString(bool reencoding) {
        if (!reencoding) return ExecArgs;

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
        if (!(OutDir.EndsWith("/") || OutFile.EndsWith("\\"))) OutDir += "/";
        OutFile = OutDir + GetNowString();
        CmdParams["outdir"] = OutDir;
        CmdParams["outfile"] = OutFile;

        int
        vw = VideoSize.w,
        vh = VideoSize.h;
        CmdParams["video_size"] = $"{vw}x{vh}";
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
            getString("vrec.ffmpeg2." + key, (string val) => { CmdParams[key] = val; });
        }

        // some things don't need to be remembered
        CmdParams["outfile"] = "";

        float argsVer = 0;
        getString("vrec.ffmpeg2.argsVer", (string val) => { argsVer = float.Parse(val); });
        if (argsVer != ArgsVer) ExecArgs = ExecArgsDef;
        else getString("vrec.ffmpeg2.execArgs", (string val) => { ExecArgs = val; });
        getString("vrec.ffmpeg2.video_scale", (string val) => { VideoScale = float.Parse(val); });
    }

    public static void SaveConfig() {
        foreach (var item in CmdParams)
            PlayerPrefs.SetString("vrec.ffmpeg2." + item.Key, item.Value);

        PlayerPrefs.SetString("vrec.ffmpeg2.argsVer", ArgsVer.ToString());
        PlayerPrefs.SetString("vrec.ffmpeg2.execArgs", ExecArgs);
        PlayerPrefs.SetString("vrec.ffmpeg2.video_scale", VideoScale.ToString());
    }

    private static string ffmpegErrOut = "";
    private static DataReceivedEventHandler ffmpegErrorHandler = new DataReceivedEventHandler(
        (sender, e) => {
            if (e.Data == null && ffmpegErrOut.Length != 0) {
                Console.Error.WriteLine(ffmpegErrOut);
                Debug.LogError(ffmpegErrOut);
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

        Directory.CreateDirectory(OutDir);

        Debug.Log("Launching ffmpeg with command: \n" + ffmpeg.StartInfo.Arguments);

        ffmpeg.Start();
        ffmpeg.BeginErrorReadLine();
        IsRecording = true;
    }

    private static byte[][] frameBuffer = new byte[16][];
    private static int startFrameNum = 0;
    public static void ReceiveFrame(ref NativeArray<byte> narray, uint frameNum) {
        if (ffmpeg == null || ffmpeg.HasExited) return;

        int GetReadyFrames() {
            int count = 0;
            for (int i = 0; i < frameBuffer.Length; i++) {
                if (frameBuffer[i] != null) {
                    count++;
                } else break;
            }
            return count;
        }

        int bufferIndex = (int)frameNum - startFrameNum;
        if (bufferIndex < 0) { Debug.LogError("ReceiveFrame: initial buffer index < 0: "+bufferIndex); return; }

        if (bufferIndex >= frameBuffer.Length) {
            int writeCount = GetReadyFrames();
            if (writeCount == 0) { Debug.LogError("ReceiveFrame: writeCount is 0"); return; }
            for (int i = 0; i < writeCount; i++) {
                ffmpeg.StandardInput.BaseStream.Write(frameBuffer[i]);
                ffmpeg.StandardInput.Flush();
            }
            for (int i = writeCount; i < frameBuffer.Length; i++) {
                frameBuffer[writeCount-i] = frameBuffer[i];
            }
            for (int i = frameBuffer.Length - writeCount; i < frameBuffer.Length; i++) {
                frameBuffer[i] = null;
            }
            startFrameNum += writeCount;
            bufferIndex -= writeCount;

            if (bufferIndex < 0 || bufferIndex >= frameBuffer.Length) { Debug.LogError("ReceiveFrame: buffer index invalid after flush: "+bufferIndex); return; }
        }

        frameBuffer[bufferIndex] = narray.ToArray();

    }

    public static void EndRecording() {
        IsRecording = false;

        if (ffmpeg == null || ffmpeg.HasExited) return;

        Task.Run(async () => {
            await Task.Delay(500);
            ffmpeg.StandardInput.Close();
            ffmpeg.WaitForExit();
            ffmpeg = null;
            startFrameNum = 0;
            Array.Clear(frameBuffer, 0, frameBuffer.Length); // this should be empty anyway?
            //StartReencoding(); 
        });
    }

    // Re-encoding is currently unused, but may be used in the future to add audio.
    static void StartReencoding() {
        IsReencoding = true;

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

        Debug.Log("Launching ffmpeg with command: \n" + ffmpeg.StartInfo.Arguments);

        ffmpeg.Start();
        ffmpeg.BeginErrorReadLine();
        ffmpeg.WaitForExit();
        ffmpeg = null;

        IsReencoding = false;
    }

    public static void ForceKill() {
        if (ffmpeg != null && !ffmpeg.HasExited) ffmpeg.Kill();
        ffmpeg = null;
    }
}
