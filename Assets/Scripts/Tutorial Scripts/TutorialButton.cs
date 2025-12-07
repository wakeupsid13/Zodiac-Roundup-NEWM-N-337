using UnityEngine;
using UnityEngine.SceneManagement;


public class TutorialButton : MonoBehaviour
{
    public void GoToTutorialScene()
    {
        SceneManager.LoadScene("TutorialScene");
    }
}
