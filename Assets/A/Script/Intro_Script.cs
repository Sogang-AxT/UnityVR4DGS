using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Intro_Script : MonoBehaviour
{
    private bool isTransitioning = false;
    public Volume v;

    private Vignette vignette;
    private ColorAdjustments colorAdjustments;

    void Start()
    {
        if (v.profile.TryGet(out vignette))
        {
            vignette.intensity.value = 0f;
        }

        if (v.profile.TryGet(out colorAdjustments))
        {
            colorAdjustments.postExposure.value = 0f;
        }
        StartCoroutine(FadeInEffect(1.0f));
    }

    void Update()
    {

    }

    // 밝아지는 효과 (씬 시작 시)
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

    public IEnumerator LoadNextSceneAfterDelay(float delay)
    {
        isTransitioning = true;
        Debug.Log(delay + "초 동안 효과가 적용되며 씬이 전환됩니다.");

        float elapsed = 0f;

        // 1초(delay) 동안 반복 수행
        while (elapsed < delay)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / delay; // 0에서 1까지 증가하는 비율

            // 1. Vignette Intensity: 0 -> 1
            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Lerp(0f, 1f, t);
            }

            // 2. Post Exposure: 0 -> -10
            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.value = Mathf.Lerp(0f, -10f, t);
            }

            yield return null; // 다음 프레임까지 대기
        }

        // 수치 최종 확인 (보정)
        if (vignette != null) vignette.intensity.value = 1f;
        if (colorAdjustments != null) colorAdjustments.postExposure.value = -10f;

        // 씬 전환
        // 씬 전환 부분만 교체
        string[] mainScenes = { "Main 1", "Main 2", "Main 3" };
        string randomScene = mainScenes[Random.Range(0, mainScenes.Length)];
        SceneManager.LoadScene(randomScene);
    }
}