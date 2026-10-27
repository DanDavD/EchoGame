using UnityEngine;

/// <summary>
/// Footsteps on stone: soft when walking, louder and faster when sprinting, silent in the air and while ragdolling.
/// Driven by the player's horizontal speed, so remote players (moved by their NetworkTransform) are heard too, and in
/// 3D, so you can tell where your teammates are in the dark. Purely cosmetic: the monster does not hear it.
/// </summary>
[DisallowMultipleComponent]
public class FootstepAudio : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float _volume = 0.45f;
    [Tooltip("Metres covered by one step, used to space the footsteps.")]
    [SerializeField] private float _strideMetres = 1.9f;
    [SerializeField] private float _walkSpeed = 3.5f;
    [SerializeField] private float _sprintSpeed = 5.5f;
    [SerializeField] private float _hearDistance = 30f;

    private AudioSource _source;
    private PlayerRagdoll _ragdoll;
    private Vector3 _lastPosition;
    private float _speed;
    private float _verticalSpeed;
    private float _stepTimer;
    private int _step;

    /// <summary>How many steps this player has played (for tests and debugging).</summary>
    public int StepsPlayed { get; private set; }

    private void Awake()
    {
        _source = ProceduralAudio.CreateSource(transform, "Footsteps", _hearDistance, _volume, minDistance: 2f);
        _source.transform.localPosition = new Vector3(0f, -0.9f, 0f); // at the feet
        _source.gameObject.AddComponent<SoundOcclusion>(); // a teammate behind a wall sounds like it
        _ragdoll = GetComponent<PlayerRagdoll>();
        _lastPosition = transform.position;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 delta = transform.position - _lastPosition;
        _lastPosition = transform.position;
        _verticalSpeed = Mathf.Lerp(_verticalSpeed, Mathf.Abs(delta.y) / dt, 1f - Mathf.Exp(-10f * dt));
        delta.y = 0f;
        float instant = delta.magnitude / dt;
        if (instant > 20f) instant = _speed; // a respawn or a ragdoll recovery is not a step
        _speed = Mathf.Lerp(_speed, instant, 1f - Mathf.Exp(-10f * dt));

        bool airborne = _verticalSpeed > 1.5f;
        bool tumbling = _ragdoll != null && _ragdoll.IsRagdolling;
        float interval = airborne || tumbling ? 0f : MovementSounds.StepInterval(_speed, _strideMetres);
        if (interval <= 0f)
        {
            _stepTimer = 0.08f; // the first step after stopping comes quickly
            return;
        }

        _stepTimer -= dt;
        if (_stepTimer > 0f) return;

        _stepTimer = interval;
        _step = (_step + 1) % 4;
        StepsPlayed++;
        _source.pitch = 0.92f + Random.value * 0.16f;
        _source.PlayOneShot(ProceduralAudio.PlayerStep(_step), MovementSounds.StepVolume(_speed, _walkSpeed, _sprintSpeed));
    }
}
