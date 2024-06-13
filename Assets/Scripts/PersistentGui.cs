using ImGuiNET;
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Draws GUI elements that will always be present.
/// </summary>
public class PersistentGui : MonoBehaviour {

    /// <summary>
    /// A class for counting the number of times <see cref="Update"/> is called per second and averaging it.
    /// </summary>
    private class XPSCounter {
        private readonly float[] spxArr;
        private int index = 0;
        private Stopwatch clock;

        /// <summary>Average amount of seconds between <see cref="Update"/> calls.</summary>
        public float avgSpx { get { return spxArr.Sum() / spxArr.Length; } }

        /// <summary>Average <see cref="Update"/> calls per second.</summary>
        public float avgXps { get { return 1 / avgSpx; } }

        /// <param name="count">The number of values to average over.</param>
        public XPSCounter(int count) {
            spxArr = new float[count];
            clock = new Stopwatch();
        }

        /// <summary>Starts (or restarts) the clock.</summary>
        public void Start() => clock.Restart();

        /// <summary>Adds the time elapsed since <see cref="Start"/> or <see cref="Update"/> to the average.</summary>
        public void Update() {
            spxArr[index] = (float)((double)clock.ElapsedTicks / Stopwatch.Frequency); // lose less precision before the division
            clock.Restart();
            index = (index+1) % spxArr.Length;
        }
    }

    private XPSCounter tpsCounter = new XPSCounter(64);
    private XPSCounter upsCounter = new XPSCounter(64);
    private XPSCounter fpsCounter = new XPSCounter(64);
    private float avgTps, avgUps, avgFps; // display values, which update every 0.5 second instead of every frame.
    private float lastXPSRefresh = 0.0f;

    private void OnEnable() {
        ImGuiUn.Layout += DrawAlwaysDebug;
        ImGuiManager.DrawMainMenuItems += DrawMainMenuItems;
        ImGuiManager.AddMainMenuTab("File");
        ImGuiManager.AddMainMenuTab("View");
        ImGuiManager.AddMainMenuTab("About");
    }

    private void OnDisable() {
        ImGuiUn.Layout -= DrawAlwaysDebug;
        ImGuiManager.DrawMainMenuItems -= DrawMainMenuItems;
        ImGuiManager.RemoveMainMenuTab("File");
        ImGuiManager.RemoveMainMenuTab("View");
        ImGuiManager.RemoveMainMenuTab("About");
    }

    private void Start() {
        tpsCounter.Start();
        upsCounter.Start();
        fpsCounter.Start();
        StartCoroutine(FrameTimer());
#if !UNITY_EDITOR
        StartCoroutine(UpdateChecker.Check());
#endif
    }

    private void Update() {
        upsCounter.Update();
    }

    private void FixedUpdate() {
        tpsCounter.Update();
    }

    private IEnumerator FrameTimer() {
        while (true) {
            fpsCounter.Update();
            yield return new WaitForEndOfFrame();
        }
    }

    private void DrawAlwaysDebug() {
        // update xps displays
        if (Time.realtimeSinceStartup - lastXPSRefresh > 0.5f) {
            avgTps = tpsCounter.avgXps;
            avgUps = upsCounter.avgXps;
            avgFps = fpsCounter.avgXps;
            lastXPSRefresh = Time.realtimeSinceStartup;
        }

        if (ImGuiManager.IsDebugEnabled) {
            // draw xps displays
            float nww = ImGui.GetFontSize() * 18;
            float nwh = ImGui.GetFontSize() * 6;
            ImGui.SetNextWindowSize(new Vector2(nww, nwh));
            ImGui.SetNextWindowPos(new Vector2(Screen.width - nww, Screen.height - nwh));
            if (ImGui.Begin("fps", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs)) {
                string tpsStr = string.Format("{0:F2} tps ({1:F2})", avgTps, 1/Time.fixedDeltaTime);
                ImGui.Text(tpsStr);
                ImGui.Text(string.Format("{0:F2} ups", avgUps).PadLeft(tpsStr.Length));
                ImGui.Text(string.Format("{0:F2} fps", avgFps).PadLeft(tpsStr.Length));
            }
            ImGui.End();
        }
    }

    private void DrawGUI() {
        
    }

    private void DrawMainMenuItems(string menu) {
        switch (menu) {
        case "File":
            if (ImGui.MenuItem("Quit")) Application.Quit();
            ImGui.Separator();
            break;
        case "View":
            DrawSceneFolderUI(SceneRegistry.SceneDir);
            break;
        case "About":
            if (ImGui.BeginMenu("Version & Updates")) {
                UpdateChecker.DrawGUI();
                ImGui.EndMenu();
            }
            break;
        }
    }

    private void DrawSceneFolderUI(SceneRegistry.Folder folder) {
        foreach (SceneRegistry.Folder f in folder.subfolders) {
            if (ImGui.BeginMenu(f.name)) {
                DrawSceneFolderUI(f);
                ImGui.EndMenu();
            }
        }
        ImGui.Separator();
        foreach (var kvp in folder.scenes) {
            bool isCurrent = kvp.buildIndex == SceneManager.GetActiveScene().buildIndex;
            if (ImGui.MenuItem(kvp.name, null, isCurrent, !isCurrent))
                StartCoroutine(CoroutineUtils.LoadScene(kvp.buildIndex));
        }
    }
}


class UpdateChecker {
    class Release {
        public string ver = null;
        public string url = null;
        public Release() { }
        public Release(string ver, string url) { this.ver = ver; this.url = url; }
    }

    private static string currentVersion = Application.version;
    private static Release latestAlpha = null;
    private static Release latestBeta = null;
    private static Release latestRelease = null;

    private static char currentChannel => currentVersion.ToLower().Last();
    private static string currentChannelName => currentChannel switch {
        'a' => "Alpha",
        'b' => "Beta",
        'r' => "Release",
        _ => "Unknown '"+currentChannel+"'",
    };

    public static void DrawGUI() {
        ImGui.Text("Current Version: " + currentVersion);
        ImGui.Text("Current Channel: " + currentChannelName);

        if (latestRelease != null) {
            if (ImGui.MenuItem("Get Latest Release: "+latestRelease.ver))
                Application.OpenURL(latestRelease.url);
        } else {
            ImGui.TextDisabled("Couldn't find a release");
        }

        if (latestBeta != null) {
            if (ImGui.MenuItem("Get Latest Beta: "+latestBeta.ver))
                Application.OpenURL(latestBeta.url);
        } else {
            ImGui.TextDisabled("Couldn't find a beta release");
        }

        if (latestAlpha != null) {
            if (ImGui.MenuItem("Get Latest Alpha: "+latestAlpha.ver))
                Application.OpenURL(latestAlpha.url);
        } else {
            ImGui.TextDisabled("Couldn't find an alpha release");
        }
    }

    public static IEnumerator Check() {
        latestAlpha = null;
        latestBeta = null;
        latestRelease = null;

        using (UnityWebRequest request = UnityWebRequest.Get("https://api.github.com/repos/TheGoldenProof/OMIDIV/releases")) {
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
            yield return request.SendWebRequest();

            switch (request.result) {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.LogError("UpdateCheck Error: " + request.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.LogError("UpdateCheck HTTP Error: " + request.error);
                break;

            case UnityWebRequest.Result.Success:
                string jsonStr = request.downloadHandler.text;
                ParseResponse(jsonStr);
                break;
            }
        }
    }

    private static void ParseResponse(string jsonResponse) {
        var json = Utilities.SimpleJSON.JSONArray.Parse(jsonResponse);

        foreach (var release in json.Children) {
            string tag = release["tag_name"].Value.ToLower();
            if (tag.EndsWith('a') && latestAlpha == null) {
                latestAlpha = new Release(tag, release["html_url"]);
            } else if (tag.EndsWith('b') && latestBeta == null) {
                latestBeta = new Release(tag, release["html_url"]);
            } else if (tag.EndsWith('r') && latestRelease == null) {
                latestRelease = new Release(tag, release["html_url"]);
            }
            
            // early exit if we found the latest of all of them.
            if (latestAlpha != null && latestBeta != null && latestRelease != null)
                break;
        }

        // if the latest release is newer than the latest beta, set the latest beta to the newest release.
        if (string.Compare(latestBeta.ver, latestRelease.ver, true) < 0) {
            latestBeta = latestRelease;
        }
        // the same but for alpha.
        if (string.Compare(latestAlpha.ver, latestBeta.ver) < 0) {
            latestAlpha = latestBeta;
        }
    }

}