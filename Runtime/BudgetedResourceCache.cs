using System;
using System.Collections.Generic;

namespace GGFramework.Core
{
    /// <summary>
    /// Single-threaded weighted LRU storage. Access pins an entry to the current
    /// ownership scope. Only unpinned values are evicted; the active scope may
    /// exceed the total-byte budget. Resource creation and destruction belong to
    /// the host. Pins are scope-wide booleans, not per-consumer reference counts.
    /// </summary>
    public sealed class BudgetedResourceCache<TKey, TValue>
    {
        sealed class Entry
        {
            public TValue Value;
            public long Bytes;
            public bool Pinned;
            public LinkedListNode<TKey> Recency;
        }

        readonly Dictionary<TKey, Entry> entries;
        readonly LinkedList<TKey> recency = new LinkedList<TKey>();
        readonly Action<TValue> release;
        long budgetBytes;
        bool releasing;

        public BudgetedResourceCache(long budgetBytes, Action<TValue> release, IEqualityComparer<TKey> comparer = null)
        {
            this.release = release ?? throw new ArgumentNullException(nameof(release));
            entries = new Dictionary<TKey, Entry>(comparer ?? EqualityComparer<TKey>.Default);
            this.budgetBytes = Math.Max(0, budgetBytes);
        }

        public int Count => entries.Count;
        /// <summary>Total declared weight of all owned entries, including pinned ones.</summary>
        public long TotalBytes { get; private set; }
        /// <summary>Declared weight protected from trimming in the current scope.</summary>
        public long PinnedBytes { get; private set; }
        /// <summary>Removed entries over this cache's lifetime, including explicit Clear.</summary>
        public int Evictions { get; private set; }
        /// <summary>Negative values clamp to zero. Setting the budget trims immediately.</summary>
        public long BudgetBytes
        {
            get => budgetBytes;
            set { CheckMutation(); budgetBytes = Math.Max(0, value); TrimToBudget(); }
        }

        void CheckMutation()
        {
            if (releasing) throw new InvalidOperationException("A release callback cannot mutate its resource cache.");
        }

        /// <summary>
        /// Adds a uniquely keyed owner without trimming. Weight must be nonnegative
        /// and fit TotalBytes. Validation failures leave ownership with the caller.
        /// Call TrimToBudget explicitly after publishing the loaded value.
        /// </summary>
        public void Add(TKey key, TValue value, long bytes, bool pinned = true)
        {
            CheckMutation();
            if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
            if (entries.ContainsKey(key)) throw new ArgumentException("A resource with this key already exists.", nameof(key));
            long total = checked(TotalBytes + bytes);
            var entry = new Entry { Value = value, Bytes = bytes, Pinned = pinned };
            entry.Recency = recency.AddLast(key);
            entries.Add(key, entry);
            TotalBytes = total;
            if (pinned) PinnedBytes += bytes;
        }

        /// <summary>On a hit, touches LRU order and pins the entry until UnpinAll.</summary>
        public bool TryGet(TKey key, out TValue value)
        {
            CheckMutation();
            Entry entry;
            if (!entries.TryGetValue(key, out entry)) { value = default(TValue); return false; }
            if (!entry.Pinned) { entry.Pinned = true; PinnedBytes += entry.Bytes; }
            recency.Remove(entry.Recency);
            recency.AddLast(entry.Recency);
            value = entry.Value;
            return true;
        }

        /// <summary>Ends the entire ownership scope. Does not release or trim any entry.</summary>
        public void UnpinAll()
        {
            CheckMutation();
            foreach (var entry in entries.Values) entry.Pinned = false;
            PinnedBytes = 0;
        }

        public void TrimToBudget() => Trim(budgetBytes);

        /// <summary>
        /// Removes oldest idle entries toward a nonnegative target; pins may prevent
        /// reaching it. All removed entries are detached before release callbacks.
        /// Callback failures aggregate after every release has been attempted.
        /// </summary>
        public void Trim(long targetBytes)
        {
            CheckMutation();
            targetBytes = Math.Max(0, targetBytes);
            var removed = new List<TValue>();
            var node = recency.First;
            while (node != null && TotalBytes > targetBytes)
            {
                var next = node.Next;
                var entry = entries[node.Value];
                if (!entry.Pinned)
                {
                    entries.Remove(node.Value);
                    recency.Remove(node);
                    TotalBytes -= entry.Bytes;
                    Evictions++;
                    removed.Add(entry.Value);
                }
                node = next;
            }
            ReleaseValues(removed);
        }

        /// <summary>
        /// Hard teardown, including pinned entries. Call only after host consumers
        /// stop using them. Failures aggregate; detached resources are not retried.
        /// Release callbacks must not mutate this cache.
        /// </summary>
        public void Clear()
        {
            CheckMutation();
            var removed = new List<TValue>();
            foreach (var key in recency) removed.Add(entries[key].Value);
            Evictions += entries.Count;
            entries.Clear();
            recency.Clear();
            TotalBytes = PinnedBytes = 0;
            ReleaseValues(removed);
        }

        void ReleaseValues(List<TValue> values)
        {
            if (values.Count == 0) return;
            var errors = new List<Exception>();
            releasing = true;
            try
            {
                foreach (var value in values)
                {
                    try { release(value); }
                    catch (Exception error) { errors.Add(error); }
                }
            }
            finally { releasing = false; }
            if (errors.Count > 0) throw new AggregateException("One or more resources could not be released.", errors);
        }
    }
}
