using UnityEngine;
using Unity.Netcode;
using Unity.Collections; // for FixedString
using TMPro;

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
        }
    }

    private void OnPlayerNameChanged(FixedString128Bytes oldV, FixedString128Bytes newV)
    {
        if (nameText) nameText.text = newV.ToString();
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
