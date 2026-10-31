using UnityEngine;

namespace EchoProtocol.UI
{
    /// <summary>
    /// The menu you open with Esc during a match: which page is open, and who owns the cursor. The match keeps running
    /// (it is multiplayer), but the local player stops moving, looking and interacting while the menu is open.
    ///
    /// The screen that draws it registers through <see cref="Available"/>. Where nothing draws it (the offline sandbox),
    /// Esc keeps its old behaviour and only frees the cursor.
    /// </summary>
    public static class MatchMenu
    {
        public enum Page { Closed, Main, Settings, Controls }

        /// <summary>The page on screen, or <see cref="Page.Closed"/>.</summary>
        public static Page Current { get; private set; } = Page.Closed;

        public static bool IsOpen => Current != Page.Closed;

        /// <summary>True while a screen can draw the menu (during a networked match).</summary>
        public static bool Available { get; private set; }

        /// <summary>
        /// How this game goes back to the main menu (the network session's Leave), set by the screen that owns the
        /// session. Null without a network: the offline sandbox has no menu to go back to.
        /// </summary>
        public static System.Action LeaveHandler { get; set; }

        public static bool CanLeave => LeaveHandler != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = Page.Closed;
            Available = false;
            LeaveHandler = null;
        }

        /// <summary>Leaves the match and returns to the main menu (used by the end screens). Does nothing offline.</summary>
        public static void Leave()
        {
            if (LeaveHandler == null) return;
            Current = Page.Closed;
            LeaveHandler();
        }

        /// <summary>Called by the screen that draws the menu. Turning it off closes the menu without touching the cursor.</summary>
        public static void SetAvailable(bool available)
        {
            Available = available;
            if (!available) Current = Page.Closed;
        }

        /// <summary>Esc: opens the menu, goes back from a sub-page, or closes the main page (back to the game).</summary>
        public static void Escape()
        {
            if (!Available) return;
            switch (Current)
            {
                case Page.Closed: Open(); break;
                case Page.Main: Resume(); break;
                default: Current = Page.Main; break;
            }
        }

        public static void Open()
        {
            if (!Available) return;
            Current = Page.Main;
            SetCursorFree(true);
        }

        public static void Show(Page page)
        {
            if (!Available) return;
            if (page == Page.Closed) { Resume(); return; }
            Current = page;
            SetCursorFree(true);
        }

        /// <summary>Back to the game: closes the menu and locks the cursor for mouse look again.</summary>
        public static void Resume()
        {
            Current = Page.Closed;
            SetCursorFree(false);
        }

        private static void SetCursorFree(bool free)
        {
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }
    }
}
