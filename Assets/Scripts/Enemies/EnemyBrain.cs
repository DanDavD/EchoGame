using UnityEngine;
using UnityEngine.AI;

/// <summary>States of the blind enemy.</summary>
public enum EnemyState
{
    /// <summary>Wanders slowly around its home area.</summary>
    Idle,
    /// <summary>Heard something: walks to where the noise came from and looks around.</summary>
    Investigate,
    /// <summary>Scream or constant noise: sprints towards the noise.</summary>
    Hunt
}

/// <summary>
/// Finite state machine for a blind monster with perfect hearing.
/// Idle -> Investigate -> Hunt, driven only by what the HearingSensor reports.
/// Movement uses a NavMeshAgent. Server-side only in multiplayer.
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(HearingSensor))]
public class EnemyBrain : MonoBehaviour
{
    [Header("Speeds (m/s)")]
    [SerializeField] private float _wanderSpeed = 1.2f;
    [SerializeField] private float _investigateSpeed = 2.5f;
    [SerializeField] private float _huntSpeed = 6f;

    [Header("Idle")]
    [Tooltip("Max distance from its starting point that it will wander.")]
    [SerializeField] private float _wanderRadius = 15f;
    [SerializeField] private float _pauseMin = 2f;
    [SerializeField] private float _pauseMax = 5f;

    [Header("Investigate")]
    [Tooltip("Seconds spent looking around after reaching the noise position.")]
    [SerializeField] private float _searchDuration = 3f;
    [Tooltip("Degrees per second it turns while searching.")]
    [SerializeField] private float _searchTurnSpeed = 90f;
    [SerializeField] private float _arriveTolerance = 0.6f;

    [Header("Hunt")]
    [Tooltip("Seconds without hearing anything before giving up the hunt.")]
    [SerializeField] private float _loseTargetTime = 4f;
    [Tooltip("Distance at which a player counts as caught.")]
    [SerializeField] private float _catchDistance = 1.6f;
    [Tooltip("Minimum seconds between two catch events.")]
    [SerializeField] private float _catchCooldown = 3f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugLabel = true;

    /// <summary>Current state of the state machine.</summary>
    public EnemyState State { get; private set; } = EnemyState.Idle;

    /// <summary>Raised on every transition: (previous, new).</summary>
    public event System.Action<EnemyState, EnemyState> OnStateChanged;

    /// <summary>Raised when the enemy reaches a player. The ragdoll system will subscribe to this later.</summary>
    public event System.Action<Transform> OnPlayerCaught;

    private NavMeshAgent _agent;
    private HearingSensor _sensor;
    private NavMeshPath _path;

    private Vector3 _home;
    private Vector3 _currentTarget;
    private bool _idleWaiting;
    private bool _searching;
    private float _timer;
    private float _lostTimer;
    private float _nextCatchTime;
    private float _stunUntil;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _sensor = GetComponent<HearingSensor>();
        _path = new NavMeshPath();
    }

    private void Start()
    {
        _home = transform.position;

        // The NavMesh may have been created after the agent was enabled. Re-enable to attach it.
        _agent.enabled = false;
        _agent.enabled = true;
        if (!_agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            _agent.Warp(hit.position);

        if (!_agent.isOnNavMesh)
        {
            Debug.LogError("[EnemyBrain] Agent is not on a NavMesh. Bake/build the NavMesh first.", this);
            enabled = false;
            return;
        }

        EnterState(EnemyState.Idle);
    }

    private void Update()
    {
        if (!_agent.isOnNavMesh) return;

        if (_stunUntil > 0f)
        {
            if (Time.time < _stunUntil) return; // gloating over the victim
            _stunUntil = 0f;
            _agent.isStopped = false;
        }

        TryCatchPlayers();

        switch (State)
        {
            case EnemyState.Idle: TickIdle(); break;
            case EnemyState.Investigate: TickInvestigate(); break;
            case EnemyState.Hunt: TickHunt(); break;
        }
    }

    // ---------------------------------------------------------------- state transitions
    private void EnterState(EnemyState next)
    {
        EnemyState previous = State;

        // After a hunt ends, forget accumulated suspicion so we do not instantly re-hunt.
        if (previous == EnemyState.Hunt && next != EnemyState.Hunt) _sensor.ResetSuspicion();

        State = next;
        _searching = false;
        _idleWaiting = false;
        _timer = 0f;
        _lostTimer = 0f;

        switch (next)
        {
            case EnemyState.Idle:
                _agent.speed = _wanderSpeed;
                _agent.ResetPath();
                _idleWaiting = true;
                _timer = 0.5f;
                break;

            case EnemyState.Investigate:
                _agent.speed = _investigateSpeed;
                MoveTo(_sensor.LastHeardPosition);
                break;

            case EnemyState.Hunt:
                _agent.speed = _huntSpeed;
                MoveTo(_sensor.LastHeardPosition);
                break;
        }

        OnStateChanged?.Invoke(previous, next);
    }

    // ---------------------------------------------------------------- state ticks
    private void TickIdle()
    {
        if (_sensor.IsHuntTriggered) { EnterState(EnemyState.Hunt); return; }
        if (_sensor.IsInvestigable) { EnterState(EnemyState.Investigate); return; }

        if (_idleWaiting)
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            _idleWaiting = false;
            if (!TryWander()) { _idleWaiting = true; _timer = 1f; } // nothing reachable, retry shortly
            return;
        }

        if (ReachedDestination())
        {
            _idleWaiting = true;
            _timer = Random.Range(_pauseMin, _pauseMax);
            _agent.ResetPath();
        }
    }

    private void TickInvestigate()
    {
        if (_sensor.IsHuntTriggered) { EnterState(EnemyState.Hunt); return; }

        if (_sensor.IsInvestigable)
        {
            // Fresh noise: stop searching and follow it.
            _searching = false;
            RefreshTarget(_sensor.LastHeardPosition);
        }

        if (_searching)
        {
            // Arrived and heard nothing new: look around, then go back to wandering.
            _timer -= Time.deltaTime;
            transform.Rotate(0f, _searchTurnSpeed * Time.deltaTime, 0f);
            if (_timer <= 0f) EnterState(EnemyState.Idle);
            return;
        }

        if (ReachedDestination())
        {
            _searching = true;
            _timer = _searchDuration;
            _agent.ResetPath();
        }
    }

    private void TickHunt()
    {
        if (_sensor.IsInvestigable)
        {
            _lostTimer = 0f;
            RefreshTarget(_sensor.LastHeardPosition);
        }
        else
        {
            _lostTimer += Time.deltaTime;
        }

        // Lost the target: check the last known position before giving up.
        if (_lostTimer >= _loseTargetTime) EnterState(EnemyState.Investigate);
    }

    // ---------------------------------------------------------------- helpers
    /// <summary>
    /// Freezes the enemy in place for a few seconds (e.g. after catching a player), then it resumes its state.
    /// </summary>
    public void Stun(float seconds)
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _stunUntil = Time.time + seconds;
    }

    /// <summary>Reports a catch when the enemy touches any noise owner (blind, but not numb).</summary>
    private void TryCatchPlayers()
    {
        if (Time.time < _nextCatchTime) return;

        var sources = NoiseRegistry.Sources;
        float sqrCatch = _catchDistance * _catchDistance;
        for (int i = 0; i < sources.Count; i++)
        {
            Transform owner = sources[i].Owner;
            Vector3 delta = owner.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > sqrCatch) continue;

            _nextCatchTime = Time.time + _catchCooldown;
            Debug.Log($"[EnemyBrain] {name} caught {owner.name}!", this);
            OnPlayerCaught?.Invoke(owner);
            return;
        }
    }

    private bool TryWander()
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = Random.insideUnitCircle * _wanderRadius;
            Vector3 candidate = _home + new Vector3(offset.x, 0f, offset.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
            if ((hit.position - transform.position).sqrMagnitude < 4f) continue; // too close to bother

            // Skip unreachable spots (e.g. the top of a pillar).
            if (!_agent.CalculatePath(hit.position, _path) || _path.status != NavMeshPathStatus.PathComplete) continue;

            MoveTo(hit.position);
            return true;
        }
        return false;
    }

    private void MoveTo(Vector3 position)
    {
        _currentTarget = position;
        _agent.SetDestination(position);
    }

    /// <summary>Re-targets only when the noise moved enough (or we have no path), to avoid spamming path requests.</summary>
    private void RefreshTarget(Vector3 position)
    {
        bool moved = (position - _currentTarget).sqrMagnitude > 0.25f;
        bool idle = !_agent.hasPath && !_agent.pathPending;
        if (moved || idle) MoveTo(position);
    }

    private bool ReachedDestination()
    {
        return !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + _arriveTolerance;
    }

    // ---------------------------------------------------------------- debug
    private void OnGUI()
    {
        if (!_showDebugLabel) return;
        GUI.color = State == EnemyState.Hunt ? Color.red : (State == EnemyState.Investigate ? Color.yellow : Color.white);
        GUI.Label(new Rect(10f, 54f, 500f, 22f),
            $"Enemy: {State}  |  heard {_sensor.PerceivedLevel:F2}  |  suspicion {_sensor.Suspicion:F2}");
        GUI.color = Color.white;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 home = Application.isPlaying ? _home : transform.position;
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.3f);
        Gizmos.DrawWireSphere(home, _wanderRadius);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _currentTarget);
            Gizmos.DrawWireSphere(_currentTarget, 0.4f);
        }
    }
}
