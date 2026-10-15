/// <summary>
/// Whether the on-screen debug readouts (microphone bar, monster state, networking panel once connected, light/freeze
/// banners) are drawn. They are on in the Editor and in Development builds, always off in a release build, and F3
/// toggles them while developing. The photo mode turns them off so screenshots show only the real game.
/// </summary>
public static class DebugOverlay
{
    /// <summary>True while the debug readouts should be drawn.</summary>
    public static bool Visible { get; set; } = DebugKeys.Enabled;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Visible = DebugKeys.Enabled;
    }

    /// <summary>Flips visibility. Does nothing in a release build.</summary>
    public static void Toggle()
    {
        if (DebugKeys.Enabled) Visible = !Visible;
    }
}
