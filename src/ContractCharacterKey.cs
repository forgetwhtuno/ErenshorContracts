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
            string safeName = SafeKey(playerName);
            return slotIndex >= 0 ? "slot" + slotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + safeName : safeName;
        }

        internal static string SafeKey(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "player" : value;
            return new string(source.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').Take(48).ToArray());
        }
    }
}
