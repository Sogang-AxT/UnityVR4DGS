using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System.Collections;

public class Ending_Script : MonoBehaviour
{
    public Volume v;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;

    void Start()
    {
        if (v != null && v.profile.TryGet(out vignette) && v.profile.TryGet(out colorAdjustments))
        {
            vignette.intensity.value = 1f;
            colorAdjustments.postExposure.value = -10f;
            StartCoroutine(FadeInEffect(1.0f));
        }
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

    // 어두워지며 Intro 씬으로 전환 (외부에서 호출)
    public IEnumerator FadeOutToIntro(float delay)
    {
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

        SceneManager.LoadScene("Intro");
    }
}