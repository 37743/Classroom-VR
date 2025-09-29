/// <summary>
/// Appathon CSE-EJUST Challenge Summer 2025
/// 
/// Manages asynchronous scene loading with a loading screen and progress bar.
/// 
/// Usage:
/// - Assign the loadingScreen, previousScreen, and progressBar in the Unity Inspector.
/// - Call LoadScene(string sceneToLoad) to start loading a new scene asynchronously.
/// 
/// TODO:
/// - Add error handling for scene loading failures.
/// - Customize loading screen appearance and behavior as needed.
/// - Add support for cancelling the loading process.
/// - Add transiition effects between scenes.
/// - Optimize performance (some scenes load slower or faster than intended, and causes lag spikes)
/// </summary>
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ASyncLoader : MonoBehaviour
{
    [SerializeField] private Canvas loadingScreen;
    [SerializeField] private Canvas previousScreen;
    [SerializeField] private Slider progressBar;

    [SerializeField] private float fillSpeed = 0.8f;
    [SerializeField] private float holdAtFull = 0.25f;

    public void LoadScene(string sceneToLoad)
    {
        previousScreen.gameObject.SetActive(false);
        loadingScreen.gameObject.SetActive(true);

        StartCoroutine(LoadSceneAsync(sceneToLoad));
    }

    private IEnumerator LoadSceneAsync(string sceneToLoad)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneToLoad);
        asyncLoad.allowSceneActivation = false;

        float visualProgress = 0f;
        progressBar.value = 0f;

        while (asyncLoad.progress < 0.9f)
        {
            float target = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            visualProgress = Mathf.MoveTowards(visualProgress, target, fillSpeed * Time.unscaledDeltaTime);
            progressBar.value = visualProgress;
            yield return null;
        }

        while (visualProgress < 1f - 0.001f)
        {
            visualProgress = Mathf.MoveTowards(visualProgress, 1f, fillSpeed * Time.unscaledDeltaTime);
            progressBar.value = visualProgress;
            yield return null;
        }

        progressBar.value = 1f;

        yield return new WaitForSecondsRealtime(holdAtFull);

        asyncLoad.allowSceneActivation = true;
    }
}
