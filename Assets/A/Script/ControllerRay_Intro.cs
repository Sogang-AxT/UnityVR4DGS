using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class ControllerRay_Intro : MonoBehaviour
{
    public float rayLength = 10f;
    public Color rayColor = Color.red;
    public bool isRight = false;
    public bool isLeft = false;

    private LineRenderer lr;
    private bool isHitting = false;
    private bool triggerWasPressed = false;

    private static int hitCount = 0;
    private static Material sharedMaterial = null;
    private static Color originalColor;
    private static bool rightHitting = false;
    private static bool leftHitting = false;

    // IntroButton 호버용
    private Material introButtonMaterial;
    private Color introButtonOriginalColor;
    private GameObject currentIntroButton;

    void Start()
    {
        lr = gameObject.AddComponent<LineRenderer>();
        lr.startWidth = 0.005f;
        lr.endWidth = 0.005f;
        lr.positionCount = 2;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = rayColor;
        lr.endColor = rayColor;
        lr.useWorldSpace = true;
    }

    bool GetTriggerPressed()
    {
        var devices = new List<InputDevice>();
        var characteristics = isRight
            ? InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller
            : InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller;

        InputDevices.GetDevicesWithCharacteristics(characteristics, devices);

        if (devices.Count > 0)
        {
            devices[0].TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed);
            Debug.Log($"[{gameObject.name}] triggerButton: {pressed}");
            return pressed;
        }

        Debug.LogWarning($"[{gameObject.name}] 컨트롤러 디바이스 못 찾음!");
        return false;
    }

    void Update()
    {
        bool triggerPressed = GetTriggerPressed();
        bool triggerDown = triggerPressed && !triggerWasPressed;
        triggerWasPressed = triggerPressed;

        if (triggerDown)
            Debug.Log($"[{gameObject.name}] 트리거 DOWN!");

        Vector3 start = transform.position;
        Vector3 end;
        bool hitStartButton = false;
        bool hitIntroButton = false;

        if (Physics.Raycast(start, transform.forward, out RaycastHit hit, rayLength))
        {
            end = hit.point;

            // StartButton
            if (hit.collider.CompareTag("StartButton"))
            {
                hitStartButton = true;

                if (sharedMaterial == null)
                {
                    sharedMaterial = hit.collider.GetComponent<Renderer>().material;
                    originalColor = sharedMaterial.color;
                }

                if (!isHitting)
                {
                    isHitting = true;
                    hitCount++;
                    if (isRight) rightHitting = true;
                    if (isLeft) leftHitting = true;
                    if (hitCount == 1)
                        sharedMaterial.color = originalColor * 0.6f;
                }

                bool myHandHitting = (isRight && rightHitting) || (isLeft && leftHitting);
                if (myHandHitting && triggerDown)
                {
                    Debug.Log($"[{gameObject.name}] 버튼 제거!");
                    DestroyButton(hit.collider.gameObject);
                }
            }

            // IntroButton
            else if (hit.collider.CompareTag("IntroButton"))
            {
                hitIntroButton = true;
                GameObject hitObj = hit.collider.gameObject;

                if (currentIntroButton != hitObj)
                {
                    RestoreIntroButtonColor();
                    currentIntroButton = hitObj;
                    introButtonMaterial = hitObj.GetComponent<Renderer>().material;
                    introButtonOriginalColor = introButtonMaterial.color;
                    introButtonMaterial.color = introButtonOriginalColor * 0.6f;
                }

                if (triggerDown)
                {
                    RestoreIntroButtonColor();
                    hitObj.SetActive(false);

                    Ending_Script ending = FindObjectOfType<Ending_Script>();
                    if (ending != null)
                        ending.StartCoroutine(ending.FadeOutToIntro(1.0f));
                    else
                        Debug.LogWarning("Ending_Script 못 찾음!");
                }
            }
        }
        else
        {
            end = start + transform.forward * rayLength;
        }

        // StartButton 벗어났을 때
        if (!hitStartButton && isHitting)
        {
            isHitting = false;
            hitCount--;
            if (isRight) rightHitting = false;
            if (isLeft) leftHitting = false;

            if (hitCount <= 0)
            {
                hitCount = 0;
                if (sharedMaterial != null)
                {
                    sharedMaterial.color = originalColor;
                    sharedMaterial = null;
                }
            }
        }

        // IntroButton 벗어났을 때
        if (!hitIntroButton)
            RestoreIntroButtonColor();

        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
    }

    void RestoreIntroButtonColor()
    {
        if (introButtonMaterial != null)
        {
            introButtonMaterial.color = introButtonOriginalColor;
            introButtonMaterial = null;
            currentIntroButton = null;
        }
    }

    void DestroyButton(GameObject button)
    {
        hitCount = 0;
        rightHitting = false;
        leftHitting = false;
        isHitting = false;
        if (sharedMaterial != null)
        {
            sharedMaterial.color = originalColor;
            sharedMaterial = null;
        }

        button.SetActive(false);

        Intro_Script intro = FindObjectOfType<Intro_Script>();
        if (intro != null)
            intro.StartCoroutine(intro.LoadNextSceneAfterDelay(1.0f));
        else
            Debug.LogWarning("Intro_Script 못 찾음!");
    }

    void OnDisable()
    {
        if (!isHitting) return;
        isHitting = false;
        hitCount--;
        if (isRight) rightHitting = false;
        if (isLeft) leftHitting = false;
        if (hitCount <= 0)
        {
            hitCount = 0;
            if (sharedMaterial != null)
            {
                sharedMaterial.color = originalColor;
                sharedMaterial = null;
            }
        }
    }
}