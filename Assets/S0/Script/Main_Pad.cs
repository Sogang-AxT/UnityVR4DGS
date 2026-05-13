using UnityEngine;

public class Main_Pad : MonoBehaviour
{
    public GameObject PadScreen;
    public Material Mat1, Mat2, Mat3, Mat4, Mat5, Mat6;

    private Renderer screenRenderer;

    void Start()
    {
        // PadScreen의 Renderer 컴포넌트를 미리 가져와서 캐싱합니다.
        if (PadScreen != null)
        {
            screenRenderer = PadScreen.GetComponent<Renderer>();
        }
    }

    void Update()
    {
        if (screenRenderer == null) return;

        // 숫자키 1~6 입력을 확인하여 머테리얼 교체
        if (Input.GetKeyDown(KeyCode.Alpha1)) ChangeMaterial(Mat1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChangeMaterial(Mat2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ChangeMaterial(Mat3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ChangeMaterial(Mat4);
        if (Input.GetKeyDown(KeyCode.Alpha5)) ChangeMaterial(Mat5);
        if (Input.GetKeyDown(KeyCode.Alpha6)) ChangeMaterial(Mat6);
    }

    // 머테리얼을 변경하는 공통 함수
    void ChangeMaterial(Material targetMat)
    {
        if (targetMat != null)
        {
            screenRenderer.material = targetMat;
        }
    }
}