using UnityEngine;

public class MovementController : MonoBehaviour {
    [SerializeField] private Main_Script mainScript;
    [SerializeField] private float movementSpeed;
    
    
    private void Update() {
        if (mainScript.player.isPlaying) {
            this.transform.Translate(Vector3.forward * (Time.deltaTime * this.movementSpeed));
        }
    }
}
