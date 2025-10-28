// ScoreboardUI.cs
// Discovers late-joining players and updates their "Assists" live.
// Inspector: set 'contentRoot' (Vertical Layout Group) and 'rowPrefab' (2 TMP texts: [Name, Assists])

using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class ScoreboardUI : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public Transform contentRoot;   // Parent with a Vertical Layout Group
    public GameObject rowPrefab;    // Prefab with two TMP texts: Name (0), Assists (1)

    // clientId -> row info
    private readonly Dictionary<ulong, (GameObject row, TextMeshProUGUI name, TextMeshProUGUI assists, PlayerState ps)> _rows
        = new();

    // Use NGO's delegate type (not System.Action)
    private readonly Dictionary<ulong, NetworkVariable<int>.OnValueChangedDelegate> _onAssistsChangedHandlers = new();

    private bool _scanning;

    private void OnEnable()
    {
        _scanning = true;
        InvokeRepeating(nameof(ScanSpawnedPlayers), 0.2f, 0.5f);
    }

    private void OnDisable()
    {
        _scanning = false;
        CancelInvoke(nameof(ScanSpawnedPlayers));

        foreach (var kv in _rows)
        {
            var id = kv.Key;
            var (row, _, _, ps) = kv.Value;
            if (ps != null && _onAssistsChangedHandlers.TryGetValue(id, out var handler))
            {
                ps.Assists.OnValueChanged -= handler;
            }
            if (row) Destroy(row);
        }
        _rows.Clear();
        _onAssistsChangedHandlers.Clear();
    }

    private void ScanSpawnedPlayers()
    {
        if (!_scanning || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
        if (contentRoot == null || rowPrefab == null) return;

        // 1) Add rows for newly seen PlayerState objects
        foreach (var no in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            var ps = no.GetComponent<PlayerState>();
            if (!ps) continue;

            var id = ps.OwnerClientId;
            if (_rows.ContainsKey(id)) continue;

            var go = Instantiate(rowPrefab, contentRoot);
            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
            if (texts.Length < 2)
            {
                Debug.LogWarning("[ScoreboardUI] Row prefab must have two TMP texts: Name, Assists");
                Destroy(go);
                continue;
            }

            var nameTxt    = texts[0];
            var assistsTxt = texts[1];

            // Initial values
            nameTxt.text    = ps.DisplayName.Value.ToString();
            assistsTxt.text = ps.Assists.Value.ToString();

            _rows[id] = (go, nameTxt, assistsTxt, ps);

            // Subscribe using the exact delegate type
            NetworkVariable<int>.OnValueChangedDelegate assistsHandler = (oldVal, newVal) =>
            {
                if (_rows.TryGetValue(id, out var rowInfo) && rowInfo.assists != null)
                    rowInfo.assists.text = newVal.ToString();
            };
            ps.Assists.OnValueChanged += assistsHandler;
            _onAssistsChangedHandlers[id] = assistsHandler;
        }

        // 2) Remove rows for despawned players
        var aliveIds = new HashSet<ulong>(
            NetworkManager.Singleton.SpawnManager.SpawnedObjectsList.Select(o => o.OwnerClientId)
        );

        var toRemove = new List<ulong>();
        foreach (var id in _rows.Keys)
            if (!aliveIds.Contains(id)) toRemove.Add(id);

        foreach (var id in toRemove)
        {
            var (row, _, _, ps) = _rows[id];

            if (ps != null && _onAssistsChangedHandlers.TryGetValue(id, out var handler))
            {
                ps.Assists.OnValueChanged -= handler;
                _onAssistsChangedHandlers.Remove(id);
            }

            if (row) Destroy(row);
            _rows.Remove(id);
        }
    }
}
