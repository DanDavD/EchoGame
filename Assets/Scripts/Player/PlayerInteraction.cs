using UnityEngine;
using UnityEngine.InputSystem;
using EchoProtocol.Interaction;

namespace EchoProtocol.Player
{
    /// <summary>
    /// Handles player interaction (E to pick up/interact, G to drop),
    /// carry socket management, and auto-drop when ragdolling.
    /// Operates on the local player owner.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private float _interactRange = 2.8f;
        [SerializeField] private LayerMask _interactLayers = ~0; // Everything by default
        [SerializeField] private Transform _carrySocket;

        [Header("Ragdoll Drop Settings")]
        [Tooltip("Horizontal ejection speed when ragdolled (sends relic flying ~50-60m).")]
        [SerializeField] private float _ragdollFlingHorizontal = 26f;
        [Tooltip("Vertical lift applied when ragdolled (launches it sky-high).")]
        [SerializeField] private float _ragdollFlingVertical = 22f;

        private PlayerController _playerController;
        private PlayerRagdoll _playerRagdoll;
        private IInteractable _hoveredInteractable;
        private RelicCarryable _carriedRelic;

        /// <summary>Transform where carried items are anchored.</summary>
        public Transform CarrySocket => _carrySocket;

        /// <summary>The relic currently carried by this player, if any.</summary>
        public RelicCarryable CarriedRelic => _carriedRelic;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _playerRagdoll = GetComponent<PlayerRagdoll>();

            EnsureCarrySocket();
        }

        private void Start()
        {
            if (_playerRagdoll != null)
            {
                _playerRagdoll.OnRagdollStarted += HandleRagdollStarted;
            }
        }

        private void OnDestroy()
        {
            if (_playerRagdoll != null)
            {
                _playerRagdoll.OnRagdollStarted -= HandleRagdollStarted;
            }
        }

        private void EnsureCarrySocket()
        {
            if (_carrySocket != null) return;

            Transform pivot = (_playerController != null && _playerController.CameraPivot != null)
                ? _playerController.CameraPivot
                : transform;

            var socketObj = new GameObject("CarrySocket");
            socketObj.transform.SetParent(pivot, false);
            socketObj.transform.localPosition = new Vector3(0.32f, -0.28f, 0.65f);
            socketObj.transform.localRotation = Quaternion.identity;
            _carrySocket = socketObj.transform;
        }

        public void SetCarriedRelic(RelicCarryable relic)
        {
            _carriedRelic = relic;
        }

        public void ClearCarriedRelic(RelicCarryable relic)
        {
            if (_carriedRelic == relic)
            {
                _carriedRelic = null;
            }
        }

        private void Update()
        {
            // Only the local owner client should process interaction inputs
            if (_playerController == null || !_playerController.IsSpawned || !_playerController.IsOwner) return;

            DetectInteractable();
            HandleInput();
        }

        private void DetectInteractable()
        {
            _hoveredInteractable = null;

            Transform cam = _playerController.CameraPivot != null ? _playerController.CameraPivot : transform;
            Ray ray = new Ray(cam.position, cam.forward);

            if (Physics.SphereCast(ray, 0.25f, out RaycastHit hit, _interactRange, _interactLayers, QueryTriggerInteraction.Ignore))
            {
                // Look for IInteractable on hit collider or parents
                _hoveredInteractable = hit.collider.GetComponentInParent<IInteractable>();
            }
        }

        private void HandleInput()
        {
            if (Keyboard.current == null) return;

            // E key: Interact with hovered object or drop carried relic
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (_hoveredInteractable != null && _hoveredInteractable.CanInteract(_playerController))
                {
                    _hoveredInteractable.Interact(_playerController);
                }
                else if (_carriedRelic != null)
                {
                    DropCarriedRelic();
                }
            }

            // G key: Explicit drop
            if (Keyboard.current.gKey.wasPressedThisFrame && _carriedRelic != null)
            {
                DropCarriedRelic();
            }
        }

        private void DropCarriedRelic()
        {
            if (_carriedRelic == null) return;

            Vector3 throwDir = _playerController.CameraPivot != null ? _playerController.CameraPivot.forward : transform.forward;
            _carriedRelic.Interact(_playerController);
        }

        private void HandleRagdollStarted()
        {
            if (_carriedRelic == null) return;

            // Determine launch direction from monster knockback or camera/body forward
            Vector3 knockback = (_playerRagdoll != null) ? _playerRagdoll.LastLaunchVelocity : Vector3.zero;
            Vector3 hDir;

            if (knockback.sqrMagnitude > 1f)
            {
                hDir = new Vector3(knockback.x, 0f, knockback.z).normalized;
            }
            else
            {
                Vector3 fwd = (_playerController != null && _playerController.CameraPivot != null)
                    ? _playerController.CameraPivot.forward
                    : transform.forward;
                hDir = new Vector3(fwd.x, 0f, fwd.z).normalized;
            }

            if (hDir.sqrMagnitude < 0.01f) hDir = transform.forward;

            // Add slight random horizontal angle dispersion (+-20 degrees)
            Quaternion scatter = Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f);
            hDir = scatter * hDir;

            Vector3 powerfulImpulse = hDir * _ragdollFlingHorizontal + Vector3.up * _ragdollFlingVertical;

            _carriedRelic.ForceDrop(powerfulImpulse);
            _carriedRelic = null;
        }

        private void OnGUI()
        {
            if (_playerController == null || !_playerController.IsSpawned || !_playerController.IsOwner) return;

            int screenW = Screen.width;
            int screenH = Screen.height;

            // Interaction Prompt
            if (_hoveredInteractable != null && _hoveredInteractable.CanInteract(_playerController))
            {
                GUIStyle promptStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                string text = $"[E] {_hoveredInteractable.PromptText}";
                Vector2 size = promptStyle.CalcSize(new GUIContent(text));
                Rect rect = new Rect((screenW - size.x - 24) / 2f, (screenH / 2f) + 45f, size.x + 24, size.y + 8);

                GUI.color = new Color(1f, 0.95f, 0.4f, 0.95f);
                GUI.Box(rect, text, promptStyle);
                GUI.color = Color.white;
            }

            // Carried Item Status
            if (_carriedRelic != null)
            {
                GUIStyle carriedStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };

                GUI.color = new Color(0.4f, 1f, 0.5f, 1f);
                GUI.Label(new Rect(20, screenH - 50, 450, 30), $"Carrying: {_carriedRelic.RelicName}  [G] Drop", carriedStyle);
                GUI.color = Color.white;
            }
        }
    }
}
