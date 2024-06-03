using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Provides a wrapper around the ffmpeg command line and process.
/// </summary>
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

    /// <summary>
    /// Sets the <paramref name="value"/> for a variable that can be used in the command arguments.
    /// </summary>
    /// <param name="name">The variable that will be replaced.</param>
    /// <param name="value">The value to replace it with.</param>
    /// <example>
    /// Exec Args:<br/>
    /// <c>-loglevel %loglevel% -f ...</c>
    /// <code>SetCmdParam("loglevel", "error")</code>
    /// Parsed command args passed to ffmpeg:<br/>
    /// <c>-loglevel error -f ...</c>
    /// </example>
    /// <remarks>Use fields/properties/setters within this class if they exist, for example <see cref="OutDir"/></remarks>
    /// <seealso cref="GetCmdParam(string)"/>
    public static void SetCmdParam(string name, string value) {
        CmdParams[name] = value;
    }

    /// <summary>
    /// Get the value that a command variable will be replaced with.
    /// </summary>
    /// <param name="name">The variable that will be replaced.</param>
    /// <returns>The value it will be replaced with.</returns>
    /// <seealso cref="SetCmdParam(string, string)"/>
    public static string GetCmdParam(string name) {
        return CmdParams[name];
    }

    /// <returns>The directory of the ffmpeg executable.</returns>
    /// <remarks>This will be <see cref="Application.streamingAssetsPath"/>/ffmpeg/</remarks>
    public static string GetExecutableDir() {
        return Application.streamingAssetsPath + "/ffmpeg/";
    }

    /// <param name="quoted">If true, puts the path in quotes (useful if it contains spaces).</param>
    /// <returns>The full path of the executable, with platform file types accounted for.</returns>
    /// <exception cref="PlatformNotSupportedException">If the platform is not Windows, Mac, or Linux Standalone/Editor.</exception>
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
    /// <param name="actuallyCheck">If true, queries the file system, otherwise returns a cached value. See remarks.</param>
    /// <returns>Whether the executable exists.</returns>
    /// <remarks>
    /// This method is called on each draw, so <paramref name="actuallyCheck"/> reduces the amount of filesystem ops.
    /// It is called with <paramref name="actuallyCheck"/><c> = true</c> when the VideoRecorder refresh button is pressed.
    /// </remarks>
    public static bool ExecutableExists(bool actuallyCheck = false) {
        if (_ExecExists == null || actuallyCheck) {
            _ExecExists = File.Exists(GetExecutablePath());
        }
        return _ExecExists.Value;
    }

    static Process ffmpeg = null;

    private static uint _framerate = 60; // cached value as an int so that CmdParams["framerate"] doesnt have to be called and parsed every time we need the framerate.
    /// <summary>The framerate the video will be recorded with.</summary>
    public static uint FrameRate { get => _framerate; set { _framerate = value; CmdParams["framerate"] = value.ToString(); } }

    /// <summary>Is ffmpeg currently running and recording frames.</summary>
    public static bool IsRecording { get; set; } = false;

    /// <summary>Currently not used, but may be used if the initial output needs to be modified, such as recompressed, changed, or audio added.</summary>
    public static bool IsReencoding { get; set; } = false;

    /// <summary>The amount the output resolution will be scaled by from the window resolution.</summary>
    public static float VideoScale = 1f;

    /// <summary>The final output size, based on the window size and <see cref="VideoScale"/>.</summary>
    /// <remarks>Both dimensions will be rounded down to a multiple of 2.</remarks>
    public static (int w, int h) VideoSize { get { 
            return 
                ((int)(Screen.width * VideoScale)/2*2, (int)(Screen.height * VideoScale)/2*2); 
        } }

    /// <returns>Whether the ffmpeg executable is running.</returns>
    public static bool IsRunning() {
        if (ffmpeg is null) return false;

        try {
            Process.GetProcessById(ffmpeg.Id);
        } catch (Exception e) when (e is ArgumentException or InvalidOperationException) {
            return false;
        }
        return true;
    }

    /// <summary>The default execution arguments, with unparsed variables.</summary>
    /// <seealso cref="ExecArgs"/>
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

    /// <summary>The current execution arguments, with unparsed variables.</summary>
    public static string ExecArgs { get; set; } = ExecArgsDef;

    /// <remarks>Changed when the default args are changed enough that the user's overriden arguments should be replaced with the default.</remarks>
    public static readonly float ArgsVer = 1f;

    /// <summary>The output directory of </summary>
    public static string OutDir { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)+"/OMIDIV/";

    /// <summary>The full filename for the output file.</summary>
    /// <remarks>Only valid during and after a recording has started.</remarks>
    /// <remarks>Only valid during and after a recording has started.</remarks>
    public static string OutFile { get; set; } = "";

    static string GetNowString() {
        return DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
    }

    /// <summary>Since reencoding currently doesnt happen, this just returns <see cref="ExecArgs"/> if false and throws if true.</summary>
    static string GetCmdString(bool reencoding) {
        if (!reencoding) return ExecArgs;

        throw new NotImplementedException();
    }

    /// <summary>Replaces all of the variables in a string with their CmdParam values.</summary>
    /// <param name="input">The string with variables to parse.</param>
    /// <returns>The string with its variables replaced.</returns>
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

    /// <param name="reencoding">Since reencoding currently doesn't happen, use false, or else this will throw.</param>
    /// <returns>The final parsed arguments to be passed to ffmpeg.</returns>
    static string GetParsedCmdString(bool reencoding) {
        return ParseParams(GetCmdString(reencoding));
    }

    /// <summary>
    /// Initializes certain parameters, such as video output size (based on window size) and output file name (based on current time).
    /// </summary>
    /// <remarks>Will be called by <see cref="StartRecording"/>, so you don't need to call it yourself.</remarks>
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

    /// <summary>Reads some settings from PlayerPrefs.</summary>
    public static void LoadConfig() {

        // since you cant pass references to properties, i have to use an action instead
        void getString(string key, Action<string> found) {
            if (PlayerPrefs.HasKey(key)) found(PlayerPrefs.GetString(key));
        }
        /*void getInt(string key, Action<int> found) {
            if (PlayerPrefs.HasKey(key)) found(PlayerPrefs.GetInt(key));
        }*/
        /*void getBool(string key, Action<bool> found) {
            if (PlayerPrefs.HasKey(key))
                found(Convert.ToBoolean(PlayerPrefs.GetInt(key)));
        }*/

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

    /// <summary>Saves some settings to PlayerPrefs.</summary>
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

    /// <summary>
    /// Starts the FFmpeg executable, which will be ready to accept frames sent to it with <see cref="ReceiveFrame(ref NativeArray{byte}, uint)"/>.
    /// </summary>
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

    // See remarks below
    private static byte[][] frameBuffer = new byte[16][];
    private static int startFrameNum = 0; // frame number of index 0 of the buffer.

    /// <summary>
    /// Passes a frame to FFmpeg while recording.
    /// </summary>
    /// <param name="narray">The frame data. Should be <see cref="VideoSize"/> width*height*4 in size.</param>
    /// <param name="frameNum">The number of the frame being sent. Can be sent out of order with a gap of up to 15.</param>
    /// <remarks>
    /// Frames are sent from Unity's AsyncGPUReadback in <see cref="FFmpegRenderFeature"/>.
    /// Unfortunately, they aren't sent in order, so they have to be buffered and reordered.
    /// </remarks>
    public static void ReceiveFrame(ref NativeArray<byte> narray, uint frameNum) {
        if (!IsRecording) return; // exit if not recording.

        // How many consecutive frames from the beginning are in order and ready to be flushed.
        static int GetReadyFrames() {
            int count = 0;
            for (int i = 0; i < frameBuffer.Length; i++) {
                if (frameBuffer[i] != null) {
                    count++;
                } else break;
            }
            return count;
        }

        // the index in the buffer that this frame should go
        int bufferIndex = (int)frameNum - startFrameNum;
        if (bufferIndex < 0) { Debug.LogError("ReceiveFrame: initial buffer index < 0: "+bufferIndex); return; }

        // if we got a frame thats beyond the buffer, flush the ready frames in the buffer.
        if (bufferIndex >= frameBuffer.Length) {
            int writeCount = GetReadyFrames();
            if (writeCount == 0) { Debug.LogError("ReceiveFrame: writeCount is 0"); return; }
            for (int i = 0; i < writeCount; i++) { // write each ready frame and flush it.
                ffmpeg.StandardInput.BaseStream.Write(frameBuffer[i]);
                ffmpeg.StandardInput.Flush();
            }
            for (int i = writeCount; i < frameBuffer.Length; i++) { // copy all the unready buffer slots to the beginning of the buffer to "move" the buffer forward.
                frameBuffer[writeCount-i] = frameBuffer[i];
            }
            for (int i = frameBuffer.Length - writeCount; i < frameBuffer.Length; i++) { // clear all the buffer slots that were just copied from.
                frameBuffer[i] = null;
            }
            startFrameNum += writeCount; // update the frame number that index 0 represents.
            bufferIndex -= writeCount; // now that the buffer has been cleared, recalculate the place our frame needs to go

            // if its still beyond the buffer's end, uh oh.
            if (bufferIndex < 0 || bufferIndex >= frameBuffer.Length) { Debug.LogError("ReceiveFrame: buffer index invalid after flush: "+bufferIndex); return; }
        }

        // write the frame into the buffer
        frameBuffer[bufferIndex] = narray.ToArray();

    }

    /// <summary>
    /// (Asynchronously) waits half a second for frames to finish being sent, then tells ffmpeg that frames are done being sent and closes ffmpeg.
    /// </summary>
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

    /// <summary>Forcibly closes ffmpeg.</summary>
    public static void ForceKill() {
        if (ffmpeg != null && !ffmpeg.HasExited) ffmpeg.Kill();
        ffmpeg = null;
    }
}
