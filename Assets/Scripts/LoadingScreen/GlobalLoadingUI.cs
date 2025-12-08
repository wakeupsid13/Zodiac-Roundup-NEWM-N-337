using UnityEngine;
using Unity.Netcode;
using System.Collections;
using TMPro;

public class GlobalLoadingUI : MonoBehaviour
{
    public GameObject loadingPanel;
    public TextMeshProUGUI loadingText;
    public DogLoadingController dogController;

    [Header("Settings")]
    public float minimumDisplaySeconds = 4f;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (loadingPanel)
            loadingPanel.SetActive(false);
    }

    void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
        }
        else
        {
            Debug.LogWarning("[GlobalLoadingUI] No NetworkManager.Singleton in OnEnable.");
        }
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    private void OnSceneEvent(SceneEvent evt)
    {
        Debug.Log($"[GlobalLoadingUI] SceneEvent: {evt.SceneEventType} for {evt.SceneName}");

        // When a networked scene starts loading
        if (evt.SceneEventType == SceneEventType.Load ||
            evt.SceneEventType == SceneEventType.Synchronize)
        {
            StartCoroutine(ShowLoadingRoutine(evt.SceneName));
        }
    }

    private IEnumerator ShowLoadingRoutine(string sceneName)
    {
        Debug.Log($"[GlobalLoadingUI] Showing loading UI for {sceneName}");

        // SHOW LOADING SCREEN
        if (loadingPanel) loadingPanel.SetActive(true);
        if (loadingText) loadingText.text = $"Loading {sceneName}...";
        if (dogController != null) dogController.ShowDog();

        float timer = 0f;

        // Stay visible for at least X seconds
        while (timer < minimumDisplaySeconds)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        Debug.Log("[GlobalLoadingUI] Hiding loading UI");
        // After minimum time, hide loading UI
        if (loadingPanel) loadingPanel.SetActive(false);
        if (dogController != null) dogController.HideDog();
    }
}
