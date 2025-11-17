using UnityEngine;
using Unity.Netcode;
using Unity.Collections; // for FixedString
using TMPro;
using System.Collections;

public class PlayerState : NetworkBehaviour
{
    // Visible to all; server writes
    public NetworkVariable<int> Assists = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString128Bytes> DisplayName = new NetworkVariable<FixedString128Bytes>(
        new FixedString64Bytes("Player"), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
            nameText.text = string.IsNullOrWhiteSpace(DisplayName.Value.ToString()) ? $"Player {OwnerClientId}" : DisplayName.Value.ToString();

            // keep it synced when the server sets PlayerName
            DisplayName.OnValueChanged += OnPlayerNameChanged;
        }

        // === Owner pushes their cached name to the server (host writes directly) ===
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
            // ADD: push to lobby list once the manager is spawned
            StartCoroutine(PushNameToLobbyWhenReady());
        }
    }

    private void Update() {
        // if (SingleSceneSessionManager.Instance != null)
        // {
        //     SingleSceneSessionManager.Instance.ReportPlayerNameServerRpc(OwnerClientId, DisplayName.Value.ToString());
        // }
    }

    // ADD:
    private IEnumerator PushNameToLobbyWhenReady()
    {
        // Wait until the SingleSceneSessionManager exists and is network-spawned
        while (SingleSceneSessionManager.Instance == null ||
               SingleSceneSessionManager.Instance.NetworkObject == null ||
               !SingleSceneSessionManager.Instance.NetworkObject.IsSpawned)
        {
            yield return null;
        }

        var cached = GameState.Instance ? GameState.Instance.localPlayerName : "";
        if (!string.IsNullOrWhiteSpace(cached))
        {
            // Tell server to overwrite default "Player{clientId}" in LobbyPlayers
            var mgr = SingleSceneSessionManager.Instance;
            if (mgr == null) yield break;

            if (IsServer) mgr.SetLobbyName_Server(OwnerClientId, cached);
            else mgr.ReportPlayerNameServerRpc(OwnerClientId, cached);
        }
    }

    private void OnPlayerNameChanged(FixedString128Bytes oldV, FixedString128Bytes newV)
    {
        if (nameText) nameText.text = newV.ToString();
        if (SingleSceneSessionManager.Instance != null)
        {
            // Update name in lobby list if needed
            var mgr = SingleSceneSessionManager.Instance;
            if (mgr != null)
            {
                var s = newV.ToString();
                if (IsServer) mgr.SetLobbyName_Server(OwnerClientId, s);
                else mgr.ReportPlayerNameServerRpc(OwnerClientId, s);
            }
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
