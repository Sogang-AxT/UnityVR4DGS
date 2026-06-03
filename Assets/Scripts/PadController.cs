using System;
using TMPro;
using UnityEngine;

public class PadController : MonoBehaviour {
    [Header("Panel")]
    [SerializeField] private GameObject defaultPanel;
    [SerializeField] private GameObject cluePanel;
    [SerializeField] private GameObject patientPanel;
    
    [Space(25f)]
    
    [Header("Clue")]
    [SerializeField] private TMP_Text clueName;
    [SerializeField] private TMP_Text clueComment;
    
    [Space(25f)]
    
    [Header("Patient")]
    [SerializeField] private TMP_Text patientName;
    [SerializeField] private TMP_Text patientComment;


    private void Init() {
        PanelSet(GameControlTypeManager.PadScreenStatusType.DEFAULT);
    }

    private void Awake() {
        Init();
    }

    public void UpdatePadScreen(SO_InfoDataClue infoDataClue) {
        PanelSet(GameControlTypeManager.PadScreenStatusType.CLUE);
        
        this.clueName.text = infoDataClue.clueName;
        this.clueComment.text = infoDataClue.clueComment;
    }

    public void UpdatePadScreen(SO_InfoDataPatient infoDataPatient) {
        PanelSet(GameControlTypeManager.PadScreenStatusType.PATIENT);
        
        this.patientName.text = infoDataPatient.PatientName;
        this.patientComment.text = infoDataPatient.PatientComment;
    }

    private void PanelSet(GameControlTypeManager.PadScreenStatusType type) {
        switch (type) {
            case GameControlTypeManager.PadScreenStatusType.DEFAULT:
                this.defaultPanel.SetActive(true);
                this.cluePanel.SetActive(false);
                this.patientPanel.SetActive(false);
                break;
            case GameControlTypeManager.PadScreenStatusType.CLUE:
                this.defaultPanel.SetActive(false);
                this.cluePanel.SetActive(true);
                this.patientPanel.SetActive(false);
                break;
            case GameControlTypeManager.PadScreenStatusType.PATIENT:
                this.defaultPanel.SetActive(false);
                this.cluePanel.SetActive(false);
                this.patientPanel.SetActive(true);
                break;
        }
    }
}