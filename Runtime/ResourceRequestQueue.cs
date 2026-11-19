using System;
using System.Collections.Generic;

namespace GGFramework.Core
{
    /// <summary>
    /// Single-threaded keyed work admission: queued and in-flight work deduplicate,
    /// FIFO starts are bounded, and only the current lease can complete a request.
    /// The host supplies actual IO, cancellation and result binding. All access
    /// must use the same owning thread. Factories must not mutate this queue.
    /// </summary>
    public sealed class ResourceRequestQueue<TKey, TRequest> where TRequest : class
    {
        public sealed class Lease
        {
            /// <summary>The key supplied when this lease was created.</summary>
            public TKey Key { get; private set; }
            /// <summary>The host request; this queue never disposes it.</summary>
            public TRequest Value { get; private set; }
            internal bool Running;
            internal Lease(TKey key, TRequest value) { Key = key; Value = value; }
        }

        readonly Dictionary<TKey, Lease> pending;
        readonly Queue<Lease> queued = new Queue<Lease>();
        bool pumping;
        bool creating;
        int maxConcurrency;

        public ResourceRequestQueue(int maxConcurrency, IEqualityComparer<TKey> comparer = null)
        {
            if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));
            this.maxConcurrency = maxConcurrency;
            pending = new Dictionary<TKey, Lease>(comparer ?? EqualityComparer<TKey>.Default);
        }

        /// <summary>Queued plus running requests.</summary>
        public int PendingCount => pending.Count;
        /// <summary>Logical admissions, not a count of live host IO operations.</summary>
        public int ActiveCount { get; private set; }
        public int PeakActiveCount { get; private set; }
        public int CancelledCount { get; private set; }
        public int MaxConcurrency
        {
            get => maxConcurrency;
            set
            {
                CheckFactoryMutation();
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
                maxConcurrency = value;
            }
        }

        void CheckFactoryMutation()
        {
            if (creating) throw new InvalidOperationException("A request factory cannot mutate its resource queue.");
        }

        /// <summary>
        /// Returns the current queued/running lease without invoking create, or
        /// constructs and queues a new request. A failed factory registers nothing.
        /// The factory may inspect this queue but must not call its mutating APIs.
        /// </summary>
        public Lease GetOrCreate(TKey key, Func<TRequest> create)
        {
            CheckFactoryMutation();
            Lease entry;
            if (pending.TryGetValue(key, out entry)) return entry;
            if (create == null) throw new ArgumentNullException(nameof(create));
            TRequest value;
            creating = true;
            try { value = create(); }
            finally { creating = false; }
            if (value == null) throw new InvalidOperationException("A request factory returned null.");
            entry = new Lease(key, value);
            pending.Add(key, entry);
            queued.Enqueue(entry);
            return entry;
        }

        /// <summary>Tests identity, including queued work; stale and foreign leases are false.</summary>
        public bool IsCurrent(Lease lease)
        {
            Lease current;
            return lease != null && pending.TryGetValue(lease.Key, out current) && ReferenceEquals(current, lease);
        }

        /// <summary>
        /// Starts FIFO work up to MaxConcurrency. Call again after asynchronous
        /// completion or a limit increase. Nested Pump calls from start are ignored;
        /// the outer pump continues. A thrown start detaches its lease and is rethrown.
        /// The host must stop any IO it started before throwing.
        /// </summary>
        public void Pump(Action<Lease> start)
        {
            CheckFactoryMutation();
            if (start == null) throw new ArgumentNullException(nameof(start));
            if (pumping) return;
            pumping = true;
            try
            {
                while (ActiveCount < maxConcurrency && queued.Count > 0)
                {
                    var entry = queued.Dequeue();
                    if (!IsCurrent(entry)) continue;
                    entry.Running = true;
                    ActiveCount++;
                    PeakActiveCount = Math.Max(PeakActiveCount, ActiveCount);
                    try { start(entry); }
                    catch { Complete(entry); throw; }
                }
            }
            finally { pumping = false; }
        }

        /// <summary>
        /// Detaches a current running lease and frees its slot once. Does not pump,
        /// dispose the request, bind its result, or accept queued/foreign/stale leases.
        /// </summary>
        public bool Complete(Lease lease)
        {
            CheckFactoryMutation();
            if (!IsCurrent(lease) || !lease.Running) return false;
            pending.Remove(lease.Key);
            lease.Running = false;
            ActiveCount--;
            return true;
        }

        /// <summary>
        /// Detaches queued and running leases before returning them in unspecified
        /// order. The host must then abort their IO and release any late results.
        /// Old callbacks cannot consume a same-key replacement's admission slot.
        /// </summary>
        public Lease[] CancelAll()
        {
            CheckFactoryMutation();
            var removed = new Lease[pending.Count];
            pending.Values.CopyTo(removed, 0);
            CancelledCount += removed.Length;
            pending.Clear();
            queued.Clear();
            ActiveCount = 0;
            foreach (var entry in removed) entry.Running = false;
            return removed;
        }
    }
}
