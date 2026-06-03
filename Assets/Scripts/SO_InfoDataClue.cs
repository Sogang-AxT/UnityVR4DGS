using UnityEngine;

[CreateAssetMenu(fileName = "SO_InfoDataClue", menuName = "Scriptable Objects/SO_InfoDataClue")]
public class SO_InfoDataClue : ScriptableObject {
    public string clueName;                 // 단서 이름; 물건 이름
    [TextArea] public string clueComment;   // 단서 관련 설명문
    // TODO: 필요한 사항 추후 추가
}
