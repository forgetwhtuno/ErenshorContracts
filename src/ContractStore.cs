using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ErenshorContracts
{
    internal sealed class ContractStore
    {
        private const string HeaderV1 = "ERENSHOR_CONTRACTS_V1";
        private const string HeaderV2 = "ERENSHOR_CONTRACTS_V2";
        private const string HeaderV3 = "ERENSHOR_CONTRACTS_V3";
        private readonly string _path;

        internal ContractStore(string path)
        {
            _path = path;
        }

        internal string PathOnDisk
        {
            get { return _path; }
        }

        internal ContractDocument Load(out string warning)
        {
            warning = string.Empty;
            if (!File.Exists(_path))
            {
                ContractDocument recovered;
                string recoveredError;
                string backupOnly = _path + ".bak";
                if (File.Exists(backupOnly) && TryLoadFile(backupOnly, out recovered, out recoveredError))
                {
                    warning = "Primary sidecar was missing; recovered the last valid .bak snapshot.";
                    return recovered;
                }

                string tempOnly = _path + ".tmp";
                if (File.Exists(tempOnly) && TryLoadFile(tempOnly, out recovered, out recoveredError))
                {
                    warning = "Primary sidecar was missing; recovered a complete pending .tmp snapshot.";
                    return recovered;
                }
                return new ContractDocument();
            }

            ContractDocument primary;
            string primaryError;
            if (TryLoadFile(_path, out primary, out primaryError)) return primary;

            TryBackupUnreadable();

            string backupPath = _path + ".bak";
            ContractDocument backup;
            string backupError = string.Empty;
            if (File.Exists(backupPath) && TryLoadFile(backupPath, out backup, out backupError))
            {
                warning = "Primary sidecar was unreadable (" + primaryError + "); recovered the last valid .bak snapshot.";
                return backup;
            }

            warning = primaryError;
            if (File.Exists(backupPath) && !string.IsNullOrEmpty(backupError))
                warning += " Backup was also unreadable (" + backupError + ").";
            return new ContractDocument();
        }

        private static bool TryLoadFile(string path, out ContractDocument document, out string error)
        {
            document = new ContractDocument();
            error = string.Empty;
            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length > 4L * 1024L * 1024L) throw new InvalidDataException("Contract data exceeds the 4 MiB safety limit.");

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                if (lines.Length == 0) throw new InvalidDataException("Unknown contract data format.");
                bool isV1 = string.Equals(lines[0], HeaderV1, StringComparison.Ordinal);
                bool isV2 = string.Equals(lines[0], HeaderV2, StringComparison.Ordinal);
                bool isV3 = string.Equals(lines[0], HeaderV3, StringComparison.Ordinal);
                if (!isV1 && !isV2 && !isV3) throw new InvalidDataException("Unknown contract data format.");
                if (lines.Length > 20000) throw new InvalidDataException("Contract data contains too many records.");

                HashSet<string> legacyPending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.Length > 65536) throw new InvalidDataException("Contract data record is too large.");
                    string[] parts = line.Split('|');
                    if (parts.Length == 0) continue;

                    if (parts[0] == "M" && parts.Length >= 2)
                    {
                        document.TotalCompleted = Math.Max(0, ParseInt(parts[1], 0));
                        if (parts.Length >= 3) document.TotalLocalCompleted = Math.Max(0, ParseInt(parts[2], 0));
                        if (parts.Length >= 4) document.TotalGlobalCompleted = Math.Max(0, ParseInt(parts[3], 0));
                    }
                    else if (parts[0] == "R" && parts.Length >= 6)
                    {
                        document.ActivePlaySeconds = Math.Max(0L, ParseLong(parts[1], 0L));
                        document.LocalBoardRevision = Math.Max(0, ParseInt(parts[2], 0));
                        document.GlobalBoardRevision = Math.Max(0, ParseInt(parts[3], 0));
                        document.NextLocalRefreshAtSeconds = Math.Max(0L, ParseLong(parts[4], 0L));
                        document.NextGlobalRefreshAtSeconds = Math.Max(0L, ParseLong(parts[5], 0L));
                        if (parts.Length >= 7) document.LocalBoardZone = CleanLoaded(DecodeOptional(parts[6]), 128);
                        if (isV3 && parts.Length >= 10)
                        {
                            document.LocalCombatGenerationRevision = ParseInt(parts[7], -1);
                            document.GlobalCombatGenerationRevision = ParseInt(parts[8], -1);
                            document.LocalCombatGenerationZone = CleanLoaded(DecodeOptional(parts[9]), 128);
                        }
                    }
                    else if (isV3 && parts[0] == "E" && parts.Length >= 6)
                    {
                        if (document.EnemyCatalog.Count >= ContractCombatPolicy.MaxEnemyCatalogRecords) continue;
                        ContractEnemyRecord enemy = new ContractEnemyRecord();
                        enemy.Zone = CleanLoaded(DecodeOptional(parts[1]), 128);
                        enemy.EnemyName = CleanLoaded(DecodeOptional(parts[2]), 120);
                        enemy.MinLevel = Math.Max(1, ParseInt(parts[3], 1));
                        enemy.MaxLevel = Math.Max(enemy.MinLevel, ParseInt(parts[4], enemy.MinLevel));
                        enemy.LastSeenActiveSeconds = Math.Max(0L, ParseLong(parts[5], 0L));
                        enemy.ObservedCount = parts.Length >= 7 ? Math.Max(1, ParseInt(parts[6], 1)) : 1;
                        if (!string.IsNullOrWhiteSpace(enemy.Zone) && !string.IsNullOrWhiteSpace(enemy.EnemyName))
                            document.EnemyCatalog.Add(enemy);
                    }
                    else if (isV3 && parts[0] == "B" && parts.Length >= 9)
                    {
                        if (document.GeneratedCombatOffers.Count >= ContractCombatPolicy.MaxGeneratedOffers) continue;
                        ContractGeneratedCombatOffer offer = new ContractGeneratedCombatOffer();
                        offer.Category = ContractCategory.Normalize(DecodeOptional(parts[1]));
                        offer.BoardRevision = Math.Max(0, ParseInt(parts[2], 0));
                        offer.BoardZone = CleanLoaded(DecodeOptional(parts[3]), 128);
                        offer.TargetZone = CleanLoaded(DecodeOptional(parts[4]), 128);
                        offer.EnemyName = CleanLoaded(DecodeOptional(parts[5]), 120);
                        offer.EnemyLevel = Math.Max(1, ParseInt(parts[6], 1));
                        offer.TargetCount = Math.Max(1, Math.Min(1000, ParseInt(parts[7], 1)));
                        offer.RewardXpBasisPoints = Math.Max(0, Math.Min(5000, ParseInt(parts[8], 0)));
                        if (!string.IsNullOrWhiteSpace(offer.TargetZone) && !string.IsNullOrWhiteSpace(offer.EnemyName))
                            document.GeneratedCombatOffers.Add(offer);
                    }
                    else if (parts[0] == "C" && parts.Length >= 2)
                    {
                        string id = Decode(parts[1]);
                        if (!string.IsNullOrWhiteSpace(id) && id.Length <= 512) document.Claimed.Add(id);
                    }
                    else if (isV1 && parts[0] == "P" && parts.Length >= 2)
                    {
                        string id = Decode(parts[1]);
                        if (!string.IsNullOrWhiteSpace(id) && id.Length <= 512) legacyPending.Add(id);
                    }
                    else if (parts[0] == "A" && parts.Length >= 17)
                    {
                        if (document.Active.Count >= ContractCore.MaxActiveContracts) continue;
                        ContractInstance value = new ContractInstance();
                        value.OccurrenceId = CleanLoaded(Decode(parts[1]), 512);
                        value.ProviderId = CleanLoaded(Decode(parts[2]), 64);
                        value.TemplateId = CleanLoaded(Decode(parts[3]), 64);
                        value.OriginZone = CleanLoaded(Decode(parts[4]), 128);
                        value.Title = CleanLoaded(Decode(parts[5]), 120);
                        value.Description = CleanLoaded(Decode(parts[6]), 320);
                        value.ProgressChannel = CleanLoaded(Decode(parts[7]), 64);
                        value.ProgressKey = CleanLoaded(Decode(parts[8]), 64);
                        value.ContextFilter = CleanLoaded(Decode(parts[9]), 160);
                        value.RewardText = CleanLoaded(Decode(parts[10]), 200);
                        value.Target = Math.Max(1, Math.Min(1000000, ParseInt(parts[11], 1)));
                        value.Progress = Math.Max(0, Math.Min(value.Target, ParseInt(parts[12], 0)));
                        value.StateToken = CleanLoaded(Decode(parts[13]), 1024);
                        value.AcceptedUtc = SafeUtc(ParseLong(parts[14], 0L));
                        value.Category = ContractCategory.Normalize(DecodeOptional(parts[15]));
                        value.RewardXpBasisPoints = Math.Max(0, Math.Min(5000, ParseIntOptional(parts[16], 0)));

                        if ((isV2 || isV3) && parts.Length < 29)
                            throw new InvalidDataException("V2/V3 active contract record is missing reward-ledger fields.");

                        if (isV2 || isV3)
                        {
                            value.RewardGoldAmount = Math.Max(0, Math.Min(100000000, ParseIntOptional(parts[17], 0)));
                            value.RewardItemId = CleanLoaded(DecodeOptional(parts[18]), 128);
                            value.RewardItemQuantity = Math.Max(0, Math.Min(1000, ParseIntOptional(parts[19], 0)));
                            value.RewardItemName = CleanLoaded(DecodeOptional(parts[20]), 120);
                            value.XpRewardStatus = ParseRewardStatus(parts[21], value.RewardXpBasisPoints > 0);
                            value.AppliedXpAmount = Math.Max(0, ParseIntOptional(parts[22], 0));
                            value.GoldRewardStatus = ParseRewardStatus(parts[23], value.RewardGoldAmount > 0);
                            value.AppliedGoldAmount = Math.Max(0, ParseIntOptional(parts[24], 0));
                            value.ItemRewardStatus = ParseRewardStatus(parts[25], value.RewardItemQuantity > 0 && !string.IsNullOrWhiteSpace(value.RewardItemId));
                            value.AppliedItemCount = Math.Max(0, Math.Min(1000, ParseIntOptional(parts[26], 0)));
                            value.AppliedItemSummary = CleanLoaded(DecodeOptional(parts[27]), 160);
                            value.PlannedXpAmount = Math.Max(0, ParseIntOptional(parts[28], 0));
                            if (isV3 && parts.Length >= 30)
                                value.TargetZone = CleanLoaded(DecodeOptional(parts[29]), 128);
                            NormalizeLoadedRewardState(value);
                        }
                        if (string.IsNullOrWhiteSpace(value.TargetZone) &&
                            string.Equals(ContractCategory.Normalize(value.Category), ContractCategory.Local, StringComparison.Ordinal))
                            value.TargetZone = value.OriginZone;

                        if (!string.IsNullOrWhiteSpace(value.OccurrenceId) &&
                            !document.Claimed.Contains(value.OccurrenceId) &&
                            ContractCore.FindActive(document, value.OccurrenceId) == null)
                            document.Active.Add(value);
                    }
                }

                if (isV1)
                {
                    foreach (string id in legacyPending)
                    {
                        ContractInstance active = ContractCore.FindActive(document, id);
                        if (active != null && active.RewardXpBasisPoints > 0)
                            active.XpRewardStatus = RewardComponentStatus.OutcomeUnknown;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                document = new ContractDocument();
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static void NormalizeLoadedRewardState(ContractInstance value)
        {
            if (value == null) return;
            // A persisted Applying marker means the previous process crossed the last safe retry
            // boundary. The sidecar cannot know whether the native call took effect.
            if (value.XpRewardStatus == RewardComponentStatus.Applying) value.XpRewardStatus = RewardComponentStatus.OutcomeUnknown;
            if (value.GoldRewardStatus == RewardComponentStatus.Applying) value.GoldRewardStatus = RewardComponentStatus.OutcomeUnknown;
            if (value.ItemRewardStatus == RewardComponentStatus.Applying) value.ItemRewardStatus = RewardComponentStatus.OutcomeUnknown;

            if (value.RewardXpBasisPoints > 0)
            {
                if (value.XpRewardStatus == RewardComponentStatus.Applied)
                {
                    if (value.AppliedXpAmount <= 0) value.XpRewardStatus = RewardComponentStatus.OutcomeUnknown;
                    else if (value.PlannedXpAmount <= 0) value.PlannedXpAmount = value.AppliedXpAmount;
                    else if (value.PlannedXpAmount != value.AppliedXpAmount) value.XpRewardStatus = RewardComponentStatus.OutcomeUnknown;
                }
                else if ((value.XpRewardStatus == RewardComponentStatus.Prepared ||
                          value.XpRewardStatus == RewardComponentStatus.FailedRetryable) && value.PlannedXpAmount <= 0)
                {
                    // Prepared/retryable means the amount should already have been locked before
                    // native invocation. Losing that plan is corruption, not permission to create
                    // a different payout after reload.
                    value.XpRewardStatus = RewardComponentStatus.OutcomeUnknown;
                }
                else if (value.XpRewardStatus == RewardComponentStatus.NotStarted && value.PlannedXpAmount > 0)
                {
                    // An unstarted transaction must not carry a hidden persisted payout amount.
                    value.XpRewardStatus = RewardComponentStatus.OutcomeUnknown;
                }
            }
            if (value.RewardGoldAmount > 0 && value.GoldRewardStatus == RewardComponentStatus.Applied &&
                value.AppliedGoldAmount != value.RewardGoldAmount)
                value.GoldRewardStatus = RewardComponentStatus.OutcomeUnknown;
            if (value.RewardItemQuantity > 0 && !string.IsNullOrWhiteSpace(value.RewardItemId) &&
                value.ItemRewardStatus == RewardComponentStatus.Applied && value.AppliedItemCount != value.RewardItemQuantity)
                value.ItemRewardStatus = RewardComponentStatus.OutcomeUnknown;
        }

        private static RewardComponentStatus ParseRewardStatus(string value, bool required)
        {
            int parsed;
            if (!int.TryParse(value ?? string.Empty, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return required ? RewardComponentStatus.OutcomeUnknown : RewardComponentStatus.NotStarted;
            if (parsed >= (int)RewardComponentStatus.NotStarted && parsed <= (int)RewardComponentStatus.OutcomeUnknown)
                return (RewardComponentStatus)parsed;
            return required ? RewardComponentStatus.OutcomeUnknown : RewardComponentStatus.NotStarted;
        }

        private static DateTime SafeUtc(long ticks)
        {
            if (ticks <= 0L || ticks > DateTime.MaxValue.Ticks) return DateTime.UtcNow;
            try { return new DateTime(ticks, DateTimeKind.Utc); }
            catch { return DateTime.UtcNow; }
        }

        private static string CleanLoaded(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string clean = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length <= max ? clean : clean.Substring(0, max);
        }

        internal void Save(ContractDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            string directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            string temp = _path + ".tmp";
            string backup = _path + ".bak";

            using (StreamWriter writer = new StreamWriter(temp, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(HeaderV3);
                writer.WriteLine("M|" + Math.Max(0, document.TotalCompleted).ToString(CultureInfo.InvariantCulture) + "|" +
                    Math.Max(0, document.TotalLocalCompleted).ToString(CultureInfo.InvariantCulture) + "|" +
                    Math.Max(0, document.TotalGlobalCompleted).ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("R|" + Math.Max(0L, document.ActivePlaySeconds).ToString(CultureInfo.InvariantCulture) + "|" +
                    Math.Max(0, document.LocalBoardRevision).ToString(CultureInfo.InvariantCulture) + "|" +
                    Math.Max(0, document.GlobalBoardRevision).ToString(CultureInfo.InvariantCulture) + "|" +
                    Math.Max(0L, document.NextLocalRefreshAtSeconds).ToString(CultureInfo.InvariantCulture) + "|" +
                    Math.Max(0L, document.NextGlobalRefreshAtSeconds).ToString(CultureInfo.InvariantCulture) + "|" +
                    Encode(CleanLoaded(document.LocalBoardZone, 128)) + "|" +
                    document.LocalCombatGenerationRevision.ToString(CultureInfo.InvariantCulture) + "|" +
                    document.GlobalCombatGenerationRevision.ToString(CultureInfo.InvariantCulture) + "|" +
                    Encode(CleanLoaded(document.LocalCombatGenerationZone, 128)));

                for (int i = 0; i < document.EnemyCatalog.Count && i < ContractCombatPolicy.MaxEnemyCatalogRecords; i++)
                {
                    ContractEnemyRecord enemy = document.EnemyCatalog[i];
                    if (enemy == null || string.IsNullOrWhiteSpace(enemy.Zone) || string.IsNullOrWhiteSpace(enemy.EnemyName)) continue;
                    writer.WriteLine("E|" + Encode(CleanLoaded(enemy.Zone, 128)) + "|" +
                        Encode(CleanLoaded(enemy.EnemyName, 120)) + "|" +
                        Math.Max(1, enemy.MinLevel).ToString(CultureInfo.InvariantCulture) + "|" +
                        Math.Max(Math.Max(1, enemy.MinLevel), enemy.MaxLevel).ToString(CultureInfo.InvariantCulture) + "|" +
                        Math.Max(0L, enemy.LastSeenActiveSeconds).ToString(CultureInfo.InvariantCulture) + "|" +
                        Math.Max(1, enemy.ObservedCount).ToString(CultureInfo.InvariantCulture));
                }

                for (int i = 0; i < document.GeneratedCombatOffers.Count && i < ContractCombatPolicy.MaxGeneratedOffers; i++)
                {
                    ContractGeneratedCombatOffer offer = document.GeneratedCombatOffers[i];
                    if (offer == null || string.IsNullOrWhiteSpace(offer.TargetZone) || string.IsNullOrWhiteSpace(offer.EnemyName)) continue;
                    writer.WriteLine("B|" + Encode(ContractCategory.Normalize(offer.Category)) + "|" +
                        Math.Max(0, offer.BoardRevision).ToString(CultureInfo.InvariantCulture) + "|" +
                        Encode(CleanLoaded(offer.BoardZone, 128)) + "|" +
                        Encode(CleanLoaded(offer.TargetZone, 128)) + "|" +
                        Encode(CleanLoaded(offer.EnemyName, 120)) + "|" +
                        Math.Max(1, offer.EnemyLevel).ToString(CultureInfo.InvariantCulture) + "|" +
                        Math.Max(1, Math.Min(1000, offer.TargetCount)).ToString(CultureInfo.InvariantCulture) + "|" +
                        Math.Max(0, Math.Min(5000, offer.RewardXpBasisPoints)).ToString(CultureInfo.InvariantCulture));
                }

                foreach (string claimed in document.Claimed)
                    writer.WriteLine("C|" + Encode(claimed));

                for (int i = 0; i < document.Active.Count; i++)
                {
                    ContractInstance value = document.Active[i];
                    if (value == null) continue;
                    writer.WriteLine(string.Join("|", new string[]
                    {
                        "A",
                        Encode(value.OccurrenceId),
                        Encode(value.ProviderId),
                        Encode(value.TemplateId),
                        Encode(value.OriginZone),
                        Encode(value.Title),
                        Encode(value.Description),
                        Encode(value.ProgressChannel),
                        Encode(value.ProgressKey),
                        Encode(value.ContextFilter),
                        Encode(value.RewardText),
                        Math.Max(1, value.Target).ToString(CultureInfo.InvariantCulture),
                        Math.Max(0, Math.Min(value.Target, value.Progress)).ToString(CultureInfo.InvariantCulture),
                        Encode(value.StateToken),
                        SafeTicks(value.AcceptedUtc).ToString(CultureInfo.InvariantCulture),
                        Encode(ContractCategory.Normalize(value.Category)),
                        Math.Max(0, Math.Min(5000, value.RewardXpBasisPoints)).ToString(CultureInfo.InvariantCulture),
                        Math.Max(0, value.RewardGoldAmount).ToString(CultureInfo.InvariantCulture),
                        Encode(CleanLoaded(value.RewardItemId, 128)),
                        Math.Max(0, Math.Min(1000, value.RewardItemQuantity)).ToString(CultureInfo.InvariantCulture),
                        Encode(CleanLoaded(value.RewardItemName, 120)),
                        ((int)value.XpRewardStatus).ToString(CultureInfo.InvariantCulture),
                        Math.Max(0, value.AppliedXpAmount).ToString(CultureInfo.InvariantCulture),
                        ((int)value.GoldRewardStatus).ToString(CultureInfo.InvariantCulture),
                        Math.Max(0, value.AppliedGoldAmount).ToString(CultureInfo.InvariantCulture),
                        ((int)value.ItemRewardStatus).ToString(CultureInfo.InvariantCulture),
                        Math.Max(0, Math.Min(1000, value.AppliedItemCount)).ToString(CultureInfo.InvariantCulture),
                        Encode(CleanLoaded(value.AppliedItemSummary, 160)),
                        Math.Max(0, value.PlannedXpAmount).ToString(CultureInfo.InvariantCulture),
                        Encode(CleanLoaded(value.TargetZone, 128))
                    }));
                }
            }

            if (File.Exists(_path))
            {
                try
                {
                    File.Replace(temp, _path, backup, true);
                    return;
                }
                catch
                {
                    try { File.Copy(_path, backup, true); } catch { }
                    File.Copy(temp, _path, true);
                    File.Delete(temp);
                    return;
                }
            }

            File.Move(temp, _path);
        }

        internal static bool TryClaimLegacyData(string legacyPath, string claimMarkerPath, string destinationPath, string characterKey)
        {
            if (string.IsNullOrWhiteSpace(legacyPath) || string.IsNullOrWhiteSpace(claimMarkerPath) ||
                string.IsNullOrWhiteSpace(destinationPath)) return false;
            if (!File.Exists(legacyPath)) return false;
            if (File.Exists(claimMarkerPath)) return false;

            if (File.Exists(destinationPath))
            {
                WriteClaimMarker(claimMarkerPath, characterKey);
                return false;
            }

            try
            {
                string directory = System.IO.Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.Copy(legacyPath, destinationPath, false);
                WriteClaimMarker(claimMarkerPath, characterKey);
                return true;
            }
            catch { return false; }
        }

        private static void WriteClaimMarker(string path, string characterKey)
        {
            try
            {
                string directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                string body = "claimed_by=" + (characterKey ?? string.Empty) + Environment.NewLine +
                              "claimed_utc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                File.WriteAllText(path, body, new UTF8Encoding(false));
            }
            catch { }
        }

        private void TryBackupUnreadable()
        {
            try
            {
                if (!File.Exists(_path)) return;
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                File.Copy(_path, _path + ".corrupt-" + stamp, true);
            }
            catch { }
        }

        private static long SafeTicks(DateTime value)
        {
            try
            {
                DateTime utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
                return utc.Ticks;
            }
            catch { return DateTime.UtcNow.Ticks; }
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        private static string DecodeOptional(string value)
        {
            try { return Decode(value); }
            catch { return string.Empty; }
        }

        private static int ParseIntOptional(string value, int fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            return ParseInt(value, fallback);
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static long ParseLong(string value, long fallback)
        {
            long parsed;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }
    }
}
