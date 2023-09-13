using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Ilmn.Das.App.Wittyer.Stats.Counts;

namespace Ilmn.Das.App.Wittyer.Stats
{
    internal class BinnedDictionary : IReadOnlyDictionary<uint?, IMutableStats>
    {
        public readonly IReadOnlyList<(uint size, bool skip)> Bins;

        private readonly IDictionary<uint, IMutableStats> _statsDictionary;

        public readonly IMutableStats? UnbinnedStats;
        
        public BinnedDictionary(IReadOnlyList<(uint size, bool skip)> bins, bool hasBaseLevelStats, bool hasLengths)
        {
            Bins = bins;
            UnbinnedStats = hasLengths
                ? null // has Lengths means no unbinned stats.
                : hasBaseLevelStats
                    ? MutableEventAndBasesStats.Create()
                    : MutableEventStats.Create();
            _statsDictionary = bins.ToImmutableDictionary(b => b.size,
                b => hasBaseLevelStats
                    ? MutableEventAndBasesStats.Create()
                    : MutableEventStats.Create() as IMutableStats);
        }

        public IEnumerator<KeyValuePair<uint?, IMutableStats>> GetEnumerator()
        {
            if (UnbinnedStats != null)
                yield return new KeyValuePair<uint?, IMutableStats>(null, UnbinnedStats);
            foreach (var (key, value) in _statsDictionary)
                yield return new KeyValuePair<uint?, IMutableStats>(key, value);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => _statsDictionary.Count;
        public bool ContainsKey(uint? key) => key == null ? UnbinnedStats != null : _statsDictionary.ContainsKey(key.Value);

        public bool TryGetValue(uint? key, out IMutableStats? value)
        {
            value = UnbinnedStats;
            return key == null ? UnbinnedStats != null : _statsDictionary.TryGetValue(key.Value, out value);
        }

        public IMutableStats this[uint? key] => key == null
            ? UnbinnedStats ??
              throw new NullReferenceException($"Tried to access {nameof(UnbinnedStats)} when all are binned by size!")
            : _statsDictionary[key.Value];

        public IEnumerable<uint?> Keys
        {
            get
            {
                if (UnbinnedStats != null)
                    yield return null;
                foreach (var key in _statsDictionary.Keys)
                    yield return key;
            }
        }

        public IEnumerable<IMutableStats> Values
        {
            get
            {
                if (UnbinnedStats != null)
                    yield return UnbinnedStats;
                foreach (var value in _statsDictionary.Values)
                    yield return value;
            }
        }
    }
}