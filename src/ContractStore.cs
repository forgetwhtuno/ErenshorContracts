using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ErenshorContracts
{
    internal sealed class ContractStore
    {
        private const string Header = "ERENSHOR_CONTRACTS_V1";
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
            ContractDocument document = new ContractDocument();
            if (!File.Exists(_path)) return document;

            try
            {
                string[] lines = File.ReadAllLines(_path, Encoding.UTF8);
                if (lines.Length == 0 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                    throw new InvalidDataException("Unknown contract data format.");

                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length == 0) continue;

                    if (parts[0] == "M" && parts.Length >= 2)
                    {
                        int total;
                        if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out total))
                            document.TotalCompleted = Math.Max(0, total);
                    }
                    else if (parts[0] == "C" && parts.Length >= 2)
                    {
                        string id = Decode(parts[1]);
                        if (!string.IsNullOrWhiteSpace(id)) document.Claimed.Add(id);
                    }
                    else if (parts[0] == "A" && parts.Length >= 17)
                    {
                        ContractInstance value = new ContractInstance();
                        value.OccurrenceId = Decode(parts[1]);
                        value.ProviderId = Decode(parts[2]);
                        value.TemplateId = Decode(parts[3]);
                        value.OriginZone = Decode(parts[4]);
                        value.Title = Decode(parts[5]);
                        value.Description = Decode(parts[6]);
                        value.ProgressChannel = Decode(parts[7]);
                        value.ProgressKey = Decode(parts[8]);
                        value.ContextFilter = Decode(parts[9]);
                        value.RewardText = Decode(parts[10]);
                        value.Target = ParseInt(parts[11], 1);
                        value.Progress = Math.Max(0, Math.Min(value.Target, ParseInt(parts[12], 0)));
                        value.StateToken = Decode(parts[13]);
                        long ticks = ParseLong(parts[14], 0L);
                        value.AcceptedUtc = ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.UtcNow;
                        // parts[15] and [16] are reserved for forward-compatible fields.
                        if (!string.IsNullOrWhiteSpace(value.OccurrenceId) &&
                            !document.Claimed.Contains(value.OccurrenceId))
                            document.Active.Add(value);
                    }
                }
                return document;
            }
            catch (Exception ex)
            {
                warning = ex.GetType().Name + ": " + ex.Message;
                TryBackupUnreadable();
                return new ContractDocument();
            }
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
                writer.WriteLine(Header);
                writer.WriteLine("M|" + document.TotalCompleted.ToString(CultureInfo.InvariantCulture));

                foreach (string claimed in document.Claimed)
                    writer.WriteLine("C|" + Encode(claimed));

                for (int i = 0; i < document.Active.Count; i++)
                {
                    ContractInstance value = document.Active[i];
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
                        value.AcceptedUtc.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
                        "",
                        ""
                    }));
                }
            }

            if (File.Exists(_path))
            {
                try { File.Copy(_path, backup, true); } catch { }
            }

            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temp, _path);
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

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
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
