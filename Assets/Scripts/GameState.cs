using UnityEngine;
using Unity.Netcode;

public class GameState : NetworkBehaviour
{
    public static GameState Instance;
    public string localPlayerName;

    // Everyone can read, only server writes
    public NetworkVariable<int> TeamScore = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // server side variable to keep track of time
    public NetworkVariable<int> RoundTimeSeconds = new NetworkVariable<int>(
        300, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    float _accum;



    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);

        // if (!IsServer) return;
        // RoundTimeSeconds.Value = 300; // 5 minutes
    }

    private void Update()
    {
        if (!IsServer) return;

        if (RoundTimeSeconds.Value <= 0) return;
        _accum += Time.deltaTime;
        if (_accum >= 1f)
        {
            RoundTimeSeconds.Value -= 1;
            _accum = 0f;
        }
    }

    public void AddScore(int amount)
    {
        if (!IsServer) return;
        TeamScore.Value += amount;
    }

    public void ChangeName(string newName)
    {
        localPlayerName = newName;
    }

    public void ResetRoundForNewGame()
    {
        if (!IsServer) return;

        // 1) Reset score + timer
        TeamScore.Value = 0;
        RoundTimeSeconds.Value = 300; // or whatever your round length is

        // 2) Despawn ALL animals from previous round
        var animals = UnityEngine.Object.FindObjectsByType<AIAnimalServer>(FindObjectsSortMode.None);
        foreach (var a in animals)
        {
            if (a.NetworkObject && a.NetworkObject.IsSpawned)
            {
                a.NetworkObject.Despawn(true); // true = destroy on server + clients
            }
        }
    }
}
