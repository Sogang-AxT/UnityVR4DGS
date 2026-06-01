using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class Ending_Script : MonoBehaviour
{
    public Volume v; // 인스펙터에서 마지막 씬의 Global Volume을 연결하세요.

    private Vignette vignette;
    private ColorAdjustments colorAdjustments;

    void Start()
    {
        // 1. 볼륨 프로필에서 효과 가져오기
        if (v != null && v.profile.TryGet(out vignette) && v.profile.TryGet(out colorAdjustments))
        {
            // 2. 시작 시 초기값 설정 (어두운 상태)
            vignette.intensity.value = 1f;
            colorAdjustments.postExposure.value = -10f;

            // 3. 1초에 걸쳐 서서히 밝아지는 효과만 실행
            StartCoroutine(FadeInEffect(1.0f));
        }
    }

    IEnumerator FadeInEffect(float delay)
    {
        float elapsed = 0f;

        while (elapsed < delay)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / delay;

            // Intensity: 1 -> 0, Exposure: -10 -> 0
            if (vignette != null)
                vignette.intensity.value = Mathf.Lerp(1f, 0f, t);

            if (colorAdjustments != null)
                colorAdjustments.postExposure.value = Mathf.Lerp(-10f, 0f, t);

            yield return null;
        }

        // 최종 값 확정
        if (vignette != null) vignette.intensity.value = 0f;
        if (colorAdjustments != null) colorAdjustments.postExposure.value = 0f;
    }
}