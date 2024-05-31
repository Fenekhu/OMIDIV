using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneRegistry : MonoBehaviour {
    private static Dictionary<int, string> scenes = new Dictionary<int, string>();
    public static IReadOnlyDictionary<int, string> Scenes { get { return scenes; } }

    public int StartupSceneIndex = -1;

    public static void RegisterScene(int index, string name) {
        scenes[index] = name;
    }

    void Start() {
        int backupSceneIndex = -1;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++) {
            string name = GetSceneName(i);
            if (!name.StartsWith('#')) { 
                RegisterScene(i, name);
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

    private bool IsValidStartupScene(int index) =>
        index > 0 && !SceneIsHiddenScene(index);

    private bool SceneIsHiddenScene(int index) =>
         GetSceneName(index).StartsWith('#');

    private string GetSceneName(int index) =>
        System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(index));
}
