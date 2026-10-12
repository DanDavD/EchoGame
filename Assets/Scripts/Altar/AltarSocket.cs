using UnityEngine;
using EchoProtocol.Interaction;
using EchoProtocol.Player;
using EchoProtocol.Relics;

namespace EchoProtocol.Altar
{
    /// <summary>
    /// Individual relic receptacle socket on the central altar.
    /// Implements IInteractable: allows players carrying a living relic to dock it with [E].
    /// Automatically locks the relic in place, triggers personality placement dialogue,
    /// and reports completion to the parent AltarReceptacle.
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

        private RelicCarryable _dockedRelic;

        /// <summary>True if a relic is currently installed in this socket.</summary>
        public bool IsOccupied => _dockedRelic != null;

        /// <summary>The relic currently locked into this socket, if any.</summary>
        public RelicCarryable DockedRelic => _dockedRelic;

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

            UpdateVisuals();
        }

        /// <inheritdoc/>
        public bool CanInteract(PlayerController player)
        {
            if (IsOccupied || player == null) return false;

            var playerInteraction = player.GetComponent<PlayerInteraction>();
            if (playerInteraction == null || playerInteraction.CarriedRelic == null) return false;

            return !playerInteraction.CarriedRelic.IsLockedInAltar;
        }

        /// <inheritdoc/>
        public void Interact(PlayerController player)
        {
            if (IsOccupied || player == null) return;

            var playerInteraction = player.GetComponent<PlayerInteraction>();
            if (playerInteraction == null || playerInteraction.CarriedRelic == null) return;

            var relic = playerInteraction.CarriedRelic;
            playerInteraction.ClearCarriedRelic(relic);

            DockRelic(relic);
        }

        /// <summary>
        /// Permanently docks the given relic into this socket.
        /// </summary>
        public void DockRelic(RelicCarryable relic)
        {
            if (relic == null || IsOccupied) return;

            _dockedRelic = relic;

            Transform mount = _socketMountPoint != null ? _socketMountPoint : transform;
            _dockedRelic.LockToAltar(mount);

            UpdateVisuals();

            // Trigger comedic placement dialogue
            var brain = relic.GetComponent<RelicBrain>();
            if (brain != null)
            {
                brain.TriggerDialogue(RelicDialogueCategory.AltarPlaced);
            }

            // Notify parent altar receptacle
            if (_parentReceptacle != null)
            {
                _parentReceptacle.NotifySocketFilled(this);
            }

            Debug.Log($"<color=yellow>[AltarSocket]</color> Relic '{relic.RelicName}' successfully docked in socket '{gameObject.name}'.");
        }

        public void SetParentReceptacle(AltarReceptacle receptacle)
        {
            _parentReceptacle = receptacle;
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
