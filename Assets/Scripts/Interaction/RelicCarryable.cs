using System.Collections.Generic;
using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Component.Transforming;
using EchoProtocol.Player;

namespace EchoProtocol.Interaction
{
    /// <summary>
    /// Handles physical carrying, dropping, and network replication for relics and key items.
    /// Works seamlessly with PlayerInteraction, NetworkTransform, and PlayerRagdoll.
    /// A carried relic can also be handed straight to a teammate standing close by ("pass the baby").
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class RelicCarryable : NetworkBehaviour, IInteractable
    {
        [Header("Item Config")]
        [SerializeField] private string _relicName = "Ancient Relic";
        [SerializeField] private float _throwForce = 6f;
        [SerializeField] private Vector3 _carryOffset = new Vector3(0.35f, -0.28f, 0.65f);
        [SerializeField] private Vector3 _carryRotationEuler = new Vector3(0f, 15f, 0f);
        [Tooltip("Farthest distance (m) between two players for a hand-off.")]
        [SerializeField] private float _passRange = 3f;

        private static readonly List<RelicCarryable> _all = new List<RelicCarryable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _all.Clear();

        /// <summary>Farthest distance (m) between two players for a hand-off.</summary>
        public float PassRange => _passRange;

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

        private void OnEnable()
        {
            if (!_all.Contains(this)) _all.Add(this);
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }

        /// <summary>True when <paramref name="player"/> holds any relic, as seen by this peer.</summary>
        public static bool IsCarryingAny(GameObject player)
        {
            if (player == null) return false;
            for (int i = 0; i < _all.Count; i++)
            {
                RelicCarryable relic = _all[i];
                if (relic == null) continue;
                NetworkObject carrier = relic.Carrier;
                if (carrier != null && carrier.gameObject == player) return true;
                if (relic._localCarrierFallback == player) return true;
            }
            return false;
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
                // Handed straight from one player to another: the giver's hands are empty now.
                if (prev != null && prev != current && prev.IsOwner)
                {
                    var giverInteraction = prev.GetComponent<PlayerInteraction>();
                    if (giverInteraction != null) giverInteraction.ClearCarriedRelic(this);
                }

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
                ServerDrop(Vector3.down * 0.5f);
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
        /// True when <paramref name="giver"/> holds this relic and can hand it to <paramref name="receiver"/>: a different
        /// player, close enough, with empty hands.
        /// </summary>
        public bool CanPassTo(PlayerController giver, PlayerController receiver)
        {
            if (giver == null || receiver == null || giver == receiver || IsLockedInAltar || !IsCarried) return false;
            if (!CanInteract(giver)) return false; // only the carrier can pass it
            if (IsCarryingAny(receiver.gameObject)) return false;

            return (receiver.transform.position - giver.transform.position).sqrMagnitude <= _passRange * _passRange;
        }

        /// <summary>Hands the relic from <paramref name="giver"/> to <paramref name="receiver"/>. The server decides on a network.</summary>
        public void PassTo(PlayerController giver, PlayerController receiver)
        {
            if (!CanPassTo(giver, receiver)) return;

            if (IsSpawned && receiver.IsSpawned)
            {
                ServerRequestPass(receiver.NetworkObject);
                return;
            }

            // Offline: move the relic between the two local players.
            var giverInteraction = giver.GetComponent<PlayerInteraction>();
            if (giverInteraction != null) giverInteraction.ClearCarriedRelic(this);

            _localCarrierFallback = receiver.gameObject;
            var receiverInteraction = receiver.GetComponent<PlayerInteraction>();
            if (receiverInteraction != null) receiverInteraction.SetCarriedRelic(this);

            OnPickedUp?.Invoke(receiver.GetComponent<NetworkObject>());
        }

        /// <summary>
        /// Requests the server to move this relic from its carrier to <paramref name="receiver"/>. Only the carrier's own
        /// client may ask, and the server checks the distance and that the receiver's hands are empty.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void ServerRequestPass(NetworkObject receiver, NetworkConnection caller = null)
        {
            NetworkObject giver = _carrier.Value;
            if (giver == null || receiver == null || !receiver.IsSpawned || receiver == giver || IsLockedInAltar) return;
            if (caller != null && giver.Owner != caller) return;
            if (IsCarryingAny(receiver.gameObject)) return;

            // A little slack over the client's own check: the two positions arrive with network delay.
            float range = _passRange + 1f;
            if ((receiver.transform.position - giver.transform.position).sqrMagnitude > range * range) return;

            _carrier.Value = receiver;
        }

        /// <summary>
        /// Requests the server to attach this relic to the target player. Only that player's own client may ask, and
        /// only with empty hands.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerRequestPickup(NetworkObject playerNetObj, NetworkConnection caller = null)
        {
            if (playerNetObj == null || !playerNetObj.IsSpawned || IsCarried || IsLockedInAltar) return;
            if (caller != null && playerNetObj.Owner != caller) return;
            if (IsCarryingAny(playerNetObj.gameObject)) return;

            // Distance check to prevent exploits
            float distSqr = (playerNetObj.transform.position - transform.position).sqrMagnitude;
            if (distSqr > 25f) return;

            _carrier.Value = playerNetObj;
        }

        /// <summary>
        /// Requests the server to drop this relic with a given velocity impulse. Only the carrier's own client may ask.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ServerRequestDrop(Vector3 throwVelocity, NetworkConnection caller = null)
        {
            if (caller != null && _carrier.Value != null && _carrier.Value.Owner != caller) return;
            ServerDrop(throwVelocity);
        }

        /// <summary>Server only: lets go of the relic with an impulse.</summary>
        private void ServerDrop(Vector3 throwVelocity)
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
