using UnityEngine;

/// <summary>
/// What happens when THIS enemy catches a player: knock them into ragdoll and gloat for a few seconds.
/// Kept separate from EnemyBrain so the AI knows nothing about ragdolls (and vice versa).
/// Server-side in multiplayer: the server decides the catch and tells the victim's client.
/// </summary>
[RequireComponent(typeof(EnemyBrain))]
public class EnemyCatchReaction : MonoBehaviour
{
    [Tooltip("Horizontal launch speed (m/s) applied to the victim, away from the enemy.")]
    [SerializeField] private float _knockbackSpeed = 9f;
    [Tooltip("Upward launch speed (m/s) so the victim goes flying.")]
    [SerializeField] private float _upSpeed = 4.5f;
    [Tooltip("Seconds the enemy stands still after a successful catch.")]
    [SerializeField] private float _stunSeconds = 3f;

    private EnemyBrain _brain;

    private void Awake()
    {
        _brain = GetComponent<EnemyBrain>();
    }

    private void OnEnable()
    {
        _brain.OnPlayerCaught += HandleCaught;
    }

    private void OnDisable()
    {
        _brain.OnPlayerCaught -= HandleCaught;
    }

    private void HandleCaught(Transform victim)
    {
        PlayerRagdoll ragdoll = victim.GetComponentInParent<PlayerRagdoll>();
        if (ragdoll == null) return;

        Vector3 away = victim.position - transform.position;
        away.y = 0f;
        away = away.sqrMagnitude < 0.01f ? transform.forward : away.normalized;

        // TryRagdoll fails during the victim's grace period; the enemy then keeps hunting instead of freezing.
        if (ragdoll.TryRagdoll(away * _knockbackSpeed + Vector3.up * _upSpeed))
            _brain.Stun(_stunSeconds);
    }
}
