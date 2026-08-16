using System;
using ErenshorContracts;

internal static class ContractJournalQueueTests
{
    internal static int RunAll()
    {
        int assertions = 0;
        assertions += TestCharacterIsolationAndOrdering();
        assertions += TestOccurrenceDeduplication();
        assertions += TestBoundedQueue();
        return assertions;
    }

    private static int TestCharacterIsolationAndOrdering()
    {
        ContractJournalQueue queue = new ContractJournalQueue();
        queue.Enqueue("slot0_a", "a1", "A one");
        queue.Enqueue("slot1_b", "b1", "B one");
        queue.Enqueue("slot0_a", "a2", "A two");

        ContractJournalDelivery delivery;
        True(queue.TryPeekForCharacter("slot0_a", out delivery), "A delivery found");
        Equal("a1", delivery.OccurrenceId, "A preserves FIFO among A entries");
        True(queue.Remove("slot0_a", "a1"), "A first removed");
        True(queue.TryPeekForCharacter("slot0_a", out delivery), "A second delivery found");
        Equal("a2", delivery.OccurrenceId, "A second remains after first removal");
        True(queue.TryPeekForCharacter("slot1_b", out delivery), "B delivery remains isolated");
        Equal("b1", delivery.OccurrenceId, "B occurrence not consumed by A");
        return 7;
    }

    private static int TestOccurrenceDeduplication()
    {
        ContractJournalQueue queue = new ContractJournalQueue();
        False(queue.Enqueue("slot0_a", "same", "old text"), "first enqueue does not drop");
        False(queue.Enqueue("slot0_a", "same", "new text"), "duplicate occurrence updates rather than appends");
        Equal(1, queue.Count, "duplicate occurrence has one queue entry");
        ContractJournalDelivery delivery;
        True(queue.TryPeekForCharacter("slot0_a", out delivery), "deduped delivery found");
        Equal("new text", delivery.Text, "dedupe retains latest text");
        return 5;
    }

    private static int TestBoundedQueue()
    {
        ContractJournalQueue queue = new ContractJournalQueue();
        bool dropped = false;
        for (int i = 0; i < ContractJournalQueue.MaxEntries + 1; i++)
            dropped = queue.Enqueue("slot0_a", "occ" + i.ToString(), "entry " + i.ToString());

        True(dropped, "overflow reports oldest drop");
        Equal(ContractJournalQueue.MaxEntries, queue.Count, "queue remains bounded");
        ContractJournalDelivery delivery;
        True(queue.TryPeekForCharacter("slot0_a", out delivery), "bounded queue still readable");
        Equal("occ1", delivery.OccurrenceId, "oldest entry was dropped at limit");
        return 4;
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
    }

    private static void False(bool condition, string label)
    {
        if (condition) throw new Exception(label);
    }

    private static void Equal(string expected, string actual, string label)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new Exception(label + " expected=" + expected + " actual=" + actual);
    }

    private static void Equal(int expected, int actual, string label)
    {
        if (expected != actual) throw new Exception(label + " expected=" + expected + " actual=" + actual);
    }
}
