using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialPit : MonoBehaviour
{
    
    private void OnTriggerEnter(Collider other) {
        if (other.gameObject.CompareTag("TutorialRat"))
        {
            SceneManager.LoadScene("Main");
        }
    }
}
