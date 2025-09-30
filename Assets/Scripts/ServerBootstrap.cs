using UnityEngine;
using Unity.Netcode;

public class ServerBootstrap : MonoBehaviour
{
    [Tooltip("How many clients can connect (excluding server)?")]
    public int maxConnections = 32;

    async void Start()
    {
#if UNITY_SERVER
        // Dedicated headless build → auto-start Relay server
        string code = await RelayManager.Instance.CreateRelayAndStartServerAsync(maxConnections);
        Debug.Log("[Relay] Share this Join Code with students: " + code);
#else
        // If running in editor with -batchmode, also auto-start
        if (Application.isBatchMode)
        {
            string code = await RelayManager.Instance.CreateRelayAndStartServerAsync(maxConnections);
            Debug.Log("[Relay] Share this Join Code with students: " + code);
        }
#endif
    }
}
