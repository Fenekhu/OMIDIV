using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// thanks https://github.com/mbitzos/devblog-code-examples/blob/main/programmatic-animation-using-coroutines/CoroutineUtils.cs
public static class CoroutineUtils {

    public static IEnumerator Lerp(float duration, Action<float> action, bool inverse = false, bool smooth = false, AnimationCurve curve = null) {
        float time = 0;
        Func<float, float> func = t => t; // linear
        if(smooth) func = t => Mathf.SmoothStep(0, 1, t);
        if(curve != null) func = t => curve.Evaluate(t);

        while(time < duration) {
            float delta = MidiScene.DeltaTime;
            float t = (time + delta > duration) ? 1 : (time / duration);
            if(inverse) t = 1 - t;
            action(func(t));
            time += delta;
            yield return null;
        }
        action(func(inverse ? 0 : 1));
    }

    public static Coroutine Lerp(MonoBehaviour obj, float duration, Action<float> action) {
        return obj.StartCoroutine(Lerp(duration, action));
    }

    public static IEnumerator LoadScene(int sceneId, bool unloadCurrent = true) {
        int unload = SceneManager.GetActiveScene().buildIndex;
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneId);
        while(!op.isDone) yield return null;
        if(unloadCurrent) {
            op = SceneManager.UnloadSceneAsync(unload);
            while(!op.isDone) yield return null;
        }
    }

    public static IEnumerator DoAsync(Func<AsyncOperation> func) {
        AsyncOperation op = func();
        while(!op.isDone) yield return null;
    }

}
