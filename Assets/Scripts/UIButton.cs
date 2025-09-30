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

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        panel.SetActive(false);
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        panel.SetActive(false);
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

            await RelayManager.Instance.JoinRelayAndStartClientAsync(code);
            Debug.Log("[ConnectionUI] Join via Relay requested...");
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

}
