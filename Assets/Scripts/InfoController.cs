// 말풍선을 클릭했을 때, Pad에 표시되는 정보 화면을 자신의 것으로 갱신 
using UnityEngine;

public class InfoController : MonoBehaviour {
    private PadController playerPadController; // 플레이용 단말기; 정보 출력           
    
    [Header("SO - InfoData")]
    [SerializeField] private SO_InfoDataClue infoDataClue;
    [SerializeField] private SO_InfoDataPatient infoDataPatient;
    
    
    public void OnClickBalloon() {
        if (this.playerPadController == null) return;   // NULL CHCK
        
        if (this.infoDataClue == null && this.infoDataPatient != null)
            this.playerPadController.UpdatePadScreen(this.infoDataPatient);
        else if (this.infoDataPatient == null && this.infoDataClue != null) 
            this.playerPadController.UpdatePadScreen(this.infoDataClue);
    }
}