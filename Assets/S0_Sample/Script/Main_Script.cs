using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System.Collections;

public class Main_Script : MonoBehaviour
{
    public Volume v; // 인스펙터에서 Global Volume 연결
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;

    public GameObject BG2;

    private bool isTransitioning = false;

    void Start()
    {
        // 1. 볼륨 프로필에서 효과 가져오기
        if (v.profile.TryGet(out vignette) && v.profile.TryGet(out colorAdjustments))
        {
            // 시작 시 어두운 상태로 설정 (Intensity 1, Exposure -10)
            vignette.intensity.value = 1f;
            colorAdjustments.postExposure.value = -10f;

            // 1초 동안 밝아지는 효과 실행
            StartCoroutine(FadeInEffect(1.0f));
        }
    }

    void Update()
    {
        // 스페이스바를 누르고, 현재 전환 중이 아닐 때만 실행
        // (New Input System 에러 발생 시 Keyboard.current.spaceKey.wasPressedThisFrame 으로 교체)
        if (Input.GetKeyDown(KeyCode.Space) && !isTransitioning)
        {
            StartCoroutine(LoadNextSceneAfterDelay(1.0f));
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            BG2.SetActive(!BG2.activeSelf);
        }
    }

    // 씬 시작 시 밝아지는 코루틴 (Fade In)
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

    // 스페이스바 입력 시 어두워지며 다음 씬으로 이동하는 코루틴 (Fade Out)
    IEnumerator LoadNextSceneAfterDelay(float delay)
    {
        isTransitioning = true;
        float elapsed = 0f;

        while (elapsed < delay)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / delay;

            // 1초 동안 Intensity 0 -> 1, Exposure 0 -> -10
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(0f, 1f, t);
            if (colorAdjustments != null) colorAdjustments.postExposure.value = Mathf.Lerp(0f, -10f, t);

            yield return null;
        }

        // 최종 값 보정
        if (vignette != null) vignette.intensity.value = 1f;
        if (colorAdjustments != null) colorAdjustments.postExposure.value = -10f;

        // 실제 씬 전환 로직
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.LogWarning("다음 씬이 없습니다. 빌드 설정을 확인하세요.");
            isTransitioning = false; // 마지막 씬이라면 다시 누를 수 있게 해제
        }
    }
}