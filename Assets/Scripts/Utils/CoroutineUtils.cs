using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// thanks https://github.com/mbitzos/devblog-code-examples/blob/main/programmatic-animation-using-coroutines/CoroutineUtils.cs
public static class CoroutineUtils {

    /// <summary>
    /// Creates a coroutine that interpolates a float from 0-1 over a <paramref name="duration"/> and passes it to <paramref name="action"/>.
    /// </summary>
    /// <param name="duration">The duration to interpolate over.</param>
    /// <param name="action">The function to call with the interpolation alpha.</param>
    /// <param name="inverse">Interpolate from 1-0 instead of 0-1.</param>
    /// <param name="smooth">Use <c>SmoothStep</c> interpolation curve instead of linear.</param>
    /// <param name="curve">If not null, will interpolate with this instead of linear or smooth.</param>
    public static IEnumerator Lerp(float duration, Action<float> action, bool inverse = false, bool smooth = false, AnimationCurve curve = null) {
        float time = 0;
        Func<float, float> func = t => t; // linear
        if(smooth) func = t => Mathf.SmoothStep(0, 1, t);
        if(curve != null) func = t => curve.Evaluate(t);

        while(time < duration) {
            float delta = (float)SceneController.FrameDeltaTime;
            float t = (time + delta > duration) ? 1 : (time / duration);
            if(inverse) t = 1 - t;
            action(func(t));
            time += delta;
            yield return null;
        }
        action(func(inverse ? 0 : 1));
    }

    /// <summary>
    /// Loads a scene asynchronously using a coroutine.
    /// </summary>
    /// <param name="sceneId">The build index of the scene to load.</param>
    /// <param name="unloadCurrent">Whether to unload the current scene after loading the next.</param>
    /// <returns></returns>
    public static IEnumerator LoadScene(int sceneId, bool unloadCurrent = true) {
        var unload = SceneManager.GetActiveScene().buildIndex;
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneId, unloadCurrent ? LoadSceneMode.Single : LoadSceneMode.Additive);
        while(!op.isDone) yield return null;
    }

    /// <summary>
    /// Performs an operation asynchronously and wraps it in a coroutine.
    /// </summary>
    /// <param name="func"></param>
    /// <returns></returns>
    public static IEnumerator DoAsync(Func<AsyncOperation> func) {
        AsyncOperation op = func();
        while(!op.isDone) yield return null;
    }

}
