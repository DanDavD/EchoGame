using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person "clunky on purpose" controller: slow acceleration, heavy jump, weak air control.
/// Uses CharacterController (simple and stable). For ragdoll later: disable this component and
/// the CharacterController, then enable the ragdoll rigidbodies.
/// Local-only for now; the networking layer will gate this behind IsOwner.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset _inputActions;
    [SerializeField] private string _actionMapName = "Player";

    [Header("References")]
    [Tooltip("Child transform holding the camera. Rotates up/down (pitch).")]
    [SerializeField] private Transform _cameraPivot;

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

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        if (_inputActions == null)
        {
            Debug.LogError("[PlayerController] Assign the InputSystem_Actions asset.", this);
            enabled = false;
            return;
        }

        _map = _inputActions.FindActionMap(_actionMapName, true);
        _moveAction = _map.FindAction("Move", true);
        _lookAction = _map.FindAction("Look", true);
        _jumpAction = _map.FindAction("Jump", true);
        _sprintAction = _map.FindAction("Sprint", true);

        if (_cameraPivot == null)
            Debug.LogWarning("[PlayerController] No camera pivot assigned: looking up/down is disabled.", this);
    }

    private void OnEnable()
    {
        _map?.Enable();
        SetCursorLocked(true);
    }

    private void OnDisable()
    {
        _map?.Disable();
        SetCursorLocked(false);
    }

    private void Update()
    {
        // Escape frees the cursor in the editor; click the Game view to lock it again.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) SetCursorLocked(true);

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

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
