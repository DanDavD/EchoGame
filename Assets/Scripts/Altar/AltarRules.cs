using UnityEngine;

namespace EchoProtocol.Altar
{
    /// <summary>
    /// Pure altar rules: which sockets are filled and whether a docking request is valid. No Unity objects and no
    /// networking, so they can be unit tested. <see cref="AltarReceptacle"/> applies them on the server.
    /// The filled sockets are a bit mask: bit <c>i</c> is socket <c>i</c>.
    /// </summary>
    public static class AltarRules
    {
        /// <summary>Largest distance (m) between a player and the socket a relic is docked into.</summary>
        public const float DockRange = 6f;

        /// <summary>An int bit mask holds 31 non-negative socket bits.</summary>
        public const int MaxSockets = 31;

        /// <summary>Number of filled sockets in <paramref name="mask"/>.</summary>
        public static int Count(int mask)
        {
            int count = 0;
            for (int bits = mask; bits != 0; bits &= bits - 1) count++;
            return count;
        }

        public static bool IsValidIndex(int socketIndex) => socketIndex >= 0 && socketIndex < MaxSockets;

        public static bool IsFilled(int mask, int socketIndex) => IsValidIndex(socketIndex) && (mask & (1 << socketIndex)) != 0;

        /// <summary>The mask with <paramref name="socketIndex"/> filled.</summary>
        public static int With(int mask, int socketIndex) => IsValidIndex(socketIndex) ? mask | (1 << socketIndex) : mask;

        /// <summary>True when the symphony should start: enough sockets are filled and it has not started yet.</summary>
        public static bool ShouldComplete(int mask, int required, bool completed) => !completed && required > 0 && Count(mask) >= required;

        /// <summary>
        /// True when a relic may be docked into <paramref name="socketIndex"/>. The caller has already checked that the
        /// player carries the relic. The socket must exist and be empty, the altar must not be complete, the relic must
        /// not already be locked, and the player must be within <see cref="DockRange"/> of the socket.
        /// </summary>
        public static bool CanDock(int mask, int socketCount, int socketIndex, bool completed, bool relicLocked, float distance)
        {
            if (completed || relicLocked) return false;
            if (socketIndex < 0 || socketIndex >= socketCount || !IsValidIndex(socketIndex)) return false;
            if (IsFilled(mask, socketIndex)) return false;
            return distance <= DockRange;
        }
    }
}
