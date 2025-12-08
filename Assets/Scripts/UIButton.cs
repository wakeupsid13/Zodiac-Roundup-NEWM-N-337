using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class UIButton : MonoBehaviour
{
    [Header("Main Menu UI")]
    public GameObject mainPanel;              // whole start menu panel
    public TMP_InputField joinCodeField;
    public Button joinRelayButton;

    [SerializeField]
    private TMP_InputField nameInput;

    [Header("Scene Names")]
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private string tutorialSceneName = "TutorialScene";

    bool _joining;

    void Awake()
    {
        // Make sure NetworkManager / RelayManager / GameState live on a
        // DontDestroyOnLoad object somewhere in the Start scene.
    }

    public async void StartHost()
    {
        // set name
        if (GameState.Instance)
            GameState.Instance.ChangeName(nameInput ? nameInput.text.Trim() : "");

        // 🔹 show loading before we start hosting / switch scene
        if (LoadingManager.Instance != null)
            LoadingManager.Instance.Show("Starting lobby...");

        // start host
        NetworkManager.Singleton.StartHost();

        // host triggers networked scene load to Lobby
        if (NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                lobbySceneName,
                UnityEngine.SceneManagement.LoadSceneMode.Single
            );
        }

        HideMainMenu();
    }


    public async void StartClient()
    {
        if (GameState.Instance)
            GameState.Instance.ChangeName(nameInput ? nameInput.text.Trim() : "");

        NetworkManager.Singleton.StartClient();

        HideMainMenu();
    }

    public async void StartClientViaRelay()
    {
        if (_joining) return;

        var code = (joinCodeField ? joinCodeField.text : "").Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(code) || !Regex.IsMatch(code, "^[A-Z0-9]{6,8}$"))
        {
            Debug.LogWarning($"[ConnectionUI] Join Code invalid: '{code}'.");
            return;
        }

        try
        {
            _joining = true;
            if (joinRelayButton) joinRelayButton.interactable = false;

            if (GameState.Instance)
                GameState.Instance.ChangeName(nameInput ? nameInput.text.Trim() : "");

            // Join via Relay (this starts the client)
            await RelayManager.Instance.JoinRelayAndStartClientAsync(code);
            Debug.Log("[ConnectionUI] Join via Relay requested...");

            // Host is already in LobbyScene; NGO will sync us into it.
            HideMainMenu();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[ConnectionUI] Relay join failed: " + ex.Message);
        }
        finally
        {
            _joining = false;
            if (joinRelayButton) joinRelayButton.interactable = true;
        }
    }

    void HideMainMenu()
    {
        if (mainPanel) mainPanel.SetActive(false);
    }

    // Called when the name input field changes (optional)
    public void ChangeName()
    {
        if (!nameInput) return;

        string nm = nameInput.text?.Trim() ?? "";

        if (GameState.Instance != null)
            GameState.Instance.ChangeName(nm);

        // Name will be picked up by PlayerState / LobbySessionManager
        // after connecting; no need to talk to any session manager here now.
    }

    // Tutorial button
    public void OpenTutorial()
    {
        // No networking here – just go offline to tutorial scene
        SceneManager.LoadScene(tutorialSceneName, LoadSceneMode.Single);
    }
}
