using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using ImGuiNET;
using UnityEngine.InputSystem;
using SFB;

public abstract class MidiRenderer : MonoBehaviour {
    private static RawMidi rawMidi;
    private static bool midiPathChanged;

    protected static Dictionary<uint, Mesh> NgonPrisms = new Dictionary<uint, Mesh>(); // <side count, mesh>
    protected static Dictionary<uint, Mesh> NgonPlanes = new Dictionary<uint, Mesh>(); // <side count, mesh>

    protected static FileInfo MidiPath;
    protected static CookedMidi Midi = new CookedMidi();
    protected static int MidiOffset;
    protected static int MidiOffsetPrev;

    protected static FileInfo AudioPath;
    protected static NAudioImporter AudioImporter;
    protected static AudioSource Sound;
    protected static int AudioOffset;
    protected static int AudioOffsetPrev;
    protected static float AudioDelay = 0;
    protected static bool IsPaused = false;

    protected static Camera MainCam;
    protected static FileInfo ImagePath;
    protected static RawImage BackgroundImage;
    protected static Camera BGCam;
    protected static Color BGColor;

    //protected static List<(int index, bool enabled)> TrackReorder = new List<(int, bool)>();
    protected static List<Color> TrackColors = new List<Color>();

    protected bool IsPlaying = false;
    protected bool AutoReload = true;
    protected bool NeedsReset = false;
    protected bool NeedsRestart = false;
    protected bool ReloadMidi = false;
    protected bool ReloadVisuals = false;
    protected bool ReloadAudio = false;
    protected bool ClearBGImage = false;

    protected long CurrentTick = 0;
    protected long CurrentTime = 0;
    protected uint CurrentTempo = 500000;

    protected static Mesh GetNSidedPrismMesh(uint sides) {
        if (NgonPrisms.TryGetValue(sides, out Mesh result)) return result;

        Mesh ret = new Mesh();

        Vector3[] verticies = new Vector3[sides * 6];
        Vector3[] normals = new Vector3[sides * 6];
        int[] indicies = new int[(sides-1) * 12]; // (sides * 6) + (sides - 2)*3*2 = (sides-1) * 12

        for (int i = 0; i < sides; i++) {
            Vector3 a = new Vector3( 0.5f, 0);
            Vector3 b = new Vector3( 0.5f, 0);
            Vector3 c = new Vector3(-0.5f, 0);
            Vector3 d = new Vector3(-0.5f, 0);

            float arc = Mathf.PI * 2 / sides;
            const float hpi = Mathf.PI / 2; // to rotate notes so 0 degrees is down
            float angle0 = arc * (i-0.5f) - hpi;
            float angle1 = arc * i - hpi;
            float angle2 = arc * (i+0.5f) - hpi;
            const float scl = 0.70710678118654752440084436210485f; // 0.5 * root2. root2 is to make squares full size, future of this variable tbd.
            Vector2 p0 = new Vector2(Mathf.Sin(angle0) * scl, Mathf.Cos(angle0) * scl);
            Vector3 norm = new Vector3(0, Mathf.Sin(angle1), Mathf.Cos(angle1));
            Vector2 p1 = new Vector2(Mathf.Sin(angle2) * scl, Mathf.Cos(angle2) * scl);

            (a.y, a.z) = (p0.x, p0.y);
            (c.y, c.z) = (p0.x, p0.y);
            (b.y, b.z) = (p1.x, p1.y);
            (d.y, d.z) = (p1.x, p1.y);

            verticies[i*6+0] = a;
            verticies[i*6+1] = b;
            verticies[i*6+2] = c;
            verticies[i*6+3] = d;
            verticies[i*6+4] = a;
            verticies[i*6+5] = c;

            for (int j = 0; j < 4; j++) normals[i*6+j] = norm;
            normals[i*6+4] = Vector3.right;
            normals[i*6+5] = Vector3.left;

            indicies[i*6+0] = i*6+0;
            indicies[i*6+1] = i*6+1;
            indicies[i*6+2] = i*6+2;
            indicies[i*6+3] = i*6+1;
            indicies[i*6+4] = i*6+3;
            indicies[i*6+5] = i*6+2;
        }

        for (int i = 0; i < sides - 2; i++) {
            indicies[sides*6 + i*6 + 2] = 4; // the winding of these has to be reversed
            indicies[sides*6 + i*6 + 1] = i*6+10; // (i+1)*6+4
            indicies[sides*6 + i*6 + 0] = i*6+16; // (i+2)*6+4
            indicies[sides*6 + i*6 + 3] = 5;
            indicies[sides*6 + i*6 + 4] = i*6+11; // (i+1)*6+5
            indicies[sides*6 + i*6 + 5] = i*6+17; // (i+2)*6+5
        }

        ret.vertices = verticies;
        ret.normals = normals;
        ret.triangles = indicies;
        NgonPrisms[sides] = ret;
        return ret;
    }

    protected static Mesh GetNSidedPlaneMesh(uint sides) {
        if (NgonPlanes.TryGetValue(sides, out Mesh result)) return result;

        Mesh ret = new Mesh();

        Vector3[] verticies = new Vector3[sides];
        Vector3[] normals = new Vector3[sides];
        int[] indicies = new int[(sides-2) * 3];

        for (int i = 0; i < sides; i++) {
            float arc = Mathf.PI * 2 / sides;
            const float hpi = Mathf.PI / 2; // to rotate notes so 0 degrees is down
            float angle0 = arc * (i-0.5f) - hpi;
            const float scl = 0.70710678118654752440084436210485f; // 0.5 * root2. root2 is to make squares full size, future of this variable tbd.

            verticies[i] = new Vector3(Mathf.Sin(angle0) * scl, Mathf.Cos(angle0) * scl);
            normals[i] = Vector3.back;
        }

        for (int i = 0; i < sides - 2; i++) {
            indicies[i*3+0] = 0;
            indicies[i*3+1] = i+1;
            indicies[i*3+2] = i+2;
        }

        ret.vertices = verticies;
        ret.normals = normals;
        ret.triangles = indicies;
        NgonPlanes[sides] = ret;
        return ret;
    }
    public void SetScene(System.Type type) {
        Destroy(gameObject.GetComponent<MidiRenderer>());
        gameObject.AddComponent(type);
    }

    protected virtual void Awake() {
        if (MainCam is null)
            MainCam = GameObject.Find("Main Camera").GetComponent<Camera>();
        if (BGCam is null)
            BGCam = GameObject.Find("BG cam").GetComponent<Camera>();
        if (BackgroundImage is null)
            BackgroundImage = GameObject.Find("Background Image").GetComponent<RawImage>();
        if (Sound is null)
            Sound = GetComponent<AudioSource>();
    }

    protected virtual void OnEnable() {
        ImGuiNET.Unity.ImGuiUn.Layout += DrawGUI;

        TrackColors.AddRange(new Color[] {
            new Color(1.00f, 0.00f, 0.00f, 1.0f),
            new Color(1.00f, 0.25f, 0.00f, 1.0f),
            new Color(1.00f, 0.50f, 0.00f, 1.0f),
            new Color(1.00f, 0.75f, 0.00f, 1.0f),
            new Color(1.00f, 1.00f, 0.00f, 1.0f),
            new Color(0.75f, 1.00f, 0.00f, 1.0f),
            new Color(0.50f, 1.00f, 0.00f, 1.0f),
            new Color(0.25f, 1.00f, 0.00f, 1.0f),
            new Color(0.00f, 1.00f, 0.00f, 1.0f),
            new Color(0.00f, 1.00f, 0.25f, 1.0f),
            new Color(0.00f, 1.00f, 0.50f, 1.0f),
            new Color(0.00f, 1.00f, 0.75f, 1.0f),
            new Color(0.00f, 1.00f, 1.00f, 1.0f),
            new Color(0.00f, 0.75f, 1.00f, 1.0f),
            new Color(0.00f, 0.50f, 1.00f, 1.0f),
            new Color(0.00f, 0.25f, 1.00f, 1.0f),
            new Color(0.00f, 0.00f, 1.00f, 1.0f),
            new Color(0.25f, 0.00f, 1.00f, 1.0f),
            new Color(0.50f, 0.00f, 1.00f, 1.0f),
            new Color(0.75f, 0.00f, 1.00f, 1.0f),
            new Color(1.00f, 0.00f, 1.00f, 1.0f),
            new Color(1.00f, 0.00f, 0.75f, 1.0f),
            new Color(1.00f, 0.00f, 0.50f, 1.0f),
            new Color(1.00f, 0.00f, 0.25f, 1.0f),
        });

        ReadConfig();
    }

    protected virtual void OnDisable() {
        ImGuiNET.Unity.ImGuiUn.Layout -= DrawGUI;
        WriteConfig();
    }

    // Start is called before the first frame update
    protected virtual void Start() {

    }

    protected virtual void FixedUpdate() {
        UpdateTPS();
        if (MidiOffset != MidiOffsetPrev) {
            int diff = 1000 * (MidiOffset - MidiOffsetPrev);
            double tickCount = math.floor(MicrosToTicks(CurrentTime, diff));
            MovePlay(tickCount);
            CurrentTime += diff;
            CurrentTick += (int)tickCount;
            MidiOffsetPrev = MidiOffset;
        }

        if (!IsPlaying) return;

        double dx = Midi.Header.fmt switch {
            EMidiDivisionFormat.TPQN => 1.0,
            EMidiDivisionFormat.SMPTE => Time.fixedDeltaTime / (-Midi.Header.smpte * Midi.Header.ticksPerFrame)
        };

        MovePlay(dx);

        CurrentTick++;
        CurrentTime += (long)(Time.fixedDeltaTime * 1e6d);
    }

    // Update is called once per frame
    protected virtual void Update() {
        if (Keyboard.current.spaceKey.wasPressedThisFrame) {
            IsPlaying = !IsPlaying;
            if (!IsPlaying && Sound.isPlaying) {
                Sound.Pause();
                IsPaused = true;
            } else if (IsPlaying && !Sound.isPlaying) {
                if (IsPaused || AudioDelay <= 0) {
                    Sound.Play();
                } else {
                    Sound.PlayDelayed(AudioDelay);
                }
                IsPaused = false;
            }
        }
        if (Keyboard.current.rKey.wasPressedThisFrame) {
            IsPlaying = false;
            Sound.Stop();
            NeedsRestart = true;
        }
        if (Keyboard.current.f5Key.wasPressedThisFrame) {
            IsPlaying = false;
            Sound.Stop();
            NeedsReset = true;
        }
        if (Keyboard.current.f6Key.wasPressedThisFrame) {
            if (Keyboard.current.shiftKey.isPressed) {
                ReloadMidi = true;
            } else {
                ReloadVisuals = true;
            }
        }
        if (Keyboard.current.f11Key.wasPressedThisFrame) {
            Screen.fullScreen = !Screen.fullScreen;
        }

        if (NeedsReset) {
            Reset_();
            ReloadMidi = false;
            ReloadVisuals = false;
            ReloadAudio = false;
            NeedsRestart = false;
            NeedsReset = false;
        }
        if (NeedsRestart) {
            Restart();
            NeedsRestart = false;
        }
        if (ReloadMidi) {
            InitMidi();
            ReloadMidi = false;
            ReloadVisuals = false;
        }
        if (ReloadVisuals) {
            ClearVisuals();
            InitVisuals();
            ReloadVisuals = false;
        }
        if (ReloadAudio) {
            LoadAudio();
            SeekAudio(AudioOffset / 1000f);
        }
        if (AudioOffset != AudioOffsetPrev) {
            float diff = (AudioOffset - AudioOffsetPrev)/1000f;
            SeekAudio(Sound.time + diff);
            AudioOffsetPrev = AudioOffset;
        }
        if (ImagePath is not null) {
            byte[] bytes = File.ReadAllBytes(ImagePath.FullName);
            Texture2D tex = Texture2D.blackTexture;
            bool result = tex.LoadImage(bytes);
            tex.filterMode = FilterMode.Bilinear;
            if (result) {
                int width = tex.width;
                int height = tex.height;
                int max = math.max(width, height);
                BackgroundImage.transform.localScale = new Vector3((float)width / max, (float)height / max, 1.0f);
                BackgroundImage.texture = tex;
                BackgroundImage.enabled = true;
            }
            ImagePath = null;
        }
        if (ClearBGImage) {
            BackgroundImage.transform.localScale = Vector3.one;
            BackgroundImage.texture = null;
            BackgroundImage.enabled = false;
        }
    }

    protected virtual void OnDestroy() {
        ClearVisuals();
    }

    protected virtual void DrawGUI() {
        if (!ImGuiManager.IsEnabled) return;
        ImGuiManager.DrawGUI();

        if (ImGuiManager.IsDebugEnabled) {
            if (ImGui.Begin("debug")) {
                ImGui.Text(string.Format("time: {0:d}", CurrentTime));
                var nextTempo = Midi.TempoMap.GT(CurrentTime) ?? (0, 0);
                ImGui.Text(string.Format("next tempo: {0:d} ({1:F2})", nextTempo.tempoMicros, MidiUtil.TempoBPM(nextTempo.tempoMicros)));
                ImGui.Text(string.Format("at: {0:d}", nextTempo.timeMicros));
            }
            ImGui.End();
        }

        bool bOpenMidi = false;
        bool bOpenAudio = false;
        bool bOpenImage = false;
        bool bOpenConfig = false;
        bool bSaveConfig = false;
        bool bSaveConfigAs = false;

        if (ImGui.BeginMainMenuBar()) {
            if (ImGui.BeginMenu("File")) {
                bOpenMidi = ImGui.MenuItem("Open Midi");
                bOpenAudio = ImGui.MenuItem("Open Audio");
                bOpenConfig = ImGui.MenuItem("Open Config");
                bSaveConfig = ImGui.MenuItem("Save Config");
                bSaveConfigAs = ImGui.MenuItem("Save Config As");
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("View")) {
                if (ImGui.MenuItem("Standard 3D", this is not Standard3D)) SetScene(typeof(Standard3D));
                if (ImGui.MenuItem("Circle 3D", this is not Circle3D)) SetScene(typeof(Circle3D));
                if (ImGui.MenuItem("Circle 2D", this is not Circle2D)) SetScene(typeof(Circle2D));
                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }

        if (ImGui.Begin("Audio Controls")) {
            ImGui.PushItemWidth(128.0f);
            float v = Sound.volume;
            if (ImGui.SliderFloat("Volume", ref v, 0.0f, 1.0f)) {
                Sound.volume = v;
            }
            ImGui.InputInt("Offset (ms)", ref AudioOffset);
            ImGui.PopItemWidth();
        }
        ImGui.End();

        if (ImGui.Begin("MIDI Controls")) {
            ImGui.SetNextItemWidth(128.0f);
            ImGui.InputInt("Offset (ms)", ref MidiOffset);
        }
        ImGui.End();

        if (ImGui.Begin("Misc Controls")) {
            ImGui.Checkbox("Auto-apply certain changes", ref AutoReload);
            Vector4 bgColor = BGColor;
            if (ImGui.ColorEdit4("Background Color", ref bgColor, ImGuiColorEditFlags.NoAlpha)) {
                BGColor = bgColor;
                BGCam.backgroundColor = BGColor;
            }
            ImGui.Text("Background Image"); ImGui.SameLine();
            bOpenImage = ImGui.Button("Open##bgImg"); ImGui.SameLine();
            ClearBGImage = ImGui.Button("Clear##bgImg");
            if (BackgroundImage.texture != null && ImGui.TreeNode("Image Options##bgImg")) {
                ImGui.PushItemWidth(128.0f);
                ImGuiManager.RawImageControlInner(BackgroundImage);
                ImGui.PopItemWidth();
                if (ImGui.Button("Stretch to window")) {
                    var scale = BackgroundImage.transform.parent.localScale;
                    BackgroundImage.transform.localScale = new Vector3(scale.x, scale.y, 1);
                }
                Vector4 color = BackgroundImage.color;
                if (ImGui.ColorEdit4("Image Tint", ref color, ImGuiColorEditFlags.NoInputs)) {
                    BackgroundImage.color = color;
                }
                ImGui.TreePop();
            }
        }
        ImGui.End();

        if (ImGui.Begin("Keybinds")) {
            ImGui.Text("Space: Play/Pause");
            ImGui.Text("R: Restart");
            ImGui.Text("F1: Toggle GUI");
            ImGui.Text("F3: Toggle debug info");
            ImGui.Text("F5: Reload Everything");
            ImGui.Text("F6: Refresh MIDI");
            ImGui.Text("F11: Toggle Fullscreen");
        }
        ImGui.End();

        if (bOpenMidi) {
            ExtensionFilter[] exts = {new ExtensionFilter("MIDI", "mid", "midi")};
            string[] res = StandaloneFileBrowser.OpenFilePanel("Open MIDI", "", exts, false);
            if (res.Length > 0) {
                MidiPath = new FileInfo(res[0]);
                midiPathChanged = true;
                IsPlaying = false;
                ReloadMidi = true;
                NeedsRestart = true;
            }
        }

        if (bOpenAudio) {
            string[] res = StandaloneFileBrowser.OpenFilePanel("Open Audio", "", "", false);
            if (res.Length > 0) {
                AudioPath = new FileInfo(res[0]);
                IsPlaying = false;
                ReloadAudio = true;
                NeedsRestart = true;
            }
        }

        if (bOpenImage) {
            ExtensionFilter[] exts = {new ExtensionFilter("Image", "png", "jpg", "jpeg"), new ExtensionFilter("PNG", "png"), new ExtensionFilter("JPEG", "jpg", "jpeg")};
            string[] res = StandaloneFileBrowser.OpenFilePanel("Open Image", "", exts, false);
            if (res.Length > 0) ImagePath = new FileInfo(res[0]);
        }

        if (bOpenConfig) {
            Config.Open();
            ReadConfig();
        }

        if (bSaveConfig) {
            WriteConfig();
            Config.Save();
        }

        if (bSaveConfigAs) {
            WriteConfig();
            Config.SaveAs();
        }
    }

    private void SeekAudio(float time) {
        AudioDelay = -time;
        if (time < 0) {
            Sound.time = 0;
            if (Sound.isPlaying) {
                Sound.PlayDelayed(AudioDelay);
            }
        } else {
            Sound.time = time;
        }
    }
    protected virtual void Restart() {
        CurrentTime = MidiOffset * 1000;
        CurrentTick = (long)MicrosToTicks(0, CurrentTime);
        MovePlay(CurrentTick);
        SeekAudio(AudioOffset / 1000f);
        IsPaused = false;
    }

    protected virtual void Reset_() {
        InitMidi();
        LoadAudio();
        Restart();
    }

    protected void LoadAudio() {
        if (AudioImporter is null) {
            AudioImporter = gameObject.AddComponent<NAudioImporter>();
            AudioImporter.Loaded += (AudioClip clip) => { Sound.clip = clip; };
        }
        AudioImporter.Import(AudioPath.FullName);
        if (AudioImporter.isError) {
            Debug.Log(AudioImporter.error);
        }
        ReloadAudio = false;
    }

    protected abstract void InitVisuals();

    protected abstract void ClearVisuals();

    protected abstract void MovePlay(double ticks);

    protected virtual void WriteConfig() {
        Config.Set("bg.color", BGColor);
    }

    protected virtual void ReadConfig() {
        if (BGCam is null) {
            BGCam = GameObject.Find("BG cam").GetComponent<Camera>();
        }

        BGColor = Config.Get<Color>("bg.color") ?? Color.black;
        if (BGCam != null) BGCam.backgroundColor = BGColor;

        ReloadVisuals = AutoReload;
    }

    private void InitMidi() {
        CurrentTick = 0;
        CurrentTempo = 500000;
        ClearVisuals();

        if (MidiPath.Length == 0) return;
        if (midiPathChanged) {
            if (rawMidi is null) rawMidi = new RawMidi();
            rawMidi.Open(MidiPath);

            //using (StreamWriter sw = new StreamWriter(new FileStream("midi_dump.txt", FileMode.Create))) rawMidi.DebugPrint(sw);

            midiPathChanged = false;
        }

        if (Midi is null) Midi = new CookedMidi();
        Midi.Cook(rawMidi);

        UpdateTPS();
        InitVisuals();
    }

    private void UpdateTPS() {
        uint newTempo = Midi.TempoMap[CurrentTime].tempoMicros;
        if (newTempo != CurrentTempo) {
            CurrentTempo = newTempo;

            switch (Midi.Header.fmt) {
            case EMidiDivisionFormat.TPQN:
                Time.fixedDeltaTime = CurrentTempo / (Midi.Header.ticksPerQuarter * 1e6f);
                break;
            case EMidiDivisionFormat.SMPTE:
                Time.fixedDeltaTime = 1.0f / (-Midi.Header.smpte * Midi.Header.ticksPerFrame);
                break;
            }
        }
    }

    protected double MicrosToTicks(long start, long micros) {
        double ret = 0;
        var tempoMap = Midi.TempoMap;

        while (micros != 0) {
            long timeSpentInTempo = 0;
            uint _tempo = tempoMap[start].tempoMicros;

            if (micros < 0) {
                int index = tempoMap.GTEIndex(start).GetValueOrDefault(tempoMap.Count);
                if (index == 0) {
                    timeSpentInTempo = micros;
                } else {
                    index--;
                    var item = tempoMap.GetAtIndex(index);
                    if (item.timeMicros < (start + micros)) {
                        timeSpentInTempo = micros;
                    } else {
                        timeSpentInTempo = item.timeMicros - start;
                    }
                }
            } else {
                int? index = tempoMap.GTIndex(start);
                if (!index.HasValue || tempoMap.GetAtIndex(index.Value).timeMicros > (start + micros)) {
                    timeSpentInTempo = micros;
                } else {
                    timeSpentInTempo = tempoMap.GetAtIndex(index.Value).timeMicros - start;
                }
            }

            start += timeSpentInTempo;
            micros -= timeSpentInTempo;

            switch (Midi.Header.fmt) {
            case EMidiDivisionFormat.TPQN:
                ret += timeSpentInTempo * Midi.Header.ticksPerQuarter / _tempo;
                break;
            case EMidiDivisionFormat.SMPTE:
                ret += timeSpentInTempo / (1e6d * -Midi.Header.smpte * Midi.Header.ticksPerFrame);
                break;
            }
        }

        return ret;
    }
}
