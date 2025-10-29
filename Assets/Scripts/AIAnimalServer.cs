using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using System.Collections.Generic;
using TMPro;

[System.Serializable]
public struct ContributorInfo
{
    public double lastTime; // Time.timeAsDouble
}


[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class AIAnimalServer : NetworkBehaviour
{
    [Header("Movement (Agent)")]
    public NavMeshAgent agent;
    public float baseSpeed = 3f;
    [Range(0.05f, 0.9f)] public float slowMultiplier = 0.3f;
    public float influenceRadius = 6f;
    [Range(10f, 120f)] public float alignAngleDegrees = 120f;
    public float wanderRadius = 15f;
    public float wanderInterval = 3f;

    [Header("Physics Kick (when ≥3 aligned players)")]
    [Tooltip("Instant velocity change applied when herded by an aligned group (≥3 players).")]
    public float impulseStrength = 6f;
    [Tooltip("Time between impulses while the herd condition remains true.")]
    public float impulseCooldown = 0.35f;
    [Tooltip("Minimum time to remain in physics mode before agent can regain control.")]
    public float physicsReenableCooldown = 0.5f;
    [Tooltip("Velocity magnitude below which we allow the agent to take over (after cooldown).")]
    public float reenableVelocityThreshold = 0.1f;

    [Header("Antigrief / Unstuck (Agent only)")]
    public float stuckRepathTime = 3f;
    public float minMoveDistance = 0.2f;

    [Header("Detection")]
    public LayerMask playerMask; // set to Player layer
    [Tooltip("If true, only count colliders whose root has NetworkObject.IsPlayerObject.")]
    public bool requireNetworkPlayerRoot = true;

    [Header("Animal Status UI (Client-side recommended)")]
    public TMP_Text animalStateText; // optional, scene object (not used on dedicated server)

    // --- private state ---
    float _wanderTimer;
    Vector3 _lastPos;
    float _stuckTimer;

    Rigidbody _rb;
    bool _physicsMode;            // true while agent is disabled and physics is in control
    bool _lastFrameWasKick;       // for edge-triggering and spam control
    float _nextAllowedImpulseAt;  // cooldown gate for impulse
    float _physicsLockUntil;      // Time.time when physics can hand back to nav
    bool _grounded;               // cached per-Update

    // Grounding probe
    const float GroundProbeRadius = 0.2f;
    const float GroundProbeDistance = 0.6f;
    const float NavmeshSnapSmall = 0.25f; // small snap to avoid popping from pits to rim

    [Header("Assist Tracking (for scoring)")]
    public double assistWindowSeconds = 5.0;
    public Dictionary<ulong, ContributorInfo> _contributors = new Dictionary<ulong, ContributorInfo>();

    [Header("Zodiac Animal Models")]
    [SerializeField] GameObject[] zodiacModels;

    void RecordContributor(ulong clientId)
    {
        var info = new ContributorInfo { lastTime = Time.timeAsDouble };
        _contributors[clientId] = info;
    }

    void PruneContributors()
    {
        double now = Time.timeAsDouble;
        var toRemove = new List<ulong>();
        foreach (var kv in _contributors)
            if (now - kv.Value.lastTime > assistWindowSeconds) toRemove.Add(kv.Key);
        foreach (var id in toRemove) _contributors.Remove(id);
    }

    // Example place to record: when computing push directions, for each valid player root:
    void RegisterNearbyPlayersAsContributors(List<Transform> playerRoots)
    {
        foreach (var root in playerRoots)
        {
            var no = root.GetComponent<NetworkObject>();
            if (no && no.IsPlayerObject)
                RecordContributor(no.OwnerClientId);
        }
        PruneContributors();
    }

    public List<ulong> GetRecentContributors()
    {
        PruneContributors();
        return new List<ulong>(_contributors.Keys);
    }

    public void ClearContributors()
    {
        _contributors.Clear();
    }

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        // Optional UI (avoid relying on this on dedicated server)
        // animalStateText = GameObject.FindGameObjectWithTag("AnimalStateText")?.GetComponent<TMP_Text>();

        if (IsServer)
        {
            // Change the animal model
            int index = Random.Range(0, zodiacModels.Length);
            GameObject zodiacInstance = Instantiate(zodiacModels[index], transform.position, transform.rotation);
            // Add or get NetworkObject component
            NetworkObject networkObject = zodiacInstance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                networkObject = zodiacInstance.AddComponent<NetworkObject>();
            }
            networkObject.Spawn();
            //Set the parent after spawning using NetworkObject's method
            networkObject.TrySetParent(transform);

            // Start in agent mode on spawn
            EnterAgentMode();
            agent.speed = baseSpeed;
            _lastPos = transform.position;

            // Reasonable rigidbody defaults (correct Unity API fields)
            _rb.useGravity = true;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0.05f;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    void Update()
    {
        if (!IsServer) return;

        _grounded = CheckGrounded();

        // If airborne, physics owns movement — do not run AI/nav
        if (!_grounded)
        {
            EnterPhysicsMode();
            animalStateTextSafe("Current Animal State: Airborne (physics)");
            _lastPos = transform.position;
            return;
        }

        // Grounded: do perception + state
        Collider[] hits = Physics.OverlapSphere(transform.position, influenceRadius, playerMask, QueryTriggerInteraction.Ignore);

        if (hits.Length == 0)
        {
            if (_physicsMode) TryExitPhysicsMode(forceExit: true); // regain agent if possible
            Wander();
        }
        else
        {
            HerdedByPlayers(hits);
        }

        // Anti-stuck only while the agent is driving
        if (!_physicsMode)
        {
            float moved = Vector3.Distance(transform.position, _lastPos);
            if (moved < minMoveDistance)
            {
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer >= stuckRepathTime)
                {
                    PickRandomDestination();
                    _stuckTimer = 0f;
                }
            }
            else
            {
                _stuckTimer = 0f;
            }
        }

        _lastPos = transform.position;
    }

    // ----------------- AI States -----------------

    void Wander()
    {
        if (_physicsMode) return; // physics owns movement
        EnsureAgentMode();
        animalStateTextSafe("Current Animal State: Wandering");
        agent.speed = baseSpeed;

        _wanderTimer += Time.deltaTime;
        if (_wanderTimer >= wanderInterval || ReachedDestination())
        {
            _wanderTimer = 0f;
            PickRandomDestination();
        }
    }

    void HerdedByPlayers(Collider[] hits)
    {
        // Collapse colliders -> unique player roots, then compute push directions
        var uniqueRoots = new List<Transform>();
        var pushDirs = new List<Vector3>();

        foreach (var h in hits)
        {
            Transform root = h.attachedRigidbody ? h.attachedRigidbody.transform.root : h.transform.root;

            if (!uniqueRoots.Contains(root))
            {
                if (requireNetworkPlayerRoot)
                {
                    var no = root.GetComponent<NetworkObject>();
                    if (no == null || !no.IsPlayerObject) continue; // ignore non-players on the same layer
                }

                uniqueRoots.Add(root);

                Vector3 dir = transform.position - root.position; // away from player
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) dir.Normalize(); else dir = Vector3.zero;
                pushDirs.Add(dir);
            }
        }

        int nearbyPlayers = uniqueRoots.Count;
        if (nearbyPlayers == 0)
        {
            if (_physicsMode) TryExitPhysicsMode(forceExit: true);
            Wander();
            return;
        }

        RegisterNearbyPlayersAsContributors(uniqueRoots);

        // Sum push vectors (away from players)
        Vector3 sumDir = Vector3.zero;
        foreach (var d in pushDirs) sumDir += d;

        if (sumDir.sqrMagnitude < 0.001f)
        {
            if (_physicsMode) TryExitPhysicsMode();
            Wander();
            return;
        }

        Vector3 dominant = sumDir.normalized;

        // Count aligned within cone
        int aligned = 0;
        float cosThresh = Mathf.Cos(alignAngleDegrees * Mathf.Deg2Rad);
        foreach (var d in pushDirs)
        {
            if (Vector3.Dot(dominant, d) >= cosThresh) aligned++;
        }

        // True herd rule: ≥3 players and ≥3 aligned
        bool enoughPlayers = nearbyPlayers >= 2;
        bool alignedGroup = aligned >= 2;
        bool triggerKick = enoughPlayers && alignedGroup;

        if (triggerKick)
        {
            RegisterNearbyPlayersAsContributors(uniqueRoots);
            EnterPhysicsMode();
            animalStateTextSafe("Current Animal State: Physics Kick (aligned herd)");

            // Edge-triggered + cooldown impulse
            bool canImpulse = Time.time >= _nextAllowedImpulseAt;
            if (!_lastFrameWasKick && canImpulse)
            {
                _rb.AddForce(dominant * impulseStrength, ForceMode.VelocityChange);
                _nextAllowedImpulseAt = Time.time + impulseCooldown;
            }
            _lastFrameWasKick = true;
        }
        else
        {
            _lastFrameWasKick = false;

            // Agent-driven steer (slow if not enough aligned helpers)
            if (_physicsMode) TryExitPhysicsMode();

            float speed = aligned >= 2 ? baseSpeed : baseSpeed * slowMultiplier;
            agent.speed = speed;

            animalStateTextSafe(speed == baseSpeed
                ? "Current Animal State: Forces Aligned, Full Speed"
                : "Current Animal State: Not Enough Aligned, Slow");

            Vector3 target = transform.position + dominant * 2.0f; // short step forward
            EnsureAgentMode();

            // Only set destination if the agent is truly active & on a navmesh
            if (agent.enabled && agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(target, out var hit, 2f, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
                else
                    agent.SetDestination(transform.position + dominant);
            }
        }
    }

    // ----------------- Helpers -----------------

    bool CheckGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        return Physics.SphereCast(origin, GroundProbeRadius, Vector3.down, out _, GroundProbeDistance, ~0, QueryTriggerInteraction.Ignore);
    }

    bool ReachedDestination()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return false;
        if (!agent.hasPath) return true;
        if (agent.remainingDistance <= agent.stoppingDistance) return true;
        return false;
    }

    void PickRandomDestination()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        for (int i = 0; i < 5; i++)
        {
            Vector3 random = transform.position + Random.insideUnitSphere * wanderRadius;
            random.y = transform.position.y;
            if (NavMesh.SamplePosition(random, out var hit, 3f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return;
            }
        }
    }

    // ---------- Mode switching ----------

    void EnterPhysicsMode()
    {
        if (_physicsMode) return;

        // Cleanly disable agent
        if (agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            agent.velocity = Vector3.zero;
            agent.enabled = false;
        }

        // Hand control to physics
        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // Preserve current velocity; do not forcibly zero it here

        _physicsMode = true;
        _physicsLockUntil = Time.time + physicsReenableCooldown; // start cooldown
    }

    void TryExitPhysicsMode(bool forceExit = false)
    {
        // Respect cooldown unless forced (e.g., no players)
        if (!forceExit && Time.time < _physicsLockUntil) return;

        // Must be grounded to give control back to nav (unless forced)
        if (!forceExit && !_grounded) return;

        // Also require some settling (unless forced)
        if (!forceExit && _rb.linearVelocity.magnitude > reenableVelocityThreshold) return;

        EnterAgentMode();
    }

    void EnterAgentMode()
    {
        // If not near any navmesh polygon, stay in physics
        if (!NavMesh.SamplePosition(transform.position, out var hit, NavmeshSnapSmall, NavMesh.AllAreas))
        {
            _rb.isKinematic = false;
            _rb.useGravity = true;
            agent.enabled = false;
            _physicsMode = true;
            _physicsLockUntil = Time.time + physicsReenableCooldown;
            return;
        }

        // Hand control to agent
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;   // agent will drive movement
        _rb.useGravity = false;   // avoid double integration while agent owns movement

        if (!agent.enabled) agent.enabled = true;

        agent.Warp(hit.position); // place on mesh
        agent.isStopped = false;
        _physicsMode = false;
    }

    void EnsureAgentMode()
    {
        // Only re-enable if not in physics mode
        if (!_physicsMode && (!agent.enabled || !agent.isOnNavMesh))
        {
            EnterAgentMode();
        }
    }

    void animalStateTextSafe(string s)
    {
        if (animalStateText) animalStateText.text = s;
    }

#if UNITY_EDITOR
    // Optional gizmos for debugging
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, influenceRadius);

        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        Gizmos.DrawWireSphere(origin + Vector3.down * GroundProbeDistance, GroundProbeRadius);
    }
#endif
}
