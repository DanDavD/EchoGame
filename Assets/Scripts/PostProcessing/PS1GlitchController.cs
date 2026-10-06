using UnityEngine;

/// <summary>
/// Triggers a screen tearing/VHS glitch effect when the player ragdolls.
/// Communicates with PS1RetroFeature via a global shader property so it doesn't dirty the URP asset.
/// </summary>
public class PS1GlitchController : MonoBehaviour
{
    [Tooltip("Peak glitch intensity when ragdolled.")]
    [SerializeField] private float _peakGlitch = 0.8f;
    [Tooltip("How fast the glitch decays back to normal (units per second).")]
    [SerializeField] private float _decayRate = 0.5f;

    private PlayerRagdoll _ragdoll;
    private float _currentGlitch = 0f;

    private void Awake()
    {
        _ragdoll = GetComponentInParent<PlayerRagdoll>();
    }

    private void OnEnable()
    {
        if (_ragdoll != null)
            _ragdoll.OnRagdollStarted += HandleRagdoll;
    }

    private void OnDisable()
    {
        if (_ragdoll != null)
            _ragdoll.OnRagdollStarted -= HandleRagdoll;
        
        // Ensure we don't leave the screen permanently glitched if disabled
        Shader.SetGlobalFloat("_PS1_GlitchIntensity", 0f);
    }

    private void HandleRagdoll()
    {
        _currentGlitch = _peakGlitch;
    }

    private void Update()
    {
        if (_currentGlitch > 0f)
        {
            _currentGlitch = Mathf.MoveTowards(_currentGlitch, 0f, _decayRate * Time.deltaTime);
            Shader.SetGlobalFloat("_PS1_GlitchIntensity", _currentGlitch);
        }
    }
}
