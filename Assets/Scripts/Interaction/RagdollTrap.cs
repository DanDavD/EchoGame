using UnityEngine;

/// <summary>
/// A trigger plate that launches any player who steps on it into ragdoll mode.
/// Put it on a GameObject with a Collider set to "Is Trigger". Pure comedy, zero damage.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RagdollTrap : MonoBehaviour
{
    [Tooltip("Upward launch speed (m/s).")]
    [SerializeField] private float _upSpeed = 8f;
    [Tooltip("Horizontal launch speed (m/s).")]
    [SerializeField] private float _horizontalSpeed = 5f;
    [Tooltip("Random horizontal direction each time. If off, launches along this object's forward.")]
    [SerializeField] private bool _randomDirection = true;

    private void OnTriggerEnter(Collider other)
    {
        PlayerRagdoll ragdoll = other.GetComponentInParent<PlayerRagdoll>();
        if (ragdoll == null) return;

        // Every peer sees every avatar enter the trigger, but only the owner may simulate its own ragdoll;
        // otherwise remote avatars would turn dynamic and fight their NetworkTransform.
        var player = ragdoll.GetComponent<PlayerController>();
        if (player != null && player.IsSpawned && !player.IsOwner) return;

        Vector3 horizontal = transform.forward;
        if (_randomDirection)
        {
            Vector2 r = Random.insideUnitCircle.normalized;
            horizontal = new Vector3(r.x, 0f, r.y);
        }

        ragdoll.TryRagdoll(horizontal * _horizontalSpeed + Vector3.up * _upSpeed);
    }
}
