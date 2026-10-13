using System.Collections.Generic;
using UnityEngine;
using EchoProtocol.Relics;

namespace EchoProtocol.Level
{
    /// <summary>What a <see cref="SpawnPoint"/> spawns.</summary>
    public enum SpawnKind
    {
        Player = 0,
        Relic = 1,
        Enemy = 2
    }

    /// <summary>
    /// Marker placed by the level (hand-made or generated). Gameplay systems ask <see cref="SpawnRegistry"/>
    /// where to spawn things instead of hardcoding world positions, so any level can plug into the same match flow.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private SpawnKind _kind;
        [Tooltip("Only used by Relic spawn points: which relic personality appears here.")]
        [SerializeField] private RelicPersonalityType _relicPersonality;

        /// <summary>What spawns here.</summary>
        public SpawnKind Kind => _kind;

        /// <summary>Personality of the relic spawned here (Relic spawn points only).</summary>
        public RelicPersonalityType RelicPersonality => _relicPersonality;

        /// <summary>Configures a freshly created spawn point (used by the level builder).</summary>
        public void Configure(SpawnKind kind, RelicPersonalityType relicPersonality = default)
        {
            _kind = kind;
            _relicPersonality = relicPersonality;
        }

        private void OnEnable() => SpawnRegistry.Register(this);

        private void OnDisable() => SpawnRegistry.Unregister(this);

        private void OnDrawGizmos()
        {
            switch (_kind)
            {
                case SpawnKind.Player: Gizmos.color = Color.cyan; break;
                case SpawnKind.Relic: Gizmos.color = new Color(1f, 0.8f, 0.2f); break;
                default: Gizmos.color = Color.red; break;
            }
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawRay(transform.position, transform.forward * 1.2f);
        }
    }

    /// <summary>Global lookup of the active spawn points, grouped by kind.</summary>
    public static class SpawnRegistry
    {
        private static readonly List<SpawnPoint> Points = new List<SpawnPoint>();

        internal static void Register(SpawnPoint point)
        {
            if (point != null && !Points.Contains(point)) Points.Add(point);
        }

        internal static void Unregister(SpawnPoint point) => Points.Remove(point);

        /// <summary>Active spawn points of a kind, ordered by object name so the order is stable across runs.</summary>
        public static List<SpawnPoint> Get(SpawnKind kind)
        {
            var result = new List<SpawnPoint>();
            for (int i = 0; i < Points.Count; i++)
            {
                if (Points[i] != null && Points[i].Kind == kind) result.Add(Points[i]);
            }
            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        /// <summary>First spawn point of a kind, or null when the level defines none.</summary>
        public static SpawnPoint First(SpawnKind kind)
        {
            var all = Get(kind);
            return all.Count > 0 ? all[0] : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Points.Clear();
    }
}
