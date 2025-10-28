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

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        var animal = other.GetComponentInParent<AIAnimalServer>();
        if (animal != null && animal.NetworkObject != null && animal.NetworkObject.IsSpawned)
        {
            // 1) Award assists to contributors
            List<ulong> contributors = animal.GetRecentContributors();
            foreach (var clientId in contributors)
            {
                var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
                if (playerObj)
                {
                    var ps = playerObj.GetComponent<PlayerState>();
                    if (ps) ps.Assists.Value += 1;
                }
            }

            // 3) Build names for toast
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

            // 4) Send toast to all clients
            string animalName = animal.gameObject.name.Replace("(Clone)", "").Trim();
            string namesCsv = (names.Count > 0) ? string.Join(", ", names) : "Team";
            ToastBroadcaster.Instance?.ShowToastClientRpc($"{namesCsv} corralled the {animalName}! +{pointsPerAnimal} points.");


            // Despawn the captured animal
            animal.NetworkObject.Despawn();
            animal.ClearContributors();

            // Award team points
            if (GameState.Instance && GameState.Instance.IsServer)
                GameState.Instance.AddScore(pointsPerAnimal);

            // Optional: respawn a new one shortly
            var spawner = FindObjectOfType<AnimalSpawner>();
            if (spawner && spawner.IsServer)
                spawner.Invoke(nameof(AnimalSpawner.SpawnOne), respawnDelay);
        }
    }
}
