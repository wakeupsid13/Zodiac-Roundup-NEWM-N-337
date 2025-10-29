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

    // 1) Helper: always cleanly reset before starting host/client
    void ResetNetworking()
    {
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // UTP sometimes holds on to state across runs; clear its config too
            var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            utp.SetConnectionData("", 0); // harmless no-op reset
        }
    }

    // 2) Subscribe once (e.g., in Awake) to catch transport failures
    void OnEnable()
    {
        var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        utp.OnTransportEvent += HandleTransportEvent;

        NetworkManager.Singleton.OnClientConnectedCallback += id => Debug.Log($"[Netcode] Client connected: {id}");
        NetworkManager.Singleton.OnClientDisconnectCallback += id => Debug.Log($"[Netcode] Client disconnected: {id}");
        NetworkManager.Singleton.OnServerStarted += () => Debug.Log("[Netcode] Server/Host started");
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton == null) return;
        var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        utp.OnTransportEvent -= HandleTransportEvent;

        NetworkManager.Singleton.OnClientConnectedCallback -= null;
        NetworkManager.Singleton.OnClientDisconnectCallback -= null;
        NetworkManager.Singleton.OnServerStarted -= null;
    }

    void HandleTransportEvent(NetworkEvent evt, ulong _, System.ArraySegment<byte> __, float ___)
    {
        if (evt == NetworkEvent.TransportFailure)
        {
            Debug.LogWarning("[Transport] Failure detected. Tearing down.");
            NetworkManager.Singleton.Shutdown();
            // UI tip: disable Join/Host buttons until a brand-new allocation is created
        }
    }


    public async Task<string> CreateRelayAndStartServerAsync(int maxConnections = 32)
    {
        try
        {
            ResetNetworking();
            await InitializeServicesAsync();

            var alloc = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
            Debug.Log($"[Relay] Join Code: {joinCode}");

            var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

            var endpoint = PickEndpoint(alloc.ServerEndpoints, "dtls");
            bool isSecure = endpoint != null && endpoint.ConnectionType == "dtls";
            string host = endpoint != null ? endpoint.Host : alloc.RelayServer.IpV4;
            ushort port = (ushort)(endpoint != null ? endpoint.Port : alloc.RelayServer.Port);

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

            // IMPORTANT: prefer StartHost() (server + local client)
            // 🚫 DO NOT StartHost() here — that creates a local client & player
            if (!NetworkManager.Singleton.StartServer())
            {
                Debug.LogError("[Relay] StartServer() failed");
                return "";
            }

            return joinCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Relay][Host] {e}");
            throw;
        }
    }

    public async Task JoinRelayAndStartClientAsync(string joinCode)
    {
        try
        {
            ResetNetworking();
            await InitializeServicesAsync();

            var joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);
            var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

            var endpoint = PickEndpoint(joinAlloc.ServerEndpoints, "dtls");
            bool isSecure = endpoint != null && endpoint.ConnectionType == "dtls";
            string host = endpoint != null ? endpoint.Host : joinAlloc.RelayServer.IpV4;
            ushort port = (ushort)(endpoint != null ? endpoint.Port : joinAlloc.RelayServer.Port);

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
        catch (System.Exception e)
        {
            Debug.LogError($"[Relay][Client] {e}");
            throw;
        }
    }
}