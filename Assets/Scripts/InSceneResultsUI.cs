using UnityEngine;
using Unity.Netcode;
using TMPro;

public class InSceneResultsUI : MonoBehaviour
{
    public static InSceneResultsUI Instance;

    public GameObject resultsPanel;
    public GameObject gamePanel;
    public GameObject lobbyPanel;
    public TextMeshProUGUI title;
    public TextMeshProUGUI teamScore;
    public GameObject playAgainButton;

    void Awake() { Instance = this; if (resultsPanel) resultsPanel.SetActive(false); }
    public static void ShowNow() { if (Instance) Instance.ShowInternal(); }

    void ShowInternal()
    {
        if (resultsPanel) resultsPanel.SetActive(true);
        bool won = SingleSceneSessionManager.Instance && SingleSceneSessionManager.Instance.RoundWon.Value;
        if (title) title.text = won ? "Victory!" : "So close — try again!";
        if (GameState.Instance && teamScore) teamScore.text = $"Team Score: {GameState.Instance.TeamScore.Value}";
        if (playAgainButton) playAgainButton.SetActive(NetworkManager.Singleton);
    }

    public void OnPlayAgainClicked()
    {
        if (NetworkManager.Singleton)
        {
            SingleSceneSessionManager.Instance.PlayAgainServerRpc();
        }

    }
    
    public void ShowLobbyUI()
    {
        if (resultsPanel) resultsPanel.SetActive(false);
        if (gamePanel)   gamePanel.SetActive(false);
        if (lobbyPanel)  lobbyPanel.SetActive(true);
    }
}
