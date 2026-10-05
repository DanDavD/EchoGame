/// <summary>
/// Anything that can report how loud a player is speaking right now.
/// Today: local microphone (MicrophoneLoudness).
/// Later: the voice-chat plugin pipeline, or a networked value received from a remote client.
/// Consumers (EchoVisionController, enemy hearing...) depend on this, never on a concrete mic class.
/// </summary>
public interface IVoiceLevelSource
{
    /// <summary>Smoothed, calibrated loudness in the range 0 (silence) to 1 (scream).</summary>
    float Level01 { get; }
}
