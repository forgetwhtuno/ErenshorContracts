using System;
using System.Linq;

namespace ErenshorContracts
{
    // Pure, Unity-free character-key resolution so it can be unit tested without a live game
    // instance. Mirrors the verified slot-qualified pattern already live-tested in the sibling
    // Erenshor-Nemesis mod (NemesisDirector.ResolveCharacterKey/SafeKey).
    internal static class ContractCharacterKey
    {
        internal static string Resolve(string playerName, int slotIndex)
        {
            return ResolveStrict(playerName, slotIndex, 1, 1);
        }

        // If the game cannot currently verify the active slot, a name-only sidecar is safe only
        // when the save-slot roster independently proves exactly one raw-name match AND exactly
        // one sanitized-key match. The second check prevents names such as "A-B" and "A B" from
        // collapsing onto the same filesystem key. Zero/unknown/multiple evidence fails closed.
        internal static string ResolveStrict(string playerName, int slotIndex, int matchingSaveSlotNames, int matchingSafeSlotKeys)
        {
            if (string.IsNullOrWhiteSpace(playerName)) return string.Empty;
            string safeName = SafeKey(playerName);
            if (string.IsNullOrWhiteSpace(safeName)) return string.Empty;
            if (slotIndex >= 0)
                return "slot" + slotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + safeName;
            if (matchingSaveSlotNames != 1 || matchingSafeSlotKeys != 1) return string.Empty;
            return safeName;
        }

        internal static string SafeKey(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "player" : value;
            return new string(source.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').Take(48).ToArray());
        }
    }
}
