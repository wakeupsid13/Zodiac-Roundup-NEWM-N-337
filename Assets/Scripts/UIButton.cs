using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class UIButton : MonoBehaviour
{
    public GameObject panel;
    public TMP_InputField joinCodeField;
    public UnityEngine.UI.Button joinRelayButton;

    bool _joining;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private GameObject lobbyPanel;

    public void StartHost()
    {
        if (GameState.Instance) GameState.Instance.ChangeName(nameInput ? nameInput.text.Trim() : "");
        NetworkManager.Singleton.StartHost();
        panel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    public void StartClient()
    {
        if (GameState.Instance) GameState.Instance.ChangeName(nameInput ? nameInput.text.Trim() : "");
        NetworkManager.Singleton.StartClient();
        panel.SetActive(false);
        lobbyPanel.SetActive(true);
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

            if (GameState.Instance) GameState.Instance.ChangeName(nameInput ? nameInput.text.Trim() : "");
            await RelayManager.Instance.JoinRelayAndStartClientAsync(code);
            Debug.Log("[ConnectionUI] Join via Relay requested...");
            panel.SetActive(false);
            lobbyPanel.SetActive(true);
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

    public void ChangeName()
    {
        if (GameState.Instance != null)
            GameState.Instance.ChangeName(nameInput.text);
        if (SingleSceneSessionManager.Instance != null)
        {
            var mgr = SingleSceneSessionManager.Instance;
            var cid = NetworkManager.Singleton.LocalClientId;
            var nm = nameInput ? nameInput.text : "";
            if (NetworkManager.Singleton.IsServer) mgr.SetLobbyName_Server(cid, nm);
            else mgr.ReportPlayerNameServerRpc(cid, nm);
        }
    }

}
