using UnityEngine;
using Unity.Netcode;
using Unity.Collections; // for FixedString
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class PlayerState : NetworkBehaviour
{
    // Visible to all; server writes
    public NetworkVariable<int> Assists = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> PersonalScore = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // How many times this player fell into the pit (for debugging / UI if you want it)
    public NetworkVariable<int> PitPenalties = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString128Bytes> DisplayName = new NetworkVariable<FixedString128Bytes>(
        new FixedString128Bytes("Player"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private TMP_Text nameText;

    public override void OnNetworkSpawn()
    {
        // === Name label: initialize for EVERYONE and subscribe to updates ===
        nameText = GetComponentInChildren<TMP_Text>(true);
        if (nameText)
        {
            nameText.transform.position = transform.position + new Vector3(0, 1.25f, 0);
            var rend = GetComponent<Renderer>();
            if (rend) nameText.color = rend.material.color;

            // initial value (empty by default until server writes)
            var display = DisplayName.Value.ToString();
            nameText.text = string.IsNullOrWhiteSpace(display)
                ? $"Player {OwnerClientId}"
                : display;

            // keep it synced when the server sets PlayerName
            DisplayName.OnValueChanged += OnPlayerNameChanged;
        }

        // === Owner pushes their cached name to the server ===
        if (IsOwner)
        {
            var cached = GameState.Instance ? GameState.Instance.localPlayerName : "";
            Debug.Log($"[{OwnerClientId}] cached name at spawn: '{cached}' (obj {NetworkObjectId})");
            if (!string.IsNullOrWhiteSpace(cached))
            {
                if (IsServer)
                    DisplayName.Value = new FixedString128Bytes(cached);
                else
                    SetNameServerRpc(cached);
            }

            // Only bother syncing into lobby list if we're actually in the LobbyScene
            if (SceneManager.GetActiveScene().name == "LobbyScene")
            {
                StartCoroutine(PushNameToLobbyWhenReady());
            }
        }
    }

    // Pushes name into LobbySessionManager's NetworkList when it's ready
    private IEnumerator PushNameToLobbyWhenReady()
    {
        // Wait until LobbySessionManager exists and is network-spawned
        while (LobbySessionManager.Instance == null ||
               LobbySessionManager.Instance.NetworkObject == null ||
               !LobbySessionManager.Instance.NetworkObject.IsSpawned)
        {
            yield return null;
        }

        var cached = GameState.Instance ? GameState.Instance.localPlayerName : "";
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var mgr = LobbySessionManager.Instance;
            if (mgr == null) yield break;

            // Tell server to overwrite default "Player {clientId}" in LobbyPlayers
            if (IsServer)
                mgr.AddOrUpdateLobbyPlayer_Server(OwnerClientId, cached);
            else
                mgr.ReportPlayerNameServerRpc(OwnerClientId, cached);
        }
    }

    private void OnPlayerNameChanged(FixedString128Bytes oldV, FixedString128Bytes newV)
    {
        if (nameText)
            nameText.text = newV.ToString();

        // If we're in a scene where LobbySessionManager exists (LobbyScene),
        // also update the lobby list entry to match
        var mgr = LobbySessionManager.Instance;
        if (mgr != null && mgr.IsSpawned)
        {
            var s = newV.ToString();
            if (IsServer)
                mgr.AddOrUpdateLobbyPlayer_Server(OwnerClientId, s);
            else
                mgr.ReportPlayerNameServerRpc(OwnerClientId, s);
        }
    }

    public override void OnNetworkDespawn()
    {
        // avoid dangling delegates
        DisplayName.OnValueChanged -= OnPlayerNameChanged;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetNameServerRpc(string newName)
    {
        Debug.Log("SetNameServerRpc: " + newName);
        DisplayName.Value = new FixedString128Bytes(newName);
    }
}
