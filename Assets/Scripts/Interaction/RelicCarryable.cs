using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Component.Transforming;
using EchoProtocol.Player;

namespace EchoProtocol.Interaction
{
    /// <summary>
    /// Handles physical carrying, dropping, and network replication for relics and key items.
    /// Works seamlessly with PlayerInteraction, NetworkTransform, and PlayerRagdoll.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class RelicCarryable : NetworkBehaviour, IInteractable
    {
        [Header("Item Config")]
        [SerializeField] private string _relicName = "Ancient Relic";
        [SerializeField] private float _throwForce = 6f;
        [SerializeField] private Vector3 _carryOffset = new Vector3(0.35f, -0.28f, 0.65f);
        [SerializeField] private Vector3 _carryRotationEuler = new Vector3(0f, 15f, 0f);

        private Rigidbody _rb;
        private Collider _col;
        private NetworkTransform _netTransform;

        /// <summary>Networked reference to the player currently carrying this relic.</summary>
        private readonly SyncVar<NetworkObject> _carrier = new SyncVar<NetworkObject>();
        private GameObject _localCarrierFallback;

        /// <summary>True if this relic is currently in a player's hands.</summary>
        public bool IsCarried => _carrier.Value != null || _localCarrierFallback != null;

        /// <summary>Set by the server when the relic is docked in an altar socket. Replicated, so every peer locks it.</summary>
        private readonly SyncVar<bool> _lockedInAltar = new SyncVar<bool>();
        private bool _lockedLocal;

        /// <summary>True if this relic has been installed into an altar receptacle.</summary>
        public bool IsLockedInAltar => _lockedLocal || _lockedInAltar.Value;

        /// <summary>The player currently holding this relic, or null.</summary>
        public NetworkObject Carrier => _carrier.Value != null ? _carrier.Value : (_localCarrierFallback != null ? _localCarrierFallback.GetComponent<NetworkObject>() : null);

        /// <summary>Public display name of this relic.</summary>
        public string RelicName => _relicName;

        /// <inheritdoc/>
        public string PromptText => IsCarried ? $"Drop {_relicName}" : $"Pick Up {_relicName}";

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();
            _netTransform = GetComponent<NetworkTransform>();

            if (_col != null && _col.sharedMaterial == null)
            {
                _col.sharedMaterial = new PhysicsMaterial("RelicPhysics")
                {
                    dynamicFriction = 0.25f,
                    staticFriction = 0.3f,
                    bounciness = 0.55f,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };
            }

            _carrier.OnChange += OnCarrierChanged;
            _lockedInAltar.OnChange += OnLockChanged;
        }

        private void OnDestroy()
        {
            _carrier.OnChange -= OnCarrierChanged;
            _lockedInAltar.OnChange -= OnLockChanged;
        }

        private void OnLockChanged(bool prev, bool current, bool asServer)
        {
            if (current) ApplyAltarLock();
        }

        /// <summary>Raised whenever this relic is picked up by a player.</summary>
        public event System.Action<NetworkObject> OnPickedUp;

        /// <summary>Raised whenever this relic is dropped or launched.</summary>
        public event System.Action<NetworkObject> OnDropped;

        private void OnCarrierChanged(NetworkObject prev, NetworkObject current, bool asServer)
        {
            // A host receives this callback twice (server side, then client side) for the same object.
            // Handle it once, otherwise pickup/drop events and relic dialogue fire twice.
            if (!asServer && IsServerStarted) return;

            if (current != null)
            {
                // Attached to player
                _col.enabled = false;
                _rb.isKinematic = true;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;

                // Register with carrier's local interaction controller if we are the carrier
                if (current.IsOwner)
                {
                    var playerInteraction = current.GetComponent<PlayerInteraction>();
                    if (playerInteraction != null)
                    {
                        playerInteraction.SetCarriedRelic(this);
                    }
                }

                OnPickedUp?.Invoke(current);
            }
            else
            {
                // Dropped
                _col.enabled = true;
                _rb.isKinematic = false;

                if (prev != null && prev.IsOwner)
                {
                    var playerInteraction = prev.GetComponent<PlayerInteraction>();
                    if (playerInteraction != null)
                    {
                        playerInteraction.ClearCarriedRelic(this);
                    }
                }

                OnDropped?.Invoke(prev);
            }
        }

        private void LateUpdate()
        {
            if (IsLockedInAltar)
            {
                // A replicated drop can briefly make the body dynamic again on a client: keep it docked.
                if (_rb != null && !_rb.isKinematic) ApplyAltarLock();
                return;
            }

            if (!IsCarried) return;

            if (IsServerStarted && (_carrier.Value == null || !_carrier.Value.IsSpawned))
            {
                ServerRequestDrop(Vector3.down * 0.5f);
                return;
            }

            Transform targetSocket = null;
            if (_carrier.Value != null)
            {
                targetSocket = GetCarrierSocket(_carrier.Value);
            }
            else if (_localCarrierFallback != null)
            {
                var pi = _localCarrierFallback.GetComponent<PlayerInteraction>();
                if (pi != null && pi.CarrySocket != null) targetSocket = pi.CarrySocket;
                else targetSocket = _localCarrierFallback.transform;
            }

            if (targetSocket != null)
            {
                // Directly glue to socket position & rotation
                transform.position = targetSocket.TransformPoint(_carryOffset);
                transform.rotation = targetSocket.rotation * Quaternion.Euler(_carryRotationEuler);
            }
        }

        private Transform GetCarrierSocket(NetworkObject carrierNetObj)
        {
            if (carrierNetObj == null) return null;

            var playerInteraction = carrierNetObj.GetComponent<PlayerInteraction>();
            if (playerInteraction != null && playerInteraction.CarrySocket != null)
            {
                return playerInteraction.CarrySocket;
            }

            // Fallback to camera pivot or player root
            var playerController = carrierNetObj.GetComponent<PlayerController>();
            if (playerController != null && playerController.CameraPivot != null)
            {
                return playerController.CameraPivot;
            }

            return carrierNetObj.transform;
        }

        /// <inheritdoc/>
        public bool CanInteract(PlayerController player)
        {
            if (player == null || IsLockedInAltar) return false;

            if (IsCarried)
            {
                if (IsSpawned && player.IsSpawned)
                {
                    return _carrier.Value == player.NetworkObject;
                }
                else
                {
                    return _localCarrierFallback == player.gameObject;
                }
            }

            // Can pick up only if the player isn't already holding a relic
            var pi = player.GetComponent<PlayerInteraction>();
            if (pi != null && pi.CarriedRelic != null) return false;

            return true;
        }

        /// <inheritdoc/>
        public void Interact(PlayerController player)
        {
            if (player == null) return;

            if (IsCarried)
            {
                Vector3 throwDir = player.CameraPivot != null ? player.CameraPivot.forward : player.transform.forward;
                Vector3 impulse = throwDir * _throwForce + Vector3.up * 1.5f;

                if (IsSpawned && player.IsSpawned)
                {
                    ServerRequestDrop(impulse);
                }
                else
                {
                    LocalDrop(impulse);
                }
            }
            else
            {
                if (IsSpawned && player.IsSpawned)
                {
                    ServerRequestPickup(player.NetworkObject);
                }
                else
                {
                    LocalPickup(player);
                }
            }
        }

        private void LocalPickup(PlayerController player)
        {
            _localCarrierFallback = player.gameObject;
            if (_col != null) _col.enabled = false;
            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            var playerInteraction = player.GetComponent<PlayerInteraction>();
            if (playerInteraction != null)
            {
                playerInteraction.SetCarriedRelic(this);
            }

            var netObj = player.GetComponent<NetworkObject>();
            OnPickedUp?.Invoke(netObj);
        }

        private void LocalDrop(Vector3 throwVelocity)
        {
            var prev = _localCarrierFallback;
            _localCarrierFallback = null;

            if (prev != null)
            {
                var playerInteraction = prev.GetComponent<PlayerInteraction>();
                if (playerInteraction != null)
                {
                    playerInteraction.ClearCarriedRelic(this);
                }
            }

            if (_col != null) _col.enabled = true;
            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.linearVelocity = throwVelocity;
                _rb.angularVelocity = Random.onUnitSphere * 4f;
            }

            var netObj = prev != null ? prev.GetComponent<NetworkObject>() : null;
            OnDropped?.Invoke(netObj);
        }

        /// <summary>
        /// Requests the server to attach this relic to the target player.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerRequestPickup(NetworkObject playerNetObj)
        {
            if (playerNetObj == null || IsCarried || IsLockedInAltar) return;

            // Distance check to prevent exploits
            float distSqr = (playerNetObj.transform.position - transform.position).sqrMagnitude;
            if (distSqr > 25f) return;

            _carrier.Value = playerNetObj;
        }

        /// <summary>
        /// Requests the server to drop this relic with a given velocity impulse.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerRequestDrop(Vector3 throwVelocity)
        {
            if (!IsCarried) return;

            // When launching with high velocity (e.g. knocked down by monster), offset slightly forward to prevent clipping carrier
            if (throwVelocity.sqrMagnitude > 80f)
            {
                Vector3 hDir = new Vector3(throwVelocity.x, 0f, throwVelocity.z).normalized;
                transform.position += hDir * 0.55f + Vector3.up * 0.45f;
            }

            _carrier.Value = null;

            // Apply ejection impulse on server
            _rb.isKinematic = false;
            _col.enabled = true;
            _rb.linearVelocity = throwVelocity;
            _rb.angularVelocity = Random.onUnitSphere * (throwVelocity.sqrMagnitude > 80f ? 16f : 4f);
        }

        /// <summary>
        /// Instantly detaches this relic with physics force (called when carrier ragdolls).
        /// </summary>
        public void ForceDrop(Vector3 impulse)
        {
            if (IsSpawned)
            {
                ServerRequestDrop(impulse);
            }
            else
            {
                LocalDrop(impulse);
            }
        }

        /// <summary>
        /// Permanently locks this relic onto an altar socket mount. The server calls it when a docking request is
        /// accepted; without a network the local player's path calls it. The lock replicates to every peer. The relic
        /// is not reparented: a parent change would not replicate, so the server only sets its position.
        /// </summary>
        public void LockToAltar(Transform socketMount)
        {
            if (IsSpawned)
            {
                if (IsServerStarted) _lockedInAltar.Value = true;
            }
            else
            {
                _lockedLocal = true;
            }

            _localCarrierFallback = null;
            if (IsServerStarted)
            {
                _carrier.Value = null;
            }

            transform.SetPositionAndRotation(socketMount.position, socketMount.rotation);
            ApplyAltarLock();
        }

        private void ApplyAltarLock()
        {
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }
            if (_col != null) _col.enabled = false;
        }
    }
}
