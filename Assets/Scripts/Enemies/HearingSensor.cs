using UnityEngine;

/// <summary>
/// The enemy's "ears". Every few frames it evaluates all noise sources in the world and computes:
///  - PerceivedLevel : loudness of the best-heard source AFTER distance attenuation (0..1)
///  - LastHeardPosition : where that noise came from
///  - Suspicion : builds up while noise persists (so constant talking eventually triggers a hunt)
/// It makes NO decisions: the EnemyBrain state machine reads these values.
/// Server-side only in multiplayer.
/// </summary>
public class HearingSensor : MonoBehaviour
{
    [Header("Range")]
    [Tooltip("Distance (m) beyond which nothing can be heard, even a scream.")]
    [SerializeField] private float _maxHearingRange = 40f;

    [Header("Thresholds (on PERCEIVED level, after distance attenuation)")]
    [Tooltip("Above this the enemy notices the sound and goes to investigate.")]
    [SerializeField, Range(0f, 1f)] private float _investigateThreshold = 0.2f;
    [Tooltip("Above this the enemy immediately starts hunting (a scream nearby).")]
    [SerializeField, Range(0f, 1f)] private float _huntThreshold = 0.55f;

    [Header("Suspicion (constant noise)")]
    [Tooltip("Suspicion gained per second at perceived level 1. Suspicion 1 => hunt.")]
    [SerializeField] private float _suspicionGain = 0.6f;
    [Tooltip("Suspicion lost per second while it is quiet.")]
    [SerializeField] private float _suspicionDecay = 0.25f;

    [Header("Performance")]
    [Tooltip("Seconds between hearing scans. 0.1 = 10 times per second.")]
    [SerializeField] private float _updateInterval = 0.1f;

    /// <summary>Loudness of the best-heard source after distance attenuation (0..1).</summary>
    public float PerceivedLevel { get; private set; }

    /// <summary>World position of the last noise loud enough to investigate.</summary>
    public Vector3 LastHeardPosition { get; private set; }

    /// <summary>Builds while noise persists, decays in silence. Reaching 1 triggers a hunt.</summary>
    public float Suspicion { get; private set; }

    /// <summary>True when the current noise is loud enough to be worth checking out.</summary>
    public bool IsInvestigable => PerceivedLevel >= _investigateThreshold;

    /// <summary>True when the noise is a scream nearby, or has been constant for too long.</summary>
    public bool IsHuntTriggered => PerceivedLevel >= _huntThreshold || Suspicion >= 1f;

    /// <summary>Configured max hearing range (used by gizmos / debug).</summary>
    public float MaxHearingRange => _maxHearingRange;

    private float _timer;

    /// <summary>Clears accumulated suspicion (called by the brain when a hunt ends).</summary>
    public void ResetSuspicion()
    {
        Suspicion = 0f;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _updateInterval) return;

        float dt = _timer;
        _timer = 0f;
        Scan(dt);
    }

    private void Scan(float dt)
    {
        Vector3 ears = transform.position;
        float best = 0f;
        Transform bestOwner = null;

        var sources = NoiseRegistry.Sources;
        for (int i = 0; i < sources.Count; i++)
        {
            Transform owner = sources[i].Owner;
            if (owner == null) continue; // emitter destroyed without unregistering
            float distance = Vector3.Distance(ears, owner.position);

            // Linear falloff: full loudness at 0 m, silent at max range.
            // (Walls do not muffle sound yet; occlusion is a possible future improvement.)
            float attenuation = 1f - Mathf.Clamp01(distance / _maxHearingRange);
            float perceived = sources[i].NoiseLevel01 * attenuation;

            if (perceived > best)
            {
                best = perceived;
                bestOwner = owner;
            }
        }

        PerceivedLevel = best;

        if (best >= _investigateThreshold && bestOwner != null)
        {
            LastHeardPosition = bestOwner.position;
            Suspicion += best * _suspicionGain * dt;
        }
        else
        {
            Suspicion -= _suspicionDecay * dt;
        }
        Suspicion = Mathf.Clamp01(Suspicion);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, _maxHearingRange);
    }
}
