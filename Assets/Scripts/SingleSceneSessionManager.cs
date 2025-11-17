using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public enum RoundPhase : byte { Lobby, Playing, Results }

public class SingleSceneSessionManager : NetworkBehaviour
{
    public static SingleSceneSessionManager Instance;

    [Header("References (assign in Inspector)")]
    public Transform[] lobbySpawns;
    public Transform[] gameSpawns;
    public AnimalSpawner spawner;
    public GameObject gameAreaRoot;
    public GameObject lobbyAreaRoot;

    [Header("Rules")]
    public int winPoints = 100;
    public float roundSeconds = 300f; // 5:00

    public NetworkVariable<RoundPhase> Phase = new NetworkVariable<RoundPhase>(
        RoundPhase.Lobby, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> SecondsRemaining = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> RoundWon = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public struct LobbyPlayer : INetworkSerializable, System.IEquatable<LobbyPlayer>
    {
        public ulong ClientId;
        public FixedString64Bytes Name;
        public bool Ready;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        { serializer.SerializeValue(ref ClientId); serializer.SerializeValue(ref Name); serializer.SerializeValue(ref Ready); }
        // IMPORTANT: compare ALL fields so NetworkList sees changes
        public bool Equals(LobbyPlayer other)
            => ClientId == other.ClientId
            && Name.Equals(other.Name)
            && Ready == other.Ready;

        public override bool Equals(object obj)
            => obj is LobbyPlayer other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ClientId.GetHashCode();
                hash = (hash * 397) ^ Name.GetHashCode();
                hash = (hash * 397) ^ Ready.GetHashCode();
                return hash;
            }
        }
    }
    public NetworkList<LobbyPlayer> LobbyPlayers;
    // public TMP_Text debugPlayerListText;

    Coroutine _timerCo;

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }

        LobbyPlayers = new NetworkList<LobbyPlayer>(
            new List<LobbyPlayer>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback  += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            EnterLobby_Server();
        }
    }

    void OnDestroy()
    {
        if (NetworkManager && NetworkManager.IsServer)
        {
            NetworkManager.OnClientConnectedCallback  -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    void EnterLobby_Server()
    {
        Phase.Value = RoundPhase.Lobby;
        SecondsRemaining.Value = 0;
        RoundWon.Value = false;
        if (spawner) spawner.enabled = false;
        if (gameAreaRoot) gameAreaRoot.SetActive(true);
        if (lobbyAreaRoot) lobbyAreaRoot.SetActive(true);

        foreach (var no in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            if (!no.IsPlayerObject) continue;
            PlaceAtLobby(no.gameObject);
        }

        // Ensure every connected client has a lobby row and use their current DisplayName if available
        foreach (var c in NetworkManager.ConnectedClientsList)
        {
            var cid = c.ClientId;
            var no = NetworkManager.SpawnManager.GetPlayerNetworkObject(cid);
            string name = $"Player {cid}";
            var ps = no ? no.GetComponent<PlayerState>() : null;
            if (ps != null)
            {
                var s = ps.DisplayName.Value.ToString();
                if (!string.IsNullOrWhiteSpace(s)) name = s;
            }
            SetLobbyName_Server(cid, name);
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        Debug.Log($"[SessionManager] Client connected: {clientId}");

        string name = $"Player {clientId}";
        var playerNO = NetworkManager.SpawnManager.GetPlayerNetworkObject(clientId);
        var ps = playerNO ? playerNO.GetComponent<PlayerState>() : null;
        if (ps != null)
        {
            var s = ps.DisplayName.Value.ToString();
            if (!string.IsNullOrWhiteSpace(s)) name = s;
        }

        // Use the same single point of truth to create/update
        SetLobbyName_Server(clientId, name);

        // (keep your existing PlaceAtLobby call after this)
        var playerNO2 = NetworkManager.SpawnManager.GetPlayerNetworkObject(clientId);
        if (playerNO2) PlaceAtLobby(playerNO2.gameObject);

    }

    void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        for (int i = LobbyPlayers.Count - 1; i >= 0; i--)
            if (LobbyPlayers[i].ClientId == clientId) LobbyPlayers.RemoveAt(i);
    }

    void PlaceAtLobby(GameObject player)
    {
        if (!player) return;
        var idx = (int)(player.GetComponent<NetworkObject>().OwnerClientId % (ulong)Mathf.Max(1, lobbySpawns.Length));
        var t = lobbySpawns.Length > 0 ? lobbySpawns[idx] : null;
        if (t)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.transform.SetPositionAndRotation(t.position, t.rotation);
            if (cc) cc.enabled = true;
        }
    }

    void PlaceAtGame(GameObject player)
    {
        if (!player) return;
        var idx = (int)(player.GetComponent<NetworkObject>().OwnerClientId % (ulong)Mathf.Max(1, gameSpawns.Length));
        var t = gameSpawns.Length > 0 ? gameSpawns[idx] : null;
        if (t)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.transform.SetPositionAndRotation(t.position, t.rotation);
            if (cc) cc.enabled = true;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReportPlayerNameServerRpc(ulong clientId, string name)
    {
        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == clientId)
            {
                var row = LobbyPlayers[i];
                row.Name = new FixedString64Bytes(string.IsNullOrWhiteSpace(name) ? $"Player{clientId}" : name);
                LobbyPlayers[i] = row;
                break;
            }
        }
    }

    // Server-only setter so the host can update names without using a ServerRpc
    public void SetLobbyName_Server(ulong clientId, string name)
    {
        if (!IsServer) return;
        var finalName = string.IsNullOrWhiteSpace(name) ? $"Player {clientId}" : name;

        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == clientId)
            {
                var row = LobbyPlayers[i];
                row.Name = new Unity.Collections.FixedString64Bytes(finalName);
                LobbyPlayers[i] = row;
                return;
            }
        }

        // If no row yet, add it
        LobbyPlayers.Add(new LobbyPlayer
        {
            ClientId = clientId,
            Name = new Unity.Collections.FixedString64Bytes(finalName),
            Ready = false
        });
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(bool ready, ServerRpcParams rpc = default)
    {
        ulong cid = rpc.Receive.SenderClientId;
        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == cid)
            {
                var row = LobbyPlayers[i];
                row.Ready = ready;
                LobbyPlayers[i] = row; 
                break;
            }
        }
        if (AllReady()) BeginRound_Server();
    }

    bool AllReady()
    {
        if (LobbyPlayers.Count == 0) return false;
        foreach (var p in LobbyPlayers) if (!p.Ready) return false;
        return true;
    }

    void BeginRound_Server()
    {
        Phase.Value = RoundPhase.Playing;
        RoundWon.Value = false;
        SecondsRemaining.Value = roundSeconds;

        if (GameState.Instance && GameState.Instance.IsServer)
            GameState.Instance.TeamScore.Value = 0;

        // NEW: tell everyone to turn off their Ready toggles locally
        ResetReadyTogglesClientRpc();

        foreach (var no in NetworkManager.SpawnManager.SpawnedObjectsList)
            if (no.IsPlayerObject) PlaceAtGame(no.gameObject);

        if (spawner) spawner.enabled = true;

        if (_timerCo != null) StopCoroutine(_timerCo);
        _timerCo = StartCoroutine(TimerTick());
    }

    IEnumerator TimerTick()
    {
        while (SecondsRemaining.Value > 0f)
        {
            yield return new WaitForSeconds(1f);
            SecondsRemaining.Value -= 1f;

            if (GameState.Instance && GameState.Instance.TeamScore.Value >= winPoints)
            { EndRound_Server(true); yield break; }
        }
        bool won = (GameState.Instance && GameState.Instance.TeamScore.Value >= winPoints);
        EndRound_Server(won);
    }

    void EndRound_Server(bool won)
    {
        if (_timerCo != null) { StopCoroutine(_timerCo); _timerCo = null; }
        RoundWon.Value = won;
        Phase.Value = RoundPhase.Results;

        if (spawner) spawner.enabled = false;
        ShowResultsClientRpc();
    }

    [ClientRpc] void ShowResultsClientRpc() { InSceneResultsUI.ShowNow(); }

    [ServerRpc(RequireOwnership = false)]
    public void PlayAgainServerRpc()
    {
        foreach (var no in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            var ps = no.GetComponent<PlayerState>();
            if (ps && ps.IsServer) ps.Assists.Value = 0;
        }
        if (GameState.Instance && GameState.Instance.IsServer)
            GameState.Instance.TeamScore.Value = 0;

        // NEW: tell everyone to turn off their Ready toggles locally
        ResetReadyTogglesClientRpc();

        for (int i = 0; i < LobbyPlayers.Count; i++)
        { var row = LobbyPlayers[i]; row.Ready = false; LobbyPlayers[i] = row; }
        
        foreach (var no in NetworkManager.SpawnManager.SpawnedObjectsList)
            if (no.IsPlayerObject) PlaceAtLobby(no.gameObject);

        EnterLobby_Server();

        // Tell everyone to update their UI
        ReturnToLobbyClientRpc();
    }

    [ClientRpc]
    void ReturnToLobbyClientRpc()
    {
        if (InSceneResultsUI.Instance != null)
            InSceneResultsUI.Instance.ShowLobbyUI();
    }

    [ClientRpc]
    void ResetReadyTogglesClientRpc()
    {
        var readyToggles = GameObject.FindGameObjectsWithTag("ReadyToggle");
        foreach (var rt in readyToggles)
        {
            var toggle = rt.GetComponent<UnityEngine.UI.Toggle>();
            if (toggle) toggle.isOn = false;  // This will also send "Ready = false" via your UI script
        }
    }

    // Server-only setter so the host can update 'Ready' without a ServerRpc
    public void SetReady_Server(ulong clientId, bool ready)
    {
        if (!IsServer) return;

        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == clientId)
            {
                var row = LobbyPlayers[i];
                row.Ready = ready;
                LobbyPlayers[i] = row;
                break;
            }
        }

        if (AllReady()) BeginRound_Server();
    }
}
