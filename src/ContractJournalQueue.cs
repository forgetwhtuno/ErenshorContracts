using System;
using System.Collections.Generic;

namespace ErenshorContracts
{
    // Small process-local retry queue for the soft Journal bridge. This is deliberately not
    // persisted: Journal API v1 does not expose an idempotency key, so a persisted cross-mod
    // delivery queue would introduce a duplicate-entry window after process loss.
    internal sealed class ContractJournalDelivery
    {
        internal string CharacterKey;
        internal string OccurrenceId;
        internal string Text;
    }

    internal sealed class ContractJournalQueue
    {
        internal const int MaxEntries = 32;
        private readonly List<ContractJournalDelivery> _entries = new List<ContractJournalDelivery>();

        internal int Count { get { return _entries.Count; } }

        // Returns true only when the bounded queue had to discard its oldest undelivered entry.
        internal bool Enqueue(string characterKey, string occurrenceId, string text)
        {
            if (string.IsNullOrWhiteSpace(characterKey) || string.IsNullOrWhiteSpace(occurrenceId) || string.IsNullOrWhiteSpace(text))
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                ContractJournalDelivery existing = _entries[i];
                if (existing != null &&
                    string.Equals(existing.CharacterKey, characterKey, StringComparison.Ordinal) &&
                    string.Equals(existing.OccurrenceId, occurrenceId, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Text = text;
                    return false;
                }
            }

            bool dropped = false;
            if (_entries.Count >= MaxEntries)
            {
                _entries.RemoveAt(0);
                dropped = true;
            }

            ContractJournalDelivery pending = new ContractJournalDelivery();
            pending.CharacterKey = characterKey;
            pending.OccurrenceId = occurrenceId;
            pending.Text = text;
            _entries.Add(pending);
            return dropped;
        }

        internal bool TryPeekForCharacter(string characterKey, out ContractJournalDelivery delivery)
        {
            delivery = null;
            if (string.IsNullOrWhiteSpace(characterKey)) return false;
            for (int i = 0; i < _entries.Count; i++)
            {
                ContractJournalDelivery current = _entries[i];
                if (current != null && string.Equals(current.CharacterKey, characterKey, StringComparison.Ordinal))
                {
                    delivery = current;
                    return true;
                }
            }
            return false;
        }

        internal bool Remove(string characterKey, string occurrenceId)
        {
            if (string.IsNullOrWhiteSpace(characterKey) || string.IsNullOrWhiteSpace(occurrenceId)) return false;
            for (int i = 0; i < _entries.Count; i++)
            {
                ContractJournalDelivery current = _entries[i];
                if (current != null &&
                    string.Equals(current.CharacterKey, characterKey, StringComparison.Ordinal) &&
                    string.Equals(current.OccurrenceId, occurrenceId, StringComparison.OrdinalIgnoreCase))
                {
                    _entries.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }
}
