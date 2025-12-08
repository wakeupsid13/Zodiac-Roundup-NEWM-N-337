using UnityEngine;
using TMPro;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance;

    [SerializeField] private GameObject loaderCanvas;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private DogLoadingController dogController;
    [SerializeField] private float minimumDisplaySeconds = 1.5f;

    float _shownAt = -999f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (loaderCanvas)
                loaderCanvas.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Show(string message)
    {
        _shownAt = Time.unscaledTime;

        if (loaderCanvas) loaderCanvas.SetActive(true);
        if (loadingText) loadingText.text = message;

        if (dogController != null)
            dogController.ShowDog();
    }

    public void Hide()
    {
        // enforce a minimum time so it doesn’t flicker
        if (Time.unscaledTime - _shownAt < minimumDisplaySeconds)
            return;

        if (dogController != null)
            dogController.HideDog();

        if (loaderCanvas)
            loaderCanvas.SetActive(false);
    }
}
