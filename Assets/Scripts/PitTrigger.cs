using UnityEngine;
using Unity.Netcode;
using System.Text;
using System.Linq;
using Unity.Collections;
using System.Collections.Generic;

public class PitTrigger : NetworkBehaviour
{
    public int pointsPerAnimal = 5;
    public float respawnDelay = 2f;

    public AudioClip splashSound;

    // NEW: per-player cooldown so they don’t get double-penalized
    private readonly Dictionary<ulong, float> _lastPenaltyTime = new Dictionary<ulong, float>();
    public float penaltyCooldown = 1f; // seconds

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // === 1) PLAYER FALLS INTO PIT → NEGATIVE POINT =======================================

        var playerState = other.GetComponentInParent<PlayerState>();
        if (playerState != null && playerState.NetworkObject != null && playerState.NetworkObject.IsSpawned)
        {
            ulong clientId = playerState.OwnerClientId;

            // If we recently penalized this player, ignore duplicate trigger
            if (_lastPenaltyTime.TryGetValue(clientId, out float lastTime))
            {
                if (Time.time - lastTime < penaltyCooldown)
                {
                    // Debug.Log($"[PitTrigger] Ignoring duplicate penalty for {clientId}");
                    return;
                }
            }
            _lastPenaltyTime[clientId] = Time.time;

            // Increment penalty count
            playerState.PitPenalties.Value += 1;

            // Subtract 1 from this player's personal score
            playerState.PersonalScore.Value -= 1;

            // Subtract 1 from TEAM score as well
            if (GameState.Instance && GameState.Instance.IsServer)
                GameState.Instance.AddScore(-1);

            // Toast: who messed up
            string playerName = playerState.DisplayName.Value.ToString();
            if (string.IsNullOrWhiteSpace(playerName))
                playerName = $"Player {playerState.OwnerClientId}";

            ToastBroadcaster.Instance?.ShowToastClientRpc($"{playerName} fell into the pit! -1 point.");

            // Optional: reuse splash for player fall
            PlaySplashSoundClientRpc();

            return; // IMPORTANT: don't also run the animal logic for this collider
        }

        // === 2) ANIMAL FALLS INTO PIT → ASSIST REWARD + TEAM POINTS =========================

        var animal = other.GetComponentInParent<AIAnimalServer>();
        if (animal != null && animal.NetworkObject != null && animal.NetworkObject.IsSpawned)
        {
            // 1) Award assists & personal points to contributors
            List<ulong> contributors = animal.GetRecentContributors();
            foreach (var clientId in contributors)
            {
                var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
                if (playerObj)
                {
                    var ps = playerObj.GetComponent<PlayerState>();
                    if (ps)
                    {
                        ps.Assists.Value += 1;
                        ps.PersonalScore.Value += pointsPerAnimal;
                    }
                }
            }

            // 2) Build names for toast
            var names = new List<string>();
            foreach (var clientId in contributors)
            {
                var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
                if (playerObj)
                {
                    var ps = playerObj.GetComponent<PlayerState>();
                    string n = ps ? ps.DisplayName.Value.ToString() : $"Player{clientId}";
                    names.Add(n);
                }
            }

            string animalName = animal.gameObject.name.Replace("(Clone)", "").Trim();
            string namesCsv = (names.Count > 0) ? string.Join(", ", names) : "Team";
            ToastBroadcaster.Instance?.ShowToastClientRpc($"{namesCsv} corralled the {animalName}! +{pointsPerAnimal} points.");

            // Despawn the captured animal
            animal.NetworkObject.Despawn();
            animal.ClearContributors();

            // Award team points
            if (GameState.Instance && GameState.Instance.IsServer)
                GameState.Instance.AddScore(pointsPerAnimal);

            // Play splash sound
            PlaySplashSoundClientRpc();

            // Optional: respawn a new one shortly
            var spawner = FindObjectOfType<AnimalSpawner>();
            if (spawner && spawner.IsServer)
                spawner.Invoke(nameof(AnimalSpawner.SpawnOne), respawnDelay);
        }
    }

    [ClientRpc]
    void PlaySplashSoundClientRpc()
    {
        if (splashSound == null) return;
        if (Camera.main == null) return;
        AudioSource.PlayClipAtPoint(splashSound, Camera.main.transform.position);
    }
}
