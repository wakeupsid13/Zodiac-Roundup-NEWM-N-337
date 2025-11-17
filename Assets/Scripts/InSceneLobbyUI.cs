using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class InSceneLobbyUI : MonoBehaviour
{
    [Header("Assign")]
    public Transform listRoot;
    public GameObject rowPrefab;   // two TMP texts: [Name, ReadyStatus]
    public Toggle readyToggle;
    public GameObject lobbyPanel;
    public GameObject gamePanel;

    bool _sentInitial;  // NEW: send one-time ready state after row exists

    void OnEnable()
    {
        _sentInitial = false;
        if (readyToggle) readyToggle.onValueChanged.AddListener(OnReadyChanged);
        InvokeRepeating(nameof(Refresh), 0.1f, 0.25f);
    }
    void OnDisable()
    {
        if (readyToggle) readyToggle.onValueChanged.RemoveListener(OnReadyChanged);
        CancelInvoke(nameof(Refresh));
        _sentInitial = false;
    }

    bool IsLive()
    {
        return NetworkManager.Singleton &&
               NetworkManager.Singleton.IsListening &&
               SingleSceneSessionManager.Instance &&
               SingleSceneSessionManager.Instance.NetworkObject &&
               SingleSceneSessionManager.Instance.NetworkObject.IsSpawned;
    }

    public void OnReadyChanged(bool on)
    {
        if (!IsLive()) return;

        var mgr = SingleSceneSessionManager.Instance;
        var myId = NetworkManager.Singleton.LocalClientId;

        if (NetworkManager.Singleton.IsServer)
            mgr.SetReady_Server(myId, on);          // host updates directly
        else
            mgr.SetReadyServerRpc(on);              // clients use RPC
    }

    void Refresh()
    {
        bool live = IsLive();
        if (readyToggle) readyToggle.interactable = live;
        if (!live || SingleSceneSessionManager.Instance == null) return;

        // One-time initial sync: only after our row exists on server
        if (!_sentInitial && readyToggle)
        {
            ulong myId = NetworkManager.Singleton.LocalClientId;
            bool haveRow = false;
            foreach (var p in SingleSceneSessionManager.Instance.LobbyPlayers)
                if (p.ClientId == myId) { haveRow = true; break; }

            if (haveRow)
            {
                var mgr = SingleSceneSessionManager.Instance;
                if (NetworkManager.Singleton.IsServer)
                    mgr.SetReady_Server(myId, readyToggle.isOn);
                else
                    mgr.SetReadyServerRpc(readyToggle.isOn);

                _sentInitial = true;
            }
        }

        // rebuild list UI
        foreach (Transform c in listRoot) Destroy(c.gameObject);
        foreach (var p in SingleSceneSessionManager.Instance.LobbyPlayers)
        {
            var go = Instantiate(rowPrefab, listRoot);
            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
            texts[0].text = p.Name.ToString();
            texts[1].text = p.Ready ? "Ready ✓" : "Not Ready !!!";
        }

        // NEW: show panel only in Lobby phase (runs on every client)
        if (lobbyPanel)
        {
            lobbyPanel.SetActive(SingleSceneSessionManager.Instance.Phase.Value == RoundPhase.Lobby);
            gamePanel.SetActive(SingleSceneSessionManager.Instance.Phase.Value == RoundPhase.Playing);
        }
    }
}
