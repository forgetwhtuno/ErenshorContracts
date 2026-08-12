using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ErenshorContracts
{
    internal static class ContractCore
    {
        internal const int MaxActiveContracts = 6;
        internal const int MaxDailySlots = 6;

        internal static ContractTemplate BuildPatrolTemplate(int minutes)
        {
            int safeMinutes = Math.Max(1, Math.Min(60, minutes));
            ContractTemplate value = NewTemplate(
                "builtin", "local_patrol", "*",
                "Local Patrol",
                "Spend " + safeMinutes.ToString(CultureInfo.InvariantCulture) + " active minute" + (safeMinutes == 1 ? string.Empty : "s") + " adventuring in this zone.",
                "builtin", "zone_seconds", string.Empty,
                safeMinutes * 60, 0,
                "No native reward in Preview; completion is recorded locally.");
            return value;
        }

        internal static ContractTemplate BuildRoadCheckTemplate()
        {
            return NewTemplate(
                "builtin", "road_check", "*",
                "Road Check",
                "Leave this zone and return once. This is a travel activity, not a navigation command.",
                "builtin", "leave_return", string.Empty,
                1, 0,
                "No native reward in Preview; completion is recorded locally.");
        }

        internal static ContractTemplate BuildWayfarerTemplate()
        {
            return NewTemplate(
                "builtin", "wayfarer", "*",
                "Wayfarer",
                "Visit two other zones while this contract is active.",
                "builtin", "visit_unique_zone", string.Empty,
                2, 0,
                "No native reward in Preview; completion is recorded locally.");
        }

        internal static ContractTemplate FromRegistration(ContractTemplateRegistration value)
        {
            if (value == null) return null;
            return NewTemplate(
                Clean(value.ProviderId, 64),
                Clean(value.TemplateId, 64),
                string.IsNullOrWhiteSpace(value.ZoneScope) ? "*" : Clean(value.ZoneScope, 96),
                Clean(value.Title, 120),
                Clean(value.Description, 320),
                Clean(value.ProgressChannel, 64),
                Clean(value.ProgressKey, 64),
                Clean(value.ContextFilter, 160),
                Math.Max(1, Math.Min(1000000, value.Target)),
                Math.Max(-1000, Math.Min(1000, value.Priority)),
                Clean(value.RewardText, 200));
        }

        internal static List<ContractOffer> BuildDailyOffers(
            DateTime localDate,
            string zone,
            string profileKey,
            IEnumerable<ContractTemplate> templates,
            ContractDocument document,
            int slotCount)
        {
            List<ContractOffer> result = new List<ContractOffer>();
            if (document == null || templates == null || string.IsNullOrWhiteSpace(zone)) return result;

            int safeSlots = Math.Max(1, Math.Min(MaxDailySlots, slotCount));
            string safeProfile = string.IsNullOrWhiteSpace(profileKey) ? "local" : profileKey.Trim();
            string day = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            Dictionary<string, ContractTemplate> unique = new Dictionary<string, ContractTemplate>(StringComparer.OrdinalIgnoreCase);
            foreach (ContractTemplate raw in templates)
            {
                if (raw == null || !MatchesZone(raw.ZoneScope, zone)) continue;
                string key = raw.ProviderId + "|" + raw.TemplateId;
                ContractTemplate existing;
                if (!unique.TryGetValue(key, out existing) || raw.Priority > existing.Priority)
                    unique[key] = raw;
            }

            List<ContractTemplate> eligible = new List<ContractTemplate>(unique.Values);
            eligible.Sort(delegate(ContractTemplate a, ContractTemplate b)
            {
                int byPriority = b.Priority.CompareTo(a.Priority);
                if (byPriority != 0) return byPriority;

                uint ah = StableHash(day + "|" + zone + "|" + safeProfile + "|" + a.ProviderId + "|" + a.TemplateId);
                uint bh = StableHash(day + "|" + zone + "|" + safeProfile + "|" + b.ProviderId + "|" + b.TemplateId);
                int hashCompare = ah.CompareTo(bh);
                if (hashCompare != 0) return hashCompare;
                int providerCompare = string.Compare(a.ProviderId, b.ProviderId, StringComparison.OrdinalIgnoreCase);
                if (providerCompare != 0) return providerCompare;
                return string.Compare(a.TemplateId, b.TemplateId, StringComparison.OrdinalIgnoreCase);
            });

            int count = Math.Min(safeSlots, eligible.Count);
            for (int i = 0; i < count; i++)
            {
                ContractTemplate template = eligible[i];
                ContractOffer offer = new ContractOffer();
                offer.Template = template;
                offer.OccurrenceId = BuildOccurrenceId(day, zone, safeProfile, template.ProviderId, template.TemplateId);
                offer.Active = FindActive(document, offer.OccurrenceId);
                offer.Claimed = document.Claimed.Contains(offer.OccurrenceId);
                result.Add(offer);
            }
            return result;
        }

        internal static ContractInstance Accept(ContractDocument document, ContractOffer offer, string zone, DateTime nowUtc)
        {
            if (document == null || offer == null || offer.Template == null) return null;
            if (offer.Claimed || document.Claimed.Contains(offer.OccurrenceId)) return null;
            ContractInstance existing = FindActive(document, offer.OccurrenceId);
            if (existing != null) return existing;
            if (document.Active.Count >= MaxActiveContracts) return null;

            ContractTemplate template = offer.Template;
            ContractInstance value = new ContractInstance();
            value.OccurrenceId = offer.OccurrenceId;
            value.ProviderId = template.ProviderId;
            value.TemplateId = template.TemplateId;
            value.OriginZone = zone == null ? string.Empty : zone;
            value.Title = template.Title;
            value.Description = template.Description;
            value.ProgressChannel = template.ProgressChannel;
            value.ProgressKey = template.ProgressKey;
            value.ContextFilter = template.ContextFilter;
            value.RewardText = template.RewardText;
            value.Target = template.Target;
            value.Progress = 0;
            value.StateToken = string.Empty;
            value.AcceptedUtc = nowUtc.Kind == DateTimeKind.Utc ? nowUtc : nowUtc.ToUniversalTime();
            document.Active.Add(value);
            return value;
        }

        internal static bool Abandon(ContractDocument document, string occurrenceId)
        {
            if (document == null || string.IsNullOrWhiteSpace(occurrenceId)) return false;
            for (int i = 0; i < document.Active.Count; i++)
            {
                if (!string.Equals(document.Active[i].OccurrenceId, occurrenceId, StringComparison.OrdinalIgnoreCase)) continue;
                document.Active.RemoveAt(i);
                return true;
            }
            return false;
        }

        internal static ContractInstance Claim(ContractDocument document, string occurrenceId)
        {
            if (document == null || string.IsNullOrWhiteSpace(occurrenceId)) return null;
            for (int i = 0; i < document.Active.Count; i++)
            {
                ContractInstance value = document.Active[i];
                if (!string.Equals(value.OccurrenceId, occurrenceId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!value.IsComplete) return null;

                document.Active.RemoveAt(i);
                document.Claimed.Add(value.OccurrenceId);
                document.TotalCompleted++;
                return value;
            }
            return null;
        }

        internal static int ApplyExternalProgress(ContractDocument document, ContractProgressReport report)
        {
            if (document == null || report == null || report.Amount <= 0) return 0;
            int changed = 0;
            for (int i = 0; i < document.Active.Count; i++)
            {
                ContractInstance active = document.Active[i];
                if (active.IsComplete) continue;
                if (!string.Equals(active.ProgressChannel, report.Channel, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(active.ProgressKey, report.Key, StringComparison.OrdinalIgnoreCase)) continue;
                if (!ContextMatches(active.ContextFilter, report.Context)) continue;

                int old = active.Progress;
                active.Progress = Math.Min(active.Target, active.Progress + report.Amount);
                if (active.Progress != old) changed++;
            }
            return changed;
        }

        internal static int AddZoneSeconds(ContractDocument document, string currentZone, int seconds)
        {
            if (document == null || string.IsNullOrWhiteSpace(currentZone) || seconds <= 0) return 0;
            int changed = 0;
            for (int i = 0; i < document.Active.Count; i++)
            {
                ContractInstance active = document.Active[i];
                if (active.IsComplete) continue;
                if (!string.Equals(active.ProviderId, "builtin", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(active.ProgressKey, "zone_seconds", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(active.OriginZone, currentZone, StringComparison.OrdinalIgnoreCase))
                    continue;

                int old = active.Progress;
                active.Progress = Math.Min(active.Target, active.Progress + seconds);
                if (active.Progress != old) changed++;
            }
            return changed;
        }

        internal static int HandleZoneTransition(ContractDocument document, string oldZone, string newZone)
        {
            if (document == null || string.IsNullOrWhiteSpace(newZone)) return 0;
            int changed = 0;

            for (int i = 0; i < document.Active.Count; i++)
            {
                ContractInstance active = document.Active[i];
                if (active.IsComplete) continue;
                if (!string.Equals(active.ProviderId, "builtin", StringComparison.OrdinalIgnoreCase)) continue;

                if (string.Equals(active.ProgressKey, "leave_return", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(newZone, active.OriginZone, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.Equals(active.StateToken, "away", StringComparison.Ordinal))
                        {
                            active.StateToken = "away";
                            changed++;
                        }
                    }
                    else if (string.Equals(active.StateToken, "away", StringComparison.Ordinal))
                    {
                        active.Progress = active.Target;
                        active.StateToken = "returned";
                        changed++;
                    }
                }
                else if (string.Equals(active.ProgressKey, "visit_unique_zone", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(newZone, active.OriginZone, StringComparison.OrdinalIgnoreCase)) continue;
                    List<string> visited = ParseVisited(active.StateToken);
                    if (ContainsIgnoreCase(visited, newZone)) continue;
                    visited.Add(newZone);
                    active.StateToken = EncodeVisited(visited);
                    active.Progress = Math.Min(active.Target, visited.Count);
                    changed++;
                }
            }
            return changed;
        }

        internal static ContractInstance FindActive(ContractDocument document, string occurrenceId)
        {
            if (document == null || string.IsNullOrWhiteSpace(occurrenceId)) return null;
            for (int i = 0; i < document.Active.Count; i++)
                if (string.Equals(document.Active[i].OccurrenceId, occurrenceId, StringComparison.OrdinalIgnoreCase))
                    return document.Active[i];
            return null;
        }

        internal static string ProgressText(ContractInstance value)
        {
            if (value == null) return string.Empty;
            if (string.Equals(value.ProgressKey, "zone_seconds", StringComparison.OrdinalIgnoreCase))
            {
                int currentMinutes = value.Progress / 60;
                int currentSeconds = value.Progress % 60;
                int targetMinutes = value.Target / 60;
                return currentMinutes.ToString(CultureInfo.InvariantCulture) + ":" +
                       currentSeconds.ToString("00", CultureInfo.InvariantCulture) + " / " +
                       targetMinutes.ToString(CultureInfo.InvariantCulture) + ":00";
            }
            return Math.Min(value.Progress, value.Target).ToString(CultureInfo.InvariantCulture) +
                   " / " + value.Target.ToString(CultureInfo.InvariantCulture);
        }

        internal static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string text = value ?? string.Empty;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= char.ToLowerInvariant(text[i]);
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        internal static bool MatchesZone(string scope, string zone)
        {
            if (string.IsNullOrWhiteSpace(zone)) return false;
            if (string.IsNullOrWhiteSpace(scope) || scope == "*") return true;
            return string.Equals(scope.Trim(), zone.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ContextMatches(string filter, string context)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            if (string.IsNullOrWhiteSpace(context)) return false;
            return context.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static string Clean(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string clean = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length <= max ? clean : clean.Substring(0, max);
        }

        private static ContractTemplate NewTemplate(
            string providerId, string templateId, string zoneScope,
            string title, string description, string progressChannel, string progressKey,
            string contextFilter, int target, int priority, string rewardText)
        {
            ContractTemplate value = new ContractTemplate();
            value.ProviderId = providerId;
            value.TemplateId = templateId;
            value.ZoneScope = zoneScope;
            value.Title = title;
            value.Description = description;
            value.ProgressChannel = progressChannel;
            value.ProgressKey = progressKey;
            value.ContextFilter = contextFilter;
            value.Target = target;
            value.Priority = priority;
            value.RewardText = rewardText;
            return value;
        }

        private static string BuildOccurrenceId(string day, string zone, string profile, string provider, string template)
        {
            return day + "|" + zone + "|" + profile + "|" + provider + "|" + template;
        }

        private static List<string> ParseVisited(string token)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(token)) return result;
            string[] parts = token.Split(new char[] { '\u001f' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                if (!ContainsIgnoreCase(result, parts[i])) result.Add(parts[i]);
            return result;
        }

        private static string EncodeVisited(List<string> values)
        {
            if (values == null || values.Count == 0) return string.Empty;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) builder.Append('\u001f');
                builder.Append((values[i] ?? string.Empty).Replace('\u001f', ' '));
            }
            return builder.ToString();
        }

        private static bool ContainsIgnoreCase(List<string> values, string candidate)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], candidate, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
