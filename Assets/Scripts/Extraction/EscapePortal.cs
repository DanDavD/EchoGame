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

        private void Awake()
        {
            if (_portalTrigger == null) _portalTrigger = GetComponent<Collider>();
            if (_portalTrigger != null) _portalTrigger.isTrigger = true;

            if (_portalHumSource == null) _portalHumSource = GetComponent<AudioSource>();

            if (_startActive)
            {
                ActivatePortal();
            }
            else
            {
                DeactivatePortal();
            }
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
                _portalVfxRenderer.material.color = _activeColor;
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
                _portalVfxRenderer.material.color = _inactiveColor;
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

            if (player != null)
            {
                Debug.Log($"<color=green><b>[EscapePortal] Player '{player.name}' reached extraction!</b></color>");
                if (GameExtractionManager.Instance != null)
                {
                    GameExtractionManager.Instance.OnPlayerEscaped(player);
                }
            }
        }
    }
}
