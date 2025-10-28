using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class AnimalSpawner : NetworkBehaviour
{
    public NetworkObject animalPrefab;
    public Transform[] spawnPoints;
    public int targetAlive = 10;

    public override void OnNetworkSpawn()
    {
        if (IsServer) EnsureTarget();
    }

    public void SpawnOne()
    {
        if (!IsServer || animalPrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;
        Transform t = spawnPoints[Random.Range(0, spawnPoints.Length)];
        var animal = Instantiate(animalPrefab, t.position, Quaternion.identity);
        animal.Spawn(true);
    }

    [ServerRpc(RequireOwnership = false)]
    public void EnsureTargetServerRpc()
    {
        EnsureTarget();
    }

    void EnsureTarget()
    {
        int alive = FindObjectsOfType<AIAnimalServer>().Length;
        for (int i = alive; i < targetAlive; i++) SpawnOne();
    }
}
