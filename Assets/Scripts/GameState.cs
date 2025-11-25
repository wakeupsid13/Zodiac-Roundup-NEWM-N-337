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

    private void Update() {
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
}
