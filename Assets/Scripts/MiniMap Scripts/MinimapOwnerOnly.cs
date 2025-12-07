using Unity.Netcode;
using UnityEngine;

public class MinimapOwnerOnly : NetworkBehaviour
{
    [SerializeField] private Camera minimapCamera;

    public override void OnNetworkSpawn()
    {
        // If this player is NOT owned by this client, turn off its minimap camera
        if (!IsOwner)
        {
            if (minimapCamera != null)
                minimapCamera.enabled = false;
        }
        else
        {
            if (minimapCamera != null)
                minimapCamera.enabled = true;
        }
    }
}
