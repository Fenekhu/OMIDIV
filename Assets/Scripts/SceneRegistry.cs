using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/**
 * <summary>
 * Holds a list of Midi visualization scenes.
 * </summary>
 * <remarks>
 * On startup, it reads the list of scenes in the build settings and adds all scenes that don't start with a '#'.<br/>
 * Scenes can be added with <see cref="RegisterScene"/> and a read-only list returned with <see cref="Scenes"/>
 * </remarks>
 */

public class SceneRegistry : MonoBehaviour {

    /// <summary>
    /// Represents a directory structure of scenes.
    /// </summary>
    public class Folder {
        public string name;
        public List<Folder> subfolders = new();
        public List<(int buildIndex, string name)> scenes = new();

        public Folder(string name) { this.name = name; }

        public void Add(string path, int buildIndex, string sceneName) {
            if (path.Length == 0) {
                scenes.Add((buildIndex, sceneName));
            } else {
                int nextSlash = path.IndexOf('/');
                string subfolder = path[..nextSlash];
                string newPath = path[(nextSlash+1)..];
                foreach (Folder f in subfolders) {
                    if (f.name == subfolder) {
                        f.Add(newPath, buildIndex, sceneName);
                        return;
                    }
                }
                Folder newFolder = new Folder(subfolder);
                newFolder.Add(newPath, buildIndex, sceneName);
                subfolders.Add(newFolder);
            }
        }
    }

    /// <summary>The available visualization scenes as a directory structure.</summary>
    public static Folder SceneDir = new("");

    private static Dictionary<int, string> scenes = new Dictionary<int, string>();

    /// <summary>Maps build index to display name</summary>
    public static IReadOnlyDictionary<int, string> Scenes { get { return scenes; } }

    /**
     * <summary>
     * The build index of the scene that will be loaded at startup.
     * </summary>
     * <remarks>
     * Change this to change which scene opens first by default.<br/>
     * If the value is invalid (-1, anything < 0, or a scene that starts with '#'),
     * it will be replaced with the first valid scene's index by the Start function.
     * </remarks>
     */
    public static int StartupSceneIndex { get; set; } = -1;

    /**
     * <summary>Allows <see cref="StartupSceneIndex"/> to be overriden from the Unity editor.</summary>
     */
    public int StartupSceneIndex_ = -1;

    /**
     * <summary>
     * Adds a midi visualization scene.
     * </summary>
     * <param name="index">The build index of the scene.</param>
     * <param name="name">The display name of the scene.</param> 
     * <remarks>
     * It is possible to add *any* scene, including ones that do not have any <see cref="MidiScene"/> or ImGui component, 
     * but they will still be added to the View list.
     * </remarks>
     */
    public static void RegisterScene(int index, string name, string path = "/") {
        scenes[index] = name;
        SceneDir.Add(path, index, name);
    }

    void Start() {
        //Debug.Log(GetScenePath(1));

        if (StartupSceneIndex_ < 0) StartupSceneIndex = StartupSceneIndex_;

        // the startup scene if StartupSceneIndex is found to be invalid
        int backupSceneIndex = -1;

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++) {
            string name = GetSceneName(i);
            string path = GetScenePath(i);
            if (!name.StartsWith('#')) { 
                RegisterScene(i, name, path);
                if (backupSceneIndex < 0) backupSceneIndex = i;
            }
        }

        if (!IsValidStartupScene(StartupSceneIndex)) {
            StartupSceneIndex = backupSceneIndex;
        }

        if (!IsValidStartupScene(StartupSceneIndex)) {
            Debug.Log("No valid scenes found! No initial scene can be loaded!");
        } else {
            StartCoroutine(CoroutineUtils.LoadScene(StartupSceneIndex));
        }
    }

    private static bool IsValidStartupScene(int index) =>
        index > 0 && !SceneIsHiddenScene(index);

    private static bool SceneIsHiddenScene(int index) =>
         GetSceneName(index).StartsWith('#');

    private static string GetScenePath(int index) {
        string path = SceneUtility.GetScenePathByBuildIndex(index);
        path = path[14..(path.LastIndexOf('/')+1)];
        return path;
    }

    private static string GetSceneName(int index) =>
        System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(index));
}
