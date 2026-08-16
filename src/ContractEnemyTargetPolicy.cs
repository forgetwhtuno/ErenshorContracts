using System;
using System.Collections.Generic;

namespace ErenshorContracts
{
    // Pure fallback policy for player-facing combat-target quality. Current Contracts runtime
    // evidence exposes native display name, level and observed population, but no authoritative
    // creature-family/template identity. Exact kill credit therefore continues to use EnemyName;
    // this policy only classifies presentation/count conservatively from evidence we actually have.
    internal static class ContractEnemyTargetPolicy
    {
        private static readonly HashSet<string> GenericHeadNouns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "skeleton", "skeletons", "spider", "spiders", "spiderling", "spiderlings",
            "wolf", "wolves", "goblin", "goblins", "crab", "crabs", "rat", "rats",
            "bat", "bats", "snake", "snakes", "slime", "slimes", "ooze", "oozes",
            "elemental", "elementals", "guard", "guards", "warrior", "warriors",
            "soldier", "soldiers", "bandit", "bandits", "cultist", "cultists",
            "zombie", "zombies", "undead", "bear", "bears", "boar", "boars",
            "kobold", "kobolds", "orc", "orcs", "troll", "trolls", "wisp", "wisps",
            "faerie", "faeries", "fairy", "fairies", "imp", "imps", "demon", "demons",
            "drake", "drakes", "beetle", "beetles", "worm", "worms", "scorpion", "scorpions",
            "enemy", "enemies", "raider", "raiders", "brigand", "brigands", "thief", "thieves"
        };

        internal static bool IsLikelyExactNamedTarget(string enemyName, int observedCount)
        {
            string name = ContractCombatPolicy.NormalizeEnemyName(enemyName);
            if (name.Length == 0) return true;
            if (HasGenericHeadNoun(name)) return false;

            // A single observed non-generic identity is safest as bounty-like work. For repeated
            // observations, a compact title-cased personal-name shape is still treated as exact.
            if (Math.Max(1, observedCount) <= 1) return true;
            string[] words = name.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2 || words.Length > 4) return false;
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (word.Length == 0 || !char.IsUpper(word[0])) return false;
                for (int c = 1; c < word.Length; c++)
                {
                    char value = word[c];
                    if (char.IsLetter(value) && char.IsUpper(value)) return false;
                }
            }
            return true;
        }

        internal static int ResolveTargetCount(string category, string seed, string enemyName, int observedCount)
        {
            int observed = Math.Max(1, observedCount);
            if (IsLikelyExactNamedTarget(enemyName, observed)) return 1;

            uint hash = ContractCore.StableHash(seed ?? string.Empty);
            bool global = string.Equals(ContractCategory.Normalize(category), ContractCategory.Global, StringComparison.Ordinal);
            int desired = global ? (8 + (int)(hash % 5u)) : (5 + (int)(hash % 4u));
            int evidenceCap = observed * (global ? 3 : 2);
            return Math.Max(1, Math.Min(desired, evidenceCap));
        }

        internal static int CapPersistedTargetCount(string category, string enemyName, int observedCount, int requestedCount)
        {
            int requested = Math.Max(1, requestedCount);
            int observed = Math.Max(1, observedCount);
            if (IsLikelyExactNamedTarget(enemyName, observed)) return 1;
            int evidenceCap = observed * (string.Equals(ContractCategory.Normalize(category), ContractCategory.Global, StringComparison.Ordinal) ? 3 : 2);
            return Math.Max(1, Math.Min(requested, evidenceCap));
        }

        internal static string BuildDisplayTarget(string enemyName, int targetCount, int observedCount)
        {
            string name = ContractCombatPolicy.NormalizeEnemyName(enemyName);
            if (name.Length == 0) return "enemy";
            if (targetCount <= 1 || IsLikelyExactNamedTarget(name, observedCount)) return name;
            return PluralizeLastWord(name);
        }

        private static bool HasGenericHeadNoun(string value)
        {
            string[] words = (value ?? string.Empty).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return false;
            return GenericHeadNouns.Contains(words[words.Length - 1]);
        }

        private static string PluralizeLastWord(string value)
        {
            string text = value ?? string.Empty;
            int split = text.LastIndexOf(' ');
            string prefix = split < 0 ? string.Empty : text.Substring(0, split + 1);
            string word = split < 0 ? text : text.Substring(split + 1);
            if (word.Length == 0) return text;
            if (word.Equals("Wolf", StringComparison.OrdinalIgnoreCase)) return prefix + MatchCase(word, "Wolves");
            if (word.Equals("Fairy", StringComparison.OrdinalIgnoreCase)) return prefix + MatchCase(word, "Fairies");
            if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase)) return text;
            if (word.EndsWith("ch", StringComparison.OrdinalIgnoreCase) || word.EndsWith("sh", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("x", StringComparison.OrdinalIgnoreCase) || word.EndsWith("z", StringComparison.OrdinalIgnoreCase))
                return prefix + word + "es";
            if (word.Length > 1 && word.EndsWith("y", StringComparison.OrdinalIgnoreCase) && !IsVowel(word[word.Length - 2]))
                return prefix + word.Substring(0, word.Length - 1) + "ies";
            return prefix + word + "s";
        }

        private static string MatchCase(string source, string replacement)
        {
            if (source.Length > 0 && char.IsLower(source[0]))
                return char.ToLowerInvariant(replacement[0]) + replacement.Substring(1);
            return replacement;
        }

        private static bool IsVowel(char value)
        {
            char c = char.ToLowerInvariant(value);
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
        }
    }
}
