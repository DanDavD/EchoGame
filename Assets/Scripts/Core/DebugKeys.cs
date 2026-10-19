using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single gate for prototype debug shortcuts (J, H, T, Y, M, debug ragdoll).
/// They only work in the Editor and in Development builds, so a release build never exposes cheats.
/// </summary>
public static class DebugKeys
{
    /// <summary>True when debug shortcuts are compiled in (Editor or Development build).</summary>
    public const bool Enabled =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        true;
#else
        false;
#endif

    /// <summary>True on the frame <paramref name="key"/> went down, always false in release builds.</summary>
    public static bool WasPressed(Key key)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].wasPressedThisFrame;
#else
        return false;
#endif
    }
}
