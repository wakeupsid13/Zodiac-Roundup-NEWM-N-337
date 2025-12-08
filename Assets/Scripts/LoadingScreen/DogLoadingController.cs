using UnityEngine;
public class DogLoadingController : MonoBehaviour
{
    public GameObject dogRoot;
    public Animator dogAnimator;

    void Awake()
    {
        if (dogRoot)
            dogRoot.SetActive(false);
    }

    public void ShowDog()
    {
        if (dogRoot) dogRoot.SetActive(true);
    }

    public void HideDog()
    {
        if (dogRoot) dogRoot.SetActive(false);
    }
}
