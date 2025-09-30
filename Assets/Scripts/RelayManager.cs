using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance;

    [Tooltip("Set to 'production' unless you made a test environment")]
    public string environmentName = "production";

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    public async Task InitializeServicesAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            var options = new InitializationOptions();
            options.SetEnvironmentName(string.IsNullOrWhiteSpace(environmentName) ? "production" : environmentName);
            await UnityServices.InitializeAsync(options);
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("[UGS] Signed in anonymously");
        }
    }

    // Accept List<> (or any IEnumerable) because some SDKs expose List<RelayServerEndpoint>
    static RelayServerEndpoint PickEndpoint(IEnumerable<RelayServerEndpoint> endpoints, string preferred)
    {
        if (endpoints == null) return null;
        // Prefer DTLS; fallback to UDP if DTLS not available
        var chosen = endpoints.FirstOrDefault(e => e.ConnectionType == preferred)
                  ?? endpoints.FirstOrDefault(e => e.ConnectionType == "udp");
        return chosen;
    }

    public async Task<string> CreateRelayAndStartServerAsync(int maxConnections = 32)
    {
        await InitializeServicesAsync();

        var alloc = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        var joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
        Debug.Log($"[Relay] Join Code: {joinCode}");

        var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

        // Prefer DTLS; fallback to UDP. Some SDKs expose alloc.ServerEndpoints as List<RelayServerEndpoint>
        var endpoint = PickEndpoint(alloc.ServerEndpoints, "dtls");
        bool isSecure = endpoint != null && endpoint.ConnectionType == "dtls";
        string host = endpoint != null ? endpoint.Host : alloc.RelayServer.IpV4;   // fallback for older SDKs
        ushort port = (ushort)(endpoint != null ? endpoint.Port : alloc.RelayServer.Port);

        Debug.Log($"[Relay] Server endpoint: {host}:{port} (type={(isSecure ? "dtls" : "udp")})");

        var relayData = new RelayServerData(
            host: host,
            port: port,
            allocationId: alloc.AllocationIdBytes,
            connectionData: alloc.ConnectionData,
            hostConnectionData: alloc.ConnectionData,
            key: alloc.Key,
            isSecure: isSecure
        );

        utp.SetRelayServerData(relayData);

        // NOTE: Some UTP versions have different OnTransportEvent signatures.
        // If you want a handler, add it only if your version matches.
        // For now we skip wiring this to avoid signature mismatches.

        NetworkManager.Singleton.StartServer();
        return joinCode;
    }

    public async Task JoinRelayAndStartClientAsync(string joinCode)
    {
        await InitializeServicesAsync();

        var joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);
        var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

        var endpoint = PickEndpoint(joinAlloc.ServerEndpoints, "dtls");
        bool isSecure = endpoint != null && endpoint.ConnectionType == "dtls";
        string host = endpoint != null ? endpoint.Host : joinAlloc.RelayServer.IpV4;
        ushort port = (ushort)(endpoint != null ? endpoint.Port : joinAlloc.RelayServer.Port);

        Debug.Log($"[Relay] Client endpoint: {host}:{port} (type={(isSecure ? "dtls" : "udp")})");

        var relayData = new RelayServerData(
            host: host,
            port: port,
            allocationId: joinAlloc.AllocationIdBytes,
            connectionData: joinAlloc.ConnectionData,
            hostConnectionData: joinAlloc.HostConnectionData,
            key: joinAlloc.Key,
            isSecure: isSecure
        );

        utp.SetRelayServerData(relayData);
        NetworkManager.Singleton.StartClient();
    }
}
