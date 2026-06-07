using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using System.Collections;

public class ControllerRay_Main : MonoBehaviour
{
    public float rayLength = 10f;
    public Color rayColor = Color.red;
    private LineRenderer lr;
    private Material hitMaterial;
    private Color originalColor;
    private GameObject currentHit;
    public GameObject Main, MeasureBlood, MeasurePulse, Select;
    public GameObject Button_Blood, Button_Pulse, Button_KTAS;
    public GameObject BloodResult, PulseResult;
    public GameObject Grade1, Grade2, Grade3, Grade4, Grade5;
    public GameObject GradeSelectButton;
    public Main_Script mainScript;
    public VoiceScript voiceScript;

    private bool triggerWasPressed = false;

    // Chat 호버용
    private Material chatMaterial;
    private Color chatOriginalColor;
    private GameObject currentChatHit;

    // 음성 재생 관련
    private AudioSource audioSource;
    private int voiceIndex = 0;
    private bool isPlayingVoice = false;

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

        audioSource = gameObject.AddComponent<AudioSource>();
    }

    bool GetTriggerDown()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);

        if (devices.Count > 0)
        {
            devices[0].TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed);
            bool triggerDown = pressed && !triggerWasPressed;
            triggerWasPressed = pressed;
            return triggerDown;
        }

        triggerWasPressed = false;
        return false;
    }

    void ActivateChildrenExcept(GameObject parent, GameObject exception)
    {
        foreach (Transform child in parent.transform)
        {
            child.gameObject.SetActive(child.gameObject != exception);
        }
    }

    void SelectGrade(GameObject selectedGrade)
    {
        GameObject[] grades = { Grade1, Grade2, Grade3, Grade4, Grade5 };
        foreach (GameObject grade in grades)
        {
            if (grade.transform.childCount > 0)
                grade.transform.GetChild(0).gameObject.SetActive(grade == selectedGrade);
        }
    }

    IEnumerator ShowResultAfterDelay(GameObject result, float delay)
    {
        yield return new WaitForSeconds(delay);
        result.SetActive(true);
    }

    IEnumerator PlayVoiceAndHideChat(GameObject chatObj)
    {
        isPlayingVoice = true;

        // Chat 숨기기
        chatObj.SetActive(false);

        // 현재 인덱스 음성 재생
        AudioClip clip = voiceScript.Voice[voiceIndex];
        audioSource.clip = clip;
        audioSource.Play();

        // 다음 인덱스 준비 (순환)
        voiceIndex = (voiceIndex + 1) % voiceScript.Voice.Length;

        // 음성 재생 끝날 때까지 대기
        yield return new WaitForSeconds(clip.length);

        // Chat 다시 보이기
        chatObj.SetActive(true);

        isPlayingVoice = false;
    }

    void RestoreChatColor()
    {
        if (chatMaterial != null)
        {
            chatMaterial.SetColor("_BaseColor", chatOriginalColor);
            chatMaterial = null;
            currentChatHit = null;
        }
    }

    void Update()
    {
        bool triggerDown = GetTriggerDown();

        Vector3 start = transform.position;
        Vector3 end;
        bool hitButton = false;
        bool hitChat = false;

        if (Physics.Raycast(start, transform.forward, out RaycastHit hit, rayLength))
        {
            end = hit.point;

            if (hit.collider.CompareTag("PadButton"))
            {
                hitButton = true;
                GameObject hitObj = hit.collider.gameObject;

                if (currentHit != hitObj)
                {
                    RestoreColor();
                    currentHit = hitObj;
                    hitMaterial = hitObj.GetComponent<Renderer>().material;
                    originalColor = hitMaterial.color;
                    hitMaterial.color = originalColor * 0.6f;
                }

                if (triggerDown)
                {
                    if (hitObj.name == "Back_Button")
                    {
                        Main.SetActive(true);
                        MeasureBlood.SetActive(false);
                        MeasurePulse.SetActive(false);
                        Select.SetActive(false);
                    }
                    else if (hitObj == Button_Blood)
                    {
                        Main.SetActive(false);
                        MeasureBlood.SetActive(true);
                        MeasurePulse.SetActive(false);
                        Select.SetActive(false);

                        ActivateChildrenExcept(MeasureBlood, BloodResult);
                        BloodResult.SetActive(false);
                        StartCoroutine(ShowResultAfterDelay(BloodResult, 1f));
                    }
                    else if (hitObj == Button_Pulse)
                    {
                        Main.SetActive(false);
                        MeasureBlood.SetActive(false);
                        MeasurePulse.SetActive(true);
                        Select.SetActive(false);

                        ActivateChildrenExcept(MeasurePulse, PulseResult);
                        PulseResult.SetActive(false);
                        StartCoroutine(ShowResultAfterDelay(PulseResult, 1f));
                    }
                    else if (hitObj == Button_KTAS)
                    {
                        Main.SetActive(false);
                        MeasureBlood.SetActive(false);
                        MeasurePulse.SetActive(false);
                        Select.SetActive(true);
                    }
                    else if (hitObj == Grade1 || hitObj == Grade2 || hitObj == Grade3 ||
                             hitObj == Grade4 || hitObj == Grade5)
                    {
                        SelectGrade(hitObj);
                    }
                    else if (hitObj == GradeSelectButton)
                    {
                        GradeSelectButton.SetActive(false);
                        if (mainScript != null)
                            mainScript.StartCoroutine(mainScript.LoadNextSceneAfterDelay(1.0f));
                        else
                            Debug.LogWarning("Main_Script 못 찾음!");
                    }
                }
            }

            // Chat 태그
            else if (hit.collider.CompareTag("Chat"))
            {
                hitChat = true;
                GameObject hitObj = hit.collider.gameObject;

                if (currentChatHit != hitObj)
                {
                    RestoreChatColor();
                    currentChatHit = hitObj;
                    chatMaterial = hitObj.GetComponent<Renderer>().material;
                    chatOriginalColor = chatMaterial.GetColor("_BaseColor");
                    chatMaterial.SetColor("_BaseColor", chatOriginalColor * 0.6f);
                }

                if (triggerDown && !isPlayingVoice && voiceScript != null && voiceScript.Voice.Length > 0)
                {
                    RestoreChatColor();
                    StartCoroutine(PlayVoiceAndHideChat(hitObj));
                }
            }
        }
        else
        {
            end = start + transform.forward * rayLength;
        }

        if (!hitButton)
            RestoreColor();

        if (!hitChat)
            RestoreChatColor();

        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
    }

    void RestoreColor()
    {
        if (hitMaterial != null)
        {
            hitMaterial.color = originalColor;
            hitMaterial = null;
            currentHit = null;
        }
    }
}