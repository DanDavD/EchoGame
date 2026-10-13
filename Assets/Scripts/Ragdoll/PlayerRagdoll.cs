using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Turns the first-person player into a physics-driven ragdoll (and back).
/// Version 1 uses the player's capsule as the "body" so it works without models or animations;
/// the public API (TryRagdoll / events) will stay the same when a rigged Mixamo character replaces it.
///
/// While ragdolled: input is suspended, the CharacterController is off, a dynamic Rigidbody tumbles
/// the body and the camera follows it with a damped fraction of the tumble (readable, not nauseating).
/// The microphone and the echo light keep working, so you can scream all the way down.
/// Owner-client only in multiplayer (the server will be told about it separately).
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(PlayerController), typeof(Rigidbody))]
public class PlayerRagdoll : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera pivot (child of the player). Needed for the tumbling camera.")]
    [SerializeField] private Transform _cameraPivot;
    [Tooltip("Optional: plays a loud noise on impact so enemies hear the crash. Found on this object if empty.")]
    [SerializeField] private NoiseBurstEmitter _impactNoise;

    [Header("Physics")]
    [SerializeField] private float _mass = 70f;
    [Tooltip("Random spin (rad/s) added when the ragdoll starts. More = more comedic flailing.")]
    [SerializeField] private float _spin = 6f;
    [SerializeField, Range(0f, 1f)] private float _bounciness = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _friction = 0.6f;

    [Header("Timing")]
    [Tooltip("Minimum seconds on the floor before getting up.")]
    [SerializeField] private float _minDuration = 3.5f;
    [Tooltip("Gets up after this long even if still rolling.")]
    [SerializeField] private float _maxDuration = 8f;
    [Tooltip("Speed (m/s) below which the body counts as settled.")]
    [SerializeField] private float _settleSpeed = 0.5f;
    [Tooltip("Seconds of immunity after getting up (prevents instant re-catching).")]
    [SerializeField] private float _graceSeconds = 3f;
    [Tooltip("Metres the body is lifted when standing up so the capsule does not clip the floor.")]
    [SerializeField] private float _standUpLift = 0.6f;

    [Header("Camera")]
    [Tooltip("0 = camera ignores the tumble, 1 = camera rotates exactly with the body.")]
    [SerializeField, Range(0f, 1f)] private float _cameraTumble = 0.35f;

    [Header("Impact noise")]
    [SerializeField, Range(0f, 1f)] private float _impactNoiseLevel = 0.9f;
    [SerializeField] private float _impactNoiseSeconds = 2.5f;

    [Header("Debug")]
    [Tooltip("Press R in Play mode to ragdoll yourself (test without an enemy).")]
    [SerializeField] private bool _debugKey = true;

    /// <summary>True while physics owns the body.</summary>
    public bool IsRagdolling { get; private set; }

    /// <summary>True during the post-recovery immunity grace period.</summary>
    public bool IsImmune => Time.time < _graceUntil;

    /// <summary>Last launch velocity applied to this ragdoll.</summary>
    public Vector3 LastLaunchVelocity { get; private set; }

    /// <summary>Raised when the ragdoll starts.</summary>
    public event System.Action OnRagdollStarted;

    /// <summary>Raised when the player is back on their feet.</summary>
    public event System.Action OnRecovered;

    private PlayerController _player;
    private CharacterController _characterController;
    private Rigidbody _rb;
    private CapsuleCollider _bodyCollider;

    private float _timer;
    private float _graceUntil;
    private Quaternion _startPivotRotation;
    private Quaternion _startBodyRotation;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _characterController = GetComponent<CharacterController>();
        _rb = GetComponent<Rigidbody>();
        if (_impactNoise == null) _impactNoise = GetComponent<NoiseBurstEmitter>();

        // Rigidbody stays kinematic while the CharacterController drives the player.
        _rb.mass = _mass;
        _rb.linearDamping = 0.15f;
        _rb.angularDamping = 0.8f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.isKinematic = true;

        // Collider used only while ragdolled (the CharacterController is its own capsule).
        _bodyCollider = gameObject.AddComponent<CapsuleCollider>();
        _bodyCollider.height = 2f;
        _bodyCollider.radius = 0.5f;
        _bodyCollider.center = Vector3.zero;
        _bodyCollider.sharedMaterial = new PhysicsMaterial("RagdollBody")
        {
            dynamicFriction = _friction,
            staticFriction = _friction,
            bounciness = _bounciness,
            bounceCombine = PhysicsMaterialCombine.Maximum
        };
        _bodyCollider.enabled = false;
    }

    /// <summary>
    /// Throws the player into ragdoll mode with the given launch velocity (m/s).
    /// Returns false if already ragdolling or still in the post-recovery grace period.
    /// Call this from enemies, traps, falls...
    /// </summary>
    public bool TryRagdoll(Vector3 launchVelocity)
    {
        if (IsRagdolling || Time.time < _graceUntil) return false;

        LastLaunchVelocity = launchVelocity;
        IsRagdolling = true;
        _timer = 0f;

        _player.SetControlEnabled(false);
        _characterController.enabled = false;

        _bodyCollider.enabled = true;
        _rb.isKinematic = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.linearVelocity = launchVelocity;
        _rb.angularVelocity = Random.onUnitSphere * _spin;

        if (_cameraPivot != null)
        {
            _startPivotRotation = _cameraPivot.rotation;
            _startBodyRotation = transform.rotation;
        }

        if (_impactNoise != null) _impactNoise.Trigger(_impactNoiseLevel, _impactNoiseSeconds);

        Debug.Log("[PlayerRagdoll] Ragdoll started.", this);
        OnRagdollStarted?.Invoke();
        return true;
    }

    private void Update()
    {
        // The debug key must only affect the local player, not the networked copies of the other players.
        bool isLocalPlayer = !_player.IsSpawned || _player.IsOwner;
        if (_debugKey && isLocalPlayer && DebugKeys.WasPressed(Key.R))
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            TryRagdoll(new Vector3(dir.x * 6f, 5f, dir.y * 6f));
        }

        if (!IsRagdolling) return;

        _timer += Time.deltaTime;
        bool settled = _rb.linearVelocity.sqrMagnitude < _settleSpeed * _settleSpeed
                       && _rb.angularVelocity.sqrMagnitude < 1f;
        if ((_timer >= _minDuration && settled) || _timer >= _maxDuration) Recover();
    }

    private void LateUpdate()
    {
        if (!IsRagdolling || _cameraPivot == null) return;

        // Apply only a fraction of the body's rotation since the fall started.
        Quaternion bodyDelta = transform.rotation * Quaternion.Inverse(_startBodyRotation);
        Quaternion damped = Quaternion.Slerp(Quaternion.identity, bodyDelta, _cameraTumble);
        _cameraPivot.rotation = damped * _startPivotRotation;
    }

    private void Recover()
    {
        // Velocities can only be written while the body is dynamic, so clear them first.
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
        _bodyCollider.enabled = false;

        // Force strictly upright rotation (0 pitch, 0 roll) facing the camera horizontal look direction
        float yaw = transform.eulerAngles.y;
        if (_cameraPivot != null)
        {
            Vector3 camFwd = _cameraPivot.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 0.01f)
            {
                yaw = Quaternion.LookRotation(camFwd.normalized, Vector3.up).eulerAngles.y;
            }
        }
        Quaternion upright = Quaternion.Euler(0f, yaw, 0f);

        // Raycast downward to place the 2m capsule cleanly above the floor surface
        Vector3 pos = transform.position;
        if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            pos.y = hit.point.y + 1.05f;
        }
        else
        {
            pos.y += _standUpLift;
        }

        transform.SetPositionAndRotation(pos, upright);
        _rb.position = pos;
        _rb.rotation = upright;
        Physics.SyncTransforms();

        if (_cameraPivot != null)
        {
            _cameraPivot.localRotation = Quaternion.identity;
        }

        _characterController.enabled = true;
        _player.SetControlEnabled(true);

        IsRagdolling = false;
        _graceUntil = Time.time + _graceSeconds;

        Debug.Log("[PlayerRagdoll] Back on feet level and upright.", this);
        OnRecovered?.Invoke();
    }
}
