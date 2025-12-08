using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class GameSessionManager : NetworkBehaviour
{
    public static GameSessionManager Instance;

    [Header("Game References (GameScene)")]
    public Transform[] gameSpawns;
    public AnimalSpawner spawner;
    public GameObject gameAreaRoot;   // optional root for arena

    [Header("Rules")]
    public int winPoints = 100;
    public float roundSeconds = 300f; // 5:00

    public NetworkVariable<RoundPhase> Phase = new NetworkVariable<RoundPhase>(
        RoundPhase.Playing, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> SecondsRemaining = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> RoundWon = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    Coroutine _timerCo;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // We are now in GameScene, server-side.
        // Start the round automatically.
        BeginRound_Server();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void PlaceAtGame(GameObject player)
    {
        if (!player || gameSpawns == null || gameSpawns.Length == 0) return;

        var netObj = player.GetComponent<NetworkObject>();
        if (!netObj) return;

        int idx = (int)(netObj.OwnerClientId % (ulong)Mathf.Max(1, gameSpawns.Length));
        var t = gameSpawns[idx];
        if (!t) return;

        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.transform.SetPositionAndRotation(t.position, t.rotation);
        if (cc) cc.enabled = true;
    }

    void BeginRound_Server()
    {
        Phase.Value = RoundPhase.Playing;
        RoundWon.Value = false;
        SecondsRemaining.Value = roundSeconds;

        if (GameState.Instance && GameState.Instance.IsServer)
            GameState.Instance.TeamScore.Value = 0;

        if (gameAreaRoot) gameAreaRoot.SetActive(true);

        foreach (var no in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            if (no.IsPlayerObject)
                PlaceAtGame(no.gameObject);
        }

        if (spawner) spawner.enabled = true;

        if (_timerCo != null) StopCoroutine(_timerCo);
        _timerCo = StartCoroutine(TimerTick());
    }

    IEnumerator TimerTick()
    {
        while (SecondsRemaining.Value > 0f)
        {
            yield return new WaitForSeconds(1f);
            SecondsRemaining.Value -= 1f;

            if (GameState.Instance && GameState.Instance.TeamScore.Value >= winPoints)
            {
                EndRound_Server(true);
                yield break;
            }
        }

        bool won = (GameState.Instance && GameState.Instance.TeamScore.Value >= winPoints);
        EndRound_Server(won);
    }

    void EndRound_Server(bool won)
    {
        if (_timerCo != null)
        {
            StopCoroutine(_timerCo);
            _timerCo = null;
        }

        RoundWon.Value = won;
        Phase.Value = RoundPhase.Results;

        if (spawner) spawner.enabled = false;

        ShowResultsClientRpc();
    }

    [ClientRpc]
    void ShowResultsClientRpc()
    {
        InSceneResultsUI.ShowNow();
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayAgainServerRpc()
    {
        if (!IsServer) return;

        // Reset per-player stats (optional but nice)
        foreach (var no in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            var ps = no.GetComponent<PlayerState>();
            if (ps != null)
            {
                ps.Assists.Value = 0;
                ps.PersonalScore.Value = 0;
                ps.PitPenalties.Value = 0;
            }
        }

        // Reset team score
        if (GameState.Instance && GameState.Instance.IsServer)
            GameState.Instance.TeamScore.Value = 0;

        // Go back to LobbyScene so everyone can ready up again
        NetworkManager.SceneManager.LoadScene(
            "LobbyScene", // make sure this matches your lobby scene name
            UnityEngine.SceneManagement.LoadSceneMode.Single
        );
    }

}
