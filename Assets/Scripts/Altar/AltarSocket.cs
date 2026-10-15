using UnityEngine;
using EchoProtocol.Interaction;
using EchoProtocol.Player;

namespace EchoProtocol.Altar
{
    /// <summary>
    /// Individual relic receptacle socket on the central altar. It is a view: whether it is filled comes from the
    /// receptacle, which the server keeps authoritative. Docking goes through <see cref="AltarReceptacle.RequestDock"/>.
    /// </summary>
    public class AltarSocket : MonoBehaviour, IInteractable
    {
        [Header("Mount & References")]
        [Tooltip("Exact transform where the relic will be anchored.")]
        [SerializeField] private Transform _socketMountPoint;
        [Tooltip("The parent altar receptacle managing the symphony event.")]
        [SerializeField] private AltarReceptacle _parentReceptacle;

        [Header("Visual Feedback")]
        [SerializeField] private Light _socketLight;
        [SerializeField] private GameObject _emptyIndicator;
        [SerializeField] private GameObject _filledIndicator;
        [SerializeField] private Color _emptyColor = new Color(0.2f, 0.7f, 1f, 1f);
        [SerializeField] private Color _filledColor = new Color(1f, 0.85f, 0.25f, 1f);

        private int _index = -1;

        /// <summary>Position of this socket in its receptacle, which is also its bit in the filled mask.</summary>
        public int Index => _index;

        /// <summary>Where a docked relic is anchored.</summary>
        public Transform MountPoint => _socketMountPoint != null ? _socketMountPoint : transform;

        /// <summary>True if a relic is currently installed in this socket, as far as this peer knows.</summary>
        public bool IsOccupied => _parentReceptacle != null && _parentReceptacle.IsSocketFilled(_index);

        /// <inheritdoc/>
        public string PromptText => IsOccupied ? "Relic Secured" : "Place Living Relic [E]";

        private void Awake()
        {
            if (_socketMountPoint == null)
            {
                var mount = new GameObject("MountPoint");
                mount.transform.SetParent(transform, false);
                mount.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                _socketMountPoint = mount.transform;
            }

            if (_parentReceptacle == null)
            {
                _parentReceptacle = GetComponentInParent<AltarReceptacle>();
            }
        }

        // Start, not Awake: the receptacle's network state is only safe to read once FishNet has initialised it.
        private void Start()
        {
            UpdateVisuals();
        }

        /// <inheritdoc/>
        public bool CanInteract(PlayerController player)
        {
            return _parentReceptacle != null && _parentReceptacle.CanDockAt(_index, player);
        }

        /// <inheritdoc/>
        public void Interact(PlayerController player)
        {
            if (_parentReceptacle != null) _parentReceptacle.RequestDock(_index, player);
        }

        /// <summary>Attaches this socket to its receptacle at <paramref name="index"/>. Called by the receptacle.</summary>
        public void Bind(AltarReceptacle receptacle, int index)
        {
            _parentReceptacle = receptacle;
            _index = index;
        }

        /// <summary>Redraws the lights and indicators from the receptacle's current state.</summary>
        public void Refresh()
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_socketLight != null)
            {
                _socketLight.color = IsOccupied ? _filledColor : _emptyColor;
                _socketLight.intensity = IsOccupied ? 4.5f : 1.8f;
            }

            if (_emptyIndicator != null) _emptyIndicator.SetActive(!IsOccupied);
            if (_filledIndicator != null) _filledIndicator.SetActive(IsOccupied);
        }
    }
}
