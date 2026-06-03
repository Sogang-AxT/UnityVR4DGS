using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System.Collections;
using GaussianSplatting.Runtime;

public class Main_Script : MonoBehaviour {
    public GaussianSplatPlayer player;
    [Space(10f)]
    public Volume v;
    public bool isLoopingScene;
    // public GameObject BG2;

    private float previousFrameTime;
    private int loopCount;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private bool isTransitioning = false;
    
    
    void Start() {
        loopCount = 0;
        
        if (v.profile.TryGet(out vignette) && v.profile.TryGet(out colorAdjustments)) 
        {
            vignette.intensity.value = 1f;
            colorAdjustments.postExposure.value = -10f;

            StartCoroutine(FadeInEffect(1.0f));
        }
    }

    void Update() {
        OnLoadNextSceneAfterLoop();
        
        if (Input.GetKeyDown(KeyCode.Space) && !isTransitioning)
        {
            StartCoroutine(LoadNextSceneAfterDelay(1.0f));
        }
    }
    
    private void OnLoadNextSceneAfterLoop() {
        var currentFrameTime = player.normalizedTime;

        if (currentFrameTime < previousFrameTime) {
            if (loopCount >= 2) {
                if (!isLoopingScene) {
                    player.Stop();
                    loopCount = 0;
                    return;
                }
                
                StartCoroutine(LoadNextSceneAfterDelay(1.0f));
                loopCount = 0;
            }

            loopCount++;
        }
        
        previousFrameTime = currentFrameTime;
    }
    
    IEnumerator FadeInEffect(float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / delay;

            if (vignette != null) vignette.intensity.value = Mathf.Lerp(1f, 0f, t);
            if (colorAdjustments != null) colorAdjustments.postExposure.value = Mathf.Lerp(-10f, 0f, t);

            yield return null;
        }
        if (vignette != null) vignette.intensity.value = 0f;
        if (colorAdjustments != null) colorAdjustments.postExposure.value = 0f;
    }
    
    IEnumerator LoadNextSceneAfterDelay(float delay)
    {
        isTransitioning = true;
        float elapsed = 0f;

        while (elapsed < delay)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / delay;
            
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(0f, 1f, t);
            if (colorAdjustments != null) colorAdjustments.postExposure.value = Mathf.Lerp(0f, -10f, t);

            yield return null;
        }
        
        if (vignette != null) vignette.intensity.value = 1f;
        if (colorAdjustments != null) colorAdjustments.postExposure.value = -10f;

        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings) 
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else 
        {
            isTransitioning = false;
        }
    }
}