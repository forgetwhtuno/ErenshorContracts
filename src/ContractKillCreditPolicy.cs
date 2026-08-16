using System;
using System.Text.RegularExpressions;

namespace ErenshorContracts
{
    internal static class ContractKillCreditPolicy
    {
        private static readonly Regex LocalKill = new Regex(
            @"^You\s+(?:have\s+)?slain\s+(?:(?:A|An|The)\s+)?(.+?)[.!]?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex PartyKill = new Regex(
            @"^(?:(?:A|An)\s+)?(.+?)\s+has been slain by\s+(.+?)[.!]?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Tags = new Regex(@"<[^>]+>", RegexOptions.CultureInvariant);

        internal static bool TryParseKillLine(string raw, out string enemy, out string killer, out bool localPlayer)
        {
            enemy = string.Empty;
            killer = string.Empty;
            localPlayer = false;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string text = Tags.Replace(raw, string.Empty).Trim();
            Match local = LocalKill.Match(text);
            if (local.Success)
            {
                enemy = ContractCombatPolicy.NormalizeEnemyName(local.Groups[1].Value);
                killer = "you";
                localPlayer = enemy.Length > 0;
                return localPlayer;
            }
            Match party = PartyKill.Match(text);
            if (!party.Success) return false;
            enemy = ContractCombatPolicy.NormalizeEnemyName(party.Groups[1].Value);
            killer = CleanActorName(party.Groups[2].Value);
            return enemy.Length > 0 && killer.Length > 0;
        }

        internal static string CleanActorName(string value)
        {
            string text = ContractCore.Clean(value, 120);
            while (text.EndsWith(".", StringComparison.Ordinal) || text.EndsWith("!", StringComparison.Ordinal))
                text = text.Substring(0, text.Length - 1).TrimEnd();
            return text;
        }
    }
}
