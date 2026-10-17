using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;

/// <summary>
/// First-person "clunky on purpose" controller: slow acceleration, heavy jump, weak air control.
/// Uses CharacterController (simple and stable). For ragdoll later: disable this component and
/// the CharacterController, then enable the ragdoll rigidbodies.
/// Networked: Uses FishNet. Only the owner reads input and moves.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset _inputActions;
    [SerializeField] private string _actionMapName = "Player";

    [Header("References")]
    [Tooltip("Child transform holding the camera. Rotates up/down (pitch).")]
    [SerializeField] private Transform _cameraPivot;

    /// <summary>Camera pivot transform for raycasts and view orientation.</summary>
    public Transform CameraPivot => _cameraPivot;

    private static readonly System.Collections.Generic.List<PlayerController> _activePlayers = new System.Collections.Generic.List<PlayerController>();
    /// <summary>Active player controllers currently spawned in the scene.</summary>
    public static System.Collections.Generic.IReadOnlyList<PlayerController> ActivePlayers => _activePlayers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlayerStatics()
    {
        _activePlayers.Clear();
    }

    private void OnEnable()
    {
        if (!_activePlayers.Contains(this)) _activePlayers.Add(this);
    }

    private void OnDisable()
    {
        _activePlayers.Remove(this);
    }

    /// <summary>Horizontal movement speed (m/s).</summary>
    public float CurrentSpeed => _horizontalVelocity.magnitude;

    /// <summary>True if the sprint action is currently held down.</summary>
    public bool IsSprinting => _sprintAction != null && _sprintAction.IsPressed();

    /// <summary>Full 3D movement velocity.</summary>
    public Vector3 CurrentVelocity => _horizontalVelocity + Vector3.up * _verticalVelocity;

    [Header("Movement (clunky on purpose)")]
    [SerializeField] private float _walkSpeed = 3.5f;
    [SerializeField] private float _sprintSpeed = 5.5f;
    [Tooltip("How quickly you reach target speed (low = sluggish).")]
    [SerializeField] private float _acceleration = 8f;
    [Tooltip("How quickly you stop when releasing input (low = slidey).")]
    [SerializeField] private float _deceleration = 10f;
    [Tooltip("Multiplier on acceleration while airborne (0 = none, 1 = full control).")]
    [SerializeField, Range(0f, 1f)] private float _airControl = 0.25f;
    [SerializeField] private float _jumpHeight = 0.9f;
    [SerializeField] private float _gravity = -22f;

    [Header("Look")]
    [Tooltip("Degrees per mouse pixel.")]
    [SerializeField] private float _lookSensitivity = 0.12f;
    [Tooltip("Higher = snappier camera, lower = heavier/laggier camera.")]
    [SerializeField] private float _lookResponsiveness = 25f;
    [SerializeField] private float _pitchLimit = 80f;

    private CharacterController _controller;
    private InputActionMap _map;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;

    private Vector3 _horizontalVelocity;
    private float _verticalVelocity;
    private Vector2 _smoothedLook;
    private float _pitch;
    private bool _controlEnabled = true;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        if (_inputActions == null)
        {
            Debug.LogError("[PlayerController] Assign the InputSystem_Actions asset.", this);
            enabled = false;
            return;
        }

        // Clone InputActionAsset per instance so enabling/disabling maps never cross-affects other players
        _inputActions = Instantiate(_inputActions);
        _map = _inputActions.FindActionMap(_actionMapName, true);
        _moveAction = _map.FindAction("Move", true);
        _lookAction = _map.FindAction("Look", true);
        _jumpAction = _map.FindAction("Jump", true);
        _sprintAction = _map.FindAction("Sprint", true);

        if (GetComponent<EchoProtocol.Player.PlayerInteraction>() == null)
        {
            gameObject.AddComponent<EchoProtocol.Player.PlayerInteraction>();
        }

        // Ensure an AudioListener is active from the very first frame (offline sandbox).
        // Remote clones in multiplayer get theirs disabled again in OnStartClient.
        EnableLocalPlayerCamera();
    }

    private void Start()
    {
        // When running in offline/singleplayer testing mode (unspawned by network),
        // enable the camera and input maps immediately so the scene renders.
        if (!IsSpawned)
        {
            EnableLocalPlayerCamera();
            _map?.Enable();
            SetCursorLocked(true);
        }
    }

    private void EnableLocalPlayerCamera()
    {
        if (_cameraPivot != null)
        {
            Camera cam = _cameraPivot.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.gameObject.SetActive(true);
                cam.enabled = true;
                EchoProtocol.Visuals.CameraPolish.Apply(cam);
                EchoProtocol.Visuals.DustMotes.Attach(cam);
                AudioListener listener = cam.GetComponent<AudioListener>();
                if (listener == null) listener = cam.gameObject.AddComponent<AudioListener>();
                listener.enabled = true;
            }
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        MeshRenderer mr = GetComponent<MeshRenderer>();

        if (base.IsOwner)
        {
            _map?.Enable();
            SetCursorLocked(true);

            // Hide body on local owner so capsule does not clip first-person view
            if (mr != null) mr.enabled = false;
            
            EnableLocalPlayerCamera();
        }
        else
        {
            // Show body for remote players so they are visible to everyone
            if (mr != null) mr.enabled = true;

            _map?.Disable();
            
            // Disable camera and audio listener for remote players
            if (_cameraPivot != null)
            {
                Camera cam = _cameraPivot.GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    cam.enabled = false;
                    cam.gameObject.SetActive(false);
                    AudioListener listener = cam.GetComponent<AudioListener>();
                    if (listener != null) listener.enabled = false;
                }
            }
        }
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (base.IsOwner)
        {
            _map?.Disable();
            SetCursorLocked(false);
        }
    }

    private void Update()
    {
        if (IsSpawned && !base.IsOwner) return;

        // Escape frees the cursor in the editor; click the Game view to lock it again.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !EchoProtocol.UI.EchoGui.PointerOverPanel) SetCursorLocked(true);

        if (!_controlEnabled) return; // ragdolled: physics owns the body, ignore input

        HandleLook();
        HandleMovement();
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // Mouse delta is already "per frame", so it must NOT be multiplied by deltaTime.
        Vector2 raw = _lookAction.ReadValue<Vector2>() * _lookSensitivity;
        _smoothedLook = Vector2.Lerp(_smoothedLook, raw, 1f - Mathf.Exp(-_lookResponsiveness * Time.deltaTime));

        transform.Rotate(0f, _smoothedLook.x, 0f, Space.Self);

        if (_cameraPivot != null)
        {
            _pitch = Mathf.Clamp(_pitch - _smoothedLook.y, -_pitchLimit, _pitchLimit);
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        float dt = Time.deltaTime;
        bool grounded = _controller.isGrounded;

        // Horizontal: accelerate toward the wished velocity (this is what makes it feel clunky).
        Vector2 input = _moveAction.ReadValue<Vector2>();
        Vector3 wishDir = Vector3.ClampMagnitude(transform.right * input.x + transform.forward * input.y, 1f);
        float speed = _sprintAction.IsPressed() ? _sprintSpeed : _walkSpeed;
        Vector3 targetVelocity = wishDir * speed;

        float accel = wishDir.sqrMagnitude > 0.01f ? _acceleration : _deceleration;
        if (!grounded) accel *= _airControl;
        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, accel * dt);

        // Vertical: gravity + jump.
        if (grounded && _verticalVelocity < 0f) _verticalVelocity = -2f; // small push keeps us glued to the floor
        if (grounded && _jumpAction.WasPressedThisFrame())
            _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
        _verticalVelocity += _gravity * dt;

        Vector3 velocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
        _controller.Move(velocity * dt);
    }

    /// <summary>
    /// Suspends or restores player control (input, movement, look) WITHOUT disabling the component,
    /// so the cursor stays locked. Used by the ragdoll system. Re-enabling clears leftover momentum.
    /// </summary>
    public void SetControlEnabled(bool enabled)
    {
        _controlEnabled = enabled;
        if (enabled)
        {
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _smoothedLook = Vector2.zero;
            _pitch = 0f;

            // Enforce completely upright orientation: 0 pitch, 0 roll
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            if (_cameraPivot != null)
            {
                _cameraPivot.localRotation = Quaternion.identity;
            }
        }
    }

    /// <summary>
    /// Replicated launch from server to the local owner when caught by an enemy or trap.
    /// </summary>
    [ObserversRpc]
    public void RpcTriggerRagdoll(Vector3 launchVelocity)
    {
        if (!base.IsOwner) return;
        var ragdoll = GetComponent<PlayerRagdoll>();
        if (ragdoll != null)
        {
            ragdoll.TryRagdoll(launchVelocity);
        }
    }

    /// <summary>
    /// Puts the local player at <paramref name="position"/> looking along <paramref name="yawDegrees"/> and
    /// <paramref name="pitchDegrees"/>, with no leftover momentum. Used by the photo mode; the owner's NetworkTransform
    /// replicates the new position to everybody else.
    /// </summary>
    public void SetView(Vector3 position, float yawDegrees, float pitchDegrees)
    {
        if (_controller != null) _controller.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
        if (_controller != null) _controller.enabled = true;

        _horizontalVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        _smoothedLook = Vector2.zero;
        _pitch = Mathf.Clamp(pitchDegrees, -_pitchLimit, _pitchLimit);
        if (_cameraPivot != null) _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    /// <summary>
    /// Server only: sends this player back to <paramref name="position"/>, for a new match. The owner moves itself, so
    /// the owner's NetworkTransform replicates the new position to everybody else.
    /// </summary>
    public void ServerTeleport(Vector3 position, Quaternion rotation)
    {
        if (!IsServerStarted) return;
        TargetTeleport(Owner, position, rotation);
    }

    [TargetRpc]
    private void TargetTeleport(FishNet.Connection.NetworkConnection connection, Vector3 position, Quaternion rotation)
    {
        var ragdoll = GetComponent<PlayerRagdoll>();
        if (ragdoll != null) ragdoll.ForceRecover();

        _controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        _controller.enabled = true;

        _horizontalVelocity = Vector3.zero;
        _verticalVelocity = 0f;
        SetControlEnabled(true);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
