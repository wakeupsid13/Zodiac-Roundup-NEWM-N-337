using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;

public enum RoundPhase : byte { Lobby, Playing, Results }

public class LobbySessionManager : NetworkBehaviour
{
    public static LobbySessionManager Instance;

    [Header("Lobby References (LobbyScene)")]
    public Transform[] lobbySpawns;

    // LOBBY PLAYER DATA
    public struct LobbyPlayer : INetworkSerializable, System.IEquatable<LobbyPlayer>
    {
        public ulong ClientId;
        public FixedString64Bytes Name;
        public bool Ready;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Ready);
        }

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

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LobbyPlayers = new NetworkList<LobbyPlayer>(
            new List<LobbyPlayer>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

        StartCoroutine(DelayedSetupExistingClients_Server());

        // tell everyone to hide the loader once lobby is up
        HideLoadingClientRpc();
    }

    [ClientRpc]
    void HideLoadingClientRpc()
    {
        LoadingManager.Instance?.Hide();
    }

    IEnumerator DelayedSetupExistingClients_Server()
    {
        // wait 1 frame so player objects exist in the LobbyScene
        yield return null;
        SetupExistingClients_Server();
    }


    void SetupExistingClients_Server()
    {
        foreach (var c in NetworkManager.ConnectedClientsList)
        {
            ulong cid = c.ClientId;
            AddOrUpdateLobbyPlayer_Server(cid, GetInitialName_Server(cid));

            var playerNO = NetworkManager.SpawnManager.GetPlayerNetworkObject(cid);
            if (playerNO != null)
            {
                Debug.Log($"[LobbySession] Placing existing client {cid} at lobby spawn");
                PlaceAtLobby(playerNO.gameObject);
            }
            else
            {
                Debug.LogWarning($"[LobbySession] No player NetworkObject found for client {cid} in SetupExistingClients.");
            }
        }
    }

    void OnDestroy()
    {
        if (NetworkManager && NetworkManager.IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        if (Instance == this)
            Instance = null;
    }

    void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        Debug.Log($"[LobbySession] Client connected: {clientId}");

        AddOrUpdateLobbyPlayer_Server(clientId, GetInitialName_Server(clientId));

        var playerNO = NetworkManager.SpawnManager.GetPlayerNetworkObject(clientId);
        if (playerNO != null)
        {
            Debug.Log($"[LobbySession] Placing newly connected client {clientId} at lobby spawn");
            PlaceAtLobby(playerNO.gameObject);
        }
        else
        {
            Debug.LogWarning($"[LobbySession] No player NetworkObject found for client {clientId} on connect.");
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        Debug.Log($"[LobbySession] Client disconnected: {clientId}");

        // Remove from lobby player list
        int index = -1;
        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == clientId)
            {
                index = i;
                break;
            }
        }
        if (index != -1)
        {
            LobbyPlayers.RemoveAt(index);
            Debug.Log($"[LobbySession] Removed client {clientId} from lobby players.");
        }
    }

    string GetInitialName_Server(ulong clientId)
    {
        string name = $"Player {clientId}";
        var no = NetworkManager.SpawnManager.GetPlayerNetworkObject(clientId);
        var ps = no ? no.GetComponent<PlayerState>() : null;
        if (ps != null)
        {
            var s = ps.DisplayName.Value.ToString();
            if (!string.IsNullOrWhiteSpace(s)) name = s;
        }
        return name;
    }

    void PlaceAtLobby(GameObject player)
    {
        if (!player || lobbySpawns == null || lobbySpawns.Length == 0)
        {
            Debug.LogWarning("[LobbySession] PlaceAtLobby skipped - no player or no spawns set.");
            return;
        }

        var netObj = player.GetComponent<NetworkObject>();
        if (!netObj)
        {
            Debug.LogWarning("[LobbySession] Player has no NetworkObject.");
            return;
        }

        int idx = (int)(netObj.OwnerClientId % (ulong)Mathf.Max(1, lobbySpawns.Length));
        var t = lobbySpawns[idx];
        if (!t)
        {
            Debug.LogWarning("[LobbySession] Spawn transform was null.");
            return;
        }

        Debug.Log($"[LobbySession] Placing client {netObj.OwnerClientId} at {t.position}");

        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.transform.SetPositionAndRotation(t.position, t.rotation);
        if (cc) cc.enabled = true;
    }

    // Host-only direct setter
    public void AddOrUpdateLobbyPlayer_Server(ulong clientId, string name)
    {
        if (!IsServer) return;

        string finalName = string.IsNullOrWhiteSpace(name) ? $"Player {clientId}" : name;

        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == clientId)
            {
                var row = LobbyPlayers[i];
                row.Name = new FixedString64Bytes(finalName);
                LobbyPlayers[i] = row;
                return;
            }
        }

        LobbyPlayers.Add(new LobbyPlayer
        {
            ClientId = clientId,
            Name = new FixedString64Bytes(finalName),
            Ready = false
        });
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReportPlayerNameServerRpc(ulong clientId, string name)
    {
        if (!IsServer) return;

        string finalName = string.IsNullOrWhiteSpace(name) ? $"Player {clientId}" : name;

        for (int i = 0; i < LobbyPlayers.Count; i++)
        {
            if (LobbyPlayers[i].ClientId == clientId)
            {
                var row = LobbyPlayers[i];
                row.Name = new FixedString64Bytes(finalName);
                LobbyPlayers[i] = row;
                return;
            }
        }

        LobbyPlayers.Add(new LobbyPlayer
        {
            ClientId = clientId,
            Name = new FixedString64Bytes(finalName),
            Ready = false
        });
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetReadyServerRpc(bool ready, ServerRpcParams rpc = default)
    {
        if (!IsServer) return;

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

        if (AllReady())
            BeginGame_Server();
    }

    bool AllReady()
    {
        if (LobbyPlayers.Count == 0) return false;
        foreach (var p in LobbyPlayers)
            if (!p.Ready) return false;
        return true;
    }

    // This is the ONLY place that moves everyone to GameScene
    void BeginGame_Server()
    {
        if (!IsServer) return;

        NetworkManager.SceneManager.LoadScene(
            "GameScene",
            UnityEngine.SceneManagement.LoadSceneMode.Single
        );
    }

    // Host UI can call this instead of auto-start on AllReady, if you want
    [ServerRpc(RequireOwnership = false)]
    public void ForceStartGameServerRpc()
    {
        BeginGame_Server();
    }
}
