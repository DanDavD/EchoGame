using UnityEngine;

/// <summary>
/// Anything in the world that can be HEARD by enemies: a player's voice, footsteps, a falling crate...
/// Enemies never talk to microphones directly; they only query noise sources through this interface.
/// This keeps the AI independent from how a level is produced (local mic, network packet, physics event).
/// </summary>
public interface INoiseSource
{
    /// <summary>The object that makes the noise. Its position is where enemies will walk to.</summary>
    Transform Owner { get; }

    /// <summary>Loudness at the source, from 0 (silent) to 1 (scream).</summary>
    float NoiseLevel01 { get; }
}
