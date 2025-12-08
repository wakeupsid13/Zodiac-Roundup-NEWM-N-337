using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class InSceneLobbyUI : MonoBehaviour
{
    [Header("Assign")]
    public Transform listRoot;          // parent for the player rows
    public GameObject rowPrefab;        // prefab with 2 TMP texts: [Name, ReadyStatus]
    public Toggle readyToggle;
    public GameObject lobbyPanel;       // optional: whole lobby UI panel

    bool _sentInitial;                  // one-time sync once our row exists

    void OnEnable()
    {
        _sentInitial = false;

        if (readyToggle)
            readyToggle.onValueChanged.AddListener(OnReadyChanged);

        InvokeRepeating(nameof(Refresh), 0.1f, 0.25f);
    }

    void OnDisable()
    {
        if (readyToggle)
            readyToggle.onValueChanged.RemoveListener(OnReadyChanged);

        CancelInvoke(nameof(Refresh));
        _sentInitial = false;
    }

    bool IsLive()
    {
        return NetworkManager.Singleton &&
               NetworkManager.Singleton.IsListening &&
               LobbySessionManager.Instance &&
               LobbySessionManager.Instance.NetworkObject &&
               LobbySessionManager.Instance.NetworkObject.IsSpawned;
    }

    public void OnReadyChanged(bool on)
    {
        if (!IsLive()) return;

        // Everyone (host + clients) just calls the ServerRpc;
        // LobbySessionManager figures out who sent it via ServerRpcParams.
        LobbySessionManager.Instance.SetReadyServerRpc(on);
    }

    void Refresh()
    {
        bool live = IsLive();

        if (readyToggle)
            readyToggle.interactable = live;

        if (lobbyPanel)
            lobbyPanel.SetActive(live);

        if (!live || LobbySessionManager.Instance == null)
            return;

        var mgr = LobbySessionManager.Instance;

        // One-time initial sync: only after our row exists on the server
        if (!_sentInitial && readyToggle)
        {
            ulong myId = NetworkManager.Singleton.LocalClientId;
            bool haveRow = false;

            foreach (var p in mgr.LobbyPlayers)
            {
                if (p.ClientId == myId)
                {
                    haveRow = true;
                    break;
                }
            }

            if (haveRow)
            {
                mgr.SetReadyServerRpc(readyToggle.isOn);
                _sentInitial = true;
            }
        }

        // Rebuild player list UI
        foreach (Transform c in listRoot)
            Destroy(c.gameObject);

        foreach (var p in mgr.LobbyPlayers)
        {
            var go = Instantiate(rowPrefab, listRoot);
            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);

            if (texts.Length > 0)
                texts[0].text = p.Name.ToString();

            if (texts.Length > 1)
                texts[1].text = p.Ready ? "Ready ✓" : "Not Ready !!!";
        }
    }
}
