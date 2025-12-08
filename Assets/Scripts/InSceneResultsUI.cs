using UnityEngine;
using Unity.Netcode;
using TMPro;

public class InSceneResultsUI : MonoBehaviour
{
    public static InSceneResultsUI Instance;

    [Header("Assign in GameScene")]
    public GameObject resultsPanel;      // whole results overlay
    public TextMeshProUGUI title;
    public TextMeshProUGUI teamScore;
    public GameObject playAgainButton;   // visible only for host

    void Awake()
    {
        Instance = this;
        if (resultsPanel) resultsPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Called from GameSessionManager.ShowResultsClientRpc()
    public static void ShowNow()
    {
        if (Instance != null)
            Instance.ShowInternal();
        else
            Debug.LogWarning("[ResultsUI] Instance is null when ShowNow was called.");
    }

    void ShowInternal()
    {
        if (resultsPanel) resultsPanel.SetActive(true);

        var mgr = GameSessionManager.Instance;
        bool won = (mgr != null && mgr.RoundWon.Value);

        if (title)
            title.text = won ? "Victory!" : "So close — try again!";

        if (GameState.Instance && teamScore)
            teamScore.text = $"Team Score: {GameState.Instance.TeamScore.Value}";

        // Only host can click Play Again
        bool isHost = NetworkManager.Singleton && NetworkManager.Singleton.IsServer;
        if (playAgainButton)
            playAgainButton.SetActive(isHost);
    }

    public void OnPlayAgainClicked()
    {
        // Only host UI should be showing this, but guard anyway
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
            return;

        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.PlayAgainServerRpc();
        }
        else
        {
            Debug.LogWarning("[ResultsUI] GameSessionManager.Instance is null on PlayAgain.");
        }

        // Optionally hide the results panel immediately while next round starts
        if (resultsPanel) resultsPanel.SetActive(false);
    }
}
