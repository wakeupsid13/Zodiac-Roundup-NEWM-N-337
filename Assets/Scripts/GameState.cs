using UnityEngine;
using Unity.Netcode;

public class GameState : NetworkBehaviour
{
    public static GameState Instance;
    public string localPlayerName;

    // Everyone can read, only server writes
    public NetworkVariable<int> TeamScore = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
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
