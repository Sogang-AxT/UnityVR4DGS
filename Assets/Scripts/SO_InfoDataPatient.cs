using UnityEngine;

[CreateAssetMenu(fileName = "SO_InfoDataPatient", menuName = "Scriptable Objects/SO_InfoDataPatient")]
public class SO_InfoDataPatient : ScriptableObject {
    public string PatientName;  // 환자 이름
    [TextArea] public string PatientComment;    // 사고 접수 텍스트; 접수원 작성본
    // TODO: 필요한 사항 추후 추가
}
