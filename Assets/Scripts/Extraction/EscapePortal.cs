using System.Collections.Generic;
using UnityEngine;
using EchoProtocol.Player;

namespace EchoProtocol.Extraction
{
    /// <summary>
    /// Escape extraction portal located on the perimeter wall of the arena.
    /// Inactive until the Altar Symphony Climax is completed.
    /// When active, walking into its trigger volume completes extraction and wins the match.
    /// </summary>
    public class EscapePortal : MonoBehaviour
    {
        public static readonly List<EscapePortal> AllPortals = new List<EscapePortal>();

        [Header("Portal Settings")]
        [SerializeField] private bool _startActive = false;
        [SerializeField] private Collider _portalTrigger;
        [SerializeField] private Light _portalLight;
        [SerializeField] private MeshRenderer _portalVfxRenderer;
        [SerializeField] private ParticleSystem _portalParticles;
        [SerializeField] private AudioSource _portalHumSource;

        [Header("Colors")]
        [SerializeField] private Color _inactiveColor = new Color(0.15f, 0.15f, 0.2f, 0.2f);
        [SerializeField] private Color _activeColor = new Color(0.2f, 0.95f, 1.0f, 1f);

        private bool _isActive;

        /// <summary>True if the escape portal is currently unlocked and extracting players.</summary>
        public bool IsActive => _isActive;

        private void OnEnable()
        {
            if (!AllPortals.Contains(this)) AllPortals.Add(this);
        }

        private void OnDisable()
        {
            AllPortals.Remove(this);
        }

        // The gateway is drawn with the EchoReveal shader's portal pattern: dormant stone that fills with swirling
        // energy once the portal opens. The pattern is selected here, so a regenerated scene needs no material setup.
        private const float PortalPattern = 4f;

        private void SetPortalEnergy(float energy)
        {
            if (_portalVfxRenderer == null) return;

            Material material = _portalVfxRenderer.material;
            if (material == null || !material.HasProperty("_Pattern")) return;

            material.SetFloat("_Pattern", PortalPattern);
            material.SetFloat("_PortalActive", energy);
        }

        private void Awake()
        {
            if (_portalTrigger == null) _portalTrigger = GetComponent<Collider>();
            if (_portalTrigger != null) _portalTrigger.isTrigger = true;

            if (_portalHumSource == null) _portalHumSource = GetComponent<AudioSource>();
            if (_portalHumSource == null) _portalHumSource = gameObject.AddComponent<AudioSource>();
            _portalHumSource.spatialBlend = 1f;
            _portalHumSource.minDistance = 3f;
            _portalHumSource.maxDistance = 45f;
            _portalHumSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _portalHumSource.loop = true;
            if (_portalHumSource.clip == null)
            {
                _portalHumSource.clip = GeneratePortalHumClip();
            }

            if (_startActive)
            {
                ActivatePortal();
            }
            else
            {
                DeactivatePortal();
            }
        }

        private static AudioClip GeneratePortalHumClip()
        {
            const int sampleRate = 44100;
            float duration = 4f;
            int count = Mathf.RoundToInt(sampleRate * duration);
            var samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float sub = Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.45f;
                float buzz = Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 195f * t) * 0.15f;
                float pulse = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 2f * t);
                samples[i] = (sub + buzz) * pulse * 0.5f;
            }

            var clip = AudioClip.Create("EscapePortalHum", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Activates all existing escape portals in the scene.
        /// </summary>
        public static void ActivateAll()
        {
            for (int i = 0; i < AllPortals.Count; i++)
            {
                if (AllPortals[i] != null)
                {
                    AllPortals[i].ActivatePortal();
                }
            }
        }

        /// <summary>
        /// Deactivates all existing escape portals in the scene.
        /// </summary>
        public static void DeactivateAll()
        {
            for (int i = 0; i < AllPortals.Count; i++)
            {
                if (AllPortals[i] != null)
                {
                    AllPortals[i].DeactivatePortal();
                }
            }
        }

        /// <summary>
        /// Unlocks the portal, enabling its trigger, cyan beacon lights, and audio hum.
        /// </summary>
        public void ActivatePortal()
        {
            _isActive = true;

            if (_portalTrigger != null) _portalTrigger.enabled = true;

            if (_portalLight != null)
            {
                _portalLight.enabled = true;
                _portalLight.color = _activeColor;
                _portalLight.intensity = 14f;
            }

            if (_portalVfxRenderer != null && _portalVfxRenderer.material != null)
            {
                SetPortalEnergy(1f);
            }

            if (_portalParticles != null && !_portalParticles.isPlaying)
            {
                _portalParticles.Play();
            }

            if (_portalHumSource != null && !_portalHumSource.isPlaying)
            {
                _portalHumSource.Play();
            }

            Debug.Log($"<color=cyan><b>[EscapePortal]</b></color> Portal '{gameObject.name}' is now ACTIVE!");
        }

        /// <summary>
        /// Locks the portal, disabling its trigger and turning off the beacon.
        /// </summary>
        public void DeactivatePortal()
        {
            _isActive = false;

            if (_portalTrigger != null) _portalTrigger.enabled = false;

            if (_portalLight != null)
            {
                _portalLight.color = _inactiveColor;
                _portalLight.intensity = 1.0f;
            }

            if (_portalVfxRenderer != null && _portalVfxRenderer.material != null)
            {
                SetPortalEnergy(0f);
            }

            if (_portalParticles != null && _portalParticles.isPlaying)
            {
                _portalParticles.Stop();
            }

            if (_portalHumSource != null && _portalHumSource.isPlaying)
            {
                _portalHumSource.Stop();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive) return;

            var player = other.GetComponent<PlayerController>();
            if (player == null) player = other.GetComponentInParent<PlayerController>();

            // Each peer reports only its own player; the match manager decides the result on the server.
            if (player == null || (player.IsSpawned && !player.IsOwner)) return;

            Debug.Log($"<color=green><b>[EscapePortal] Player '{player.name}' reached extraction!</b></color>");
            if (GameExtractionManager.Instance != null)
            {
                GameExtractionManager.Instance.OnPlayerEscaped(player);
            }
        }
    }
}
