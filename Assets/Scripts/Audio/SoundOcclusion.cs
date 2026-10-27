using UnityEngine;

/// <summary>
/// Makes a 3D sound muffled when walls stand between it and the listener, as if it came from another room, and clear
/// again in the open. It drives a low-pass filter on the same <see cref="AudioSource"/>: the monster's breath and steps,
/// and players' footsteps, so you can tell from the sound alone whether something is in your corridor or behind a wall.
///
/// Only solid level geometry counts: triggers, players (CharacterController), physics objects (relics) and the sound's
/// own body are ignored, so a teammate standing in between does not muffle anything.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class SoundOcclusion : MonoBehaviour
{
    /// <summary>Low-pass cutoff (Hz) with a clear line to the listener: everything passes.</summary>
    public const float ClearCutoff = 22000f;
    /// <summary>Cutoff behind one wall: dull, but you can still make the sound out.</summary>
    public const float OneWallCutoff = 1400f;
    /// <summary>Cutoff behind two walls or more: a distant thud.</summary>
    public const float ThickCutoff = 600f;

    private const float CheckSeconds = 0.12f;
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    private AudioLowPassFilter _filter;
    private Transform _ignoreRoot;
    private float _targetCutoff = ClearCutoff;
    private float _nextCheck;

    /// <summary>How many walls the last check found between the sound and the listener.</summary>
    public int Walls { get; private set; }

    /// <summary>The cutoff currently applied (Hz).</summary>
    public float Cutoff => _filter != null ? _filter.cutoffFrequency : ClearCutoff;

    /// <summary>The low-pass cutoff for a sound behind <paramref name="walls"/> walls.</summary>
    public static float CutoffFor(int walls) => walls <= 0 ? ClearCutoff : walls == 1 ? OneWallCutoff : ThickCutoff;

    private void Awake()
    {
        _filter = GetComponent<AudioLowPassFilter>();
        if (_filter == null) _filter = gameObject.AddComponent<AudioLowPassFilter>();
        _filter.cutoffFrequency = ClearCutoff;
        _filter.lowpassResonanceQ = 1f;
        _ignoreRoot = transform.root;
        _nextCheck = Random.value * CheckSeconds; // spread the raycasts of many sources over several frames
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextCheck)
        {
            _nextCheck = Time.unscaledTime + CheckSeconds;
            if (CaveAmbience.TryGetListenerPosition(out Vector3 listener))
            {
                Walls = CountWalls(transform.position, listener, _ignoreRoot);
                _targetCutoff = CutoffFor(Walls);
            }
        }

        // Glide in log space, so a sound turning a corner fades into the muffle instead of clicking.
        float current = Mathf.Log(Mathf.Max(_filter.cutoffFrequency, 10f));
        float target = Mathf.Log(_targetCutoff);
        _filter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(current, target, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime)));
    }

    /// <summary>Solid level colliders between <paramref name="from"/> and <paramref name="to"/>.</summary>
    public static int CountWalls(Vector3 from, Vector3 to, Transform ignoreRoot)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        if (distance < 0.05f) return 0;

        int count = Physics.RaycastNonAlloc(from, direction / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
        int walls = 0;
        for (int i = 0; i < count; i++)
        {
            Collider collider = Hits[i].collider;
            if (collider == null || collider is CharacterController || collider.attachedRigidbody != null) continue;
            if (ignoreRoot != null && collider.transform.IsChildOf(ignoreRoot)) continue;
            if (collider.GetComponentInParent<PlayerController>() != null) continue;
            walls++;
        }
        return walls;
    }
}
