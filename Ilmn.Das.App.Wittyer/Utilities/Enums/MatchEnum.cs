using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using Bio.Util;
using Ilmn.Das.Std.AppUtils.Comparers;
using Ilmn.Das.Std.AppUtils.Enums;
using Ilmn.Das.Std.VariantUtils.Vcf;

namespace Ilmn.Das.App.Wittyer.Utilities.Enums
{
    public class MatchSet : IImmutableSet<MatchEnum>
    {
        private readonly IImmutableSet<MatchEnum> _baseSet;
        public static readonly MatchSet Empty = new(ImmutableHashSet<MatchEnum>.Empty);
        
        /// <summary>
        /// Local Match
        /// </summary>
        public static readonly MatchSet LocalMatch =
            new(ImmutableHashSet.Create(MatchEnum.Coordinate));
        
        /// <summary>
        /// Allele Match
        /// </summary>
        public static readonly MatchSet AlleleMatch = LocalMatch.Add(MatchEnum.Allele);
        public static readonly MatchSet AlleleAndLengthMatch = AlleleMatch.Add(MatchEnum.Length);

        public MatchSet(IImmutableSet<MatchEnum> baseSet) => _baseSet = baseSet;

        /// <inheritdoc/>
        public IEnumerator<MatchEnum> GetEnumerator() => _baseSet.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_baseSet).GetEnumerator();

        /// <inheritdoc/>
        public int Count => _baseSet.Count;

        public MatchSet Add(MatchEnum value) => new(((IImmutableSet<MatchEnum>)this).Add(value));

        IImmutableSet<MatchEnum> IImmutableSet<MatchEnum>.Add(MatchEnum value)
            => SetEquals(Empty)
                ? ImmutableHashSet.Create(value)
                : _baseSet.Add(value);

        public MatchSet Clear() => Empty;
        
        /// <inheritdoc/>
        IImmutableSet<MatchEnum> IImmutableSet<MatchEnum>.Clear() => Empty;

        /// <inheritdoc/>
        public bool Contains(MatchEnum value) => _baseSet.Contains(value);

        public MatchSet Except(IEnumerable<MatchEnum> other) => new(_baseSet.Except(other));

        /// <inheritdoc/>
        IImmutableSet<MatchEnum> IImmutableSet<MatchEnum>.Except(IEnumerable<MatchEnum> other) => _baseSet.Except(other);
        
        public MatchSet Intersect(IEnumerable<MatchEnum> other) => new(_baseSet.Intersect(other));
        
        /// <inheritdoc/>
        IImmutableSet<MatchEnum> IImmutableSet<MatchEnum>.Intersect(IEnumerable<MatchEnum> other) => _baseSet.Except(other);
        
        /// <inheritdoc/>
        public bool IsProperSubsetOf(IEnumerable<MatchEnum> other) => _baseSet.IsProperSubsetOf(other);
        
        /// <inheritdoc/>
        public bool IsProperSupersetOf(IEnumerable<MatchEnum> other) => _baseSet.IsProperSupersetOf(other);
        
        /// <inheritdoc/>
        public bool IsSubsetOf(IEnumerable<MatchEnum> other) => _baseSet.IsSubsetOf(other);
        
        /// <inheritdoc/>
        public bool IsSupersetOf(IEnumerable<MatchEnum> other) => _baseSet.IsSupersetOf(other);
        
        /// <inheritdoc/>
        public bool Overlaps(IEnumerable<MatchEnum> other) => _baseSet.Overlaps(other);

        public MatchSet Remove(MatchEnum value)
            => Contains(value)
                ? Count == 1 ? Empty : new(_baseSet.Remove(value))
                : this;
        
        /// <inheritdoc/>
        IImmutableSet<MatchEnum> IImmutableSet<MatchEnum>.Remove(MatchEnum value) => Remove(value);
        
        /// <inheritdoc/>
        public bool SetEquals(IEnumerable<MatchEnum> other) => _baseSet.SetEquals(other);
        
        public MatchSet SymmetricExcept(IEnumerable<MatchEnum> other) => new(_baseSet.SymmetricExcept(other));
        
        /// <inheritdoc/>
        IImmutableSet<MatchEnum> IImmutableSet<MatchEnum>.SymmetricExcept(IEnumerable<MatchEnum> other)
            => _baseSet.SymmetricExcept(other);
        
        /// <inheritdoc/>
        public bool TryGetValue(MatchEnum equalValue, out MatchEnum actualValue)
            => _baseSet.TryGetValue(equalValue, out actualValue);

        public MatchSet Union(IEnumerable<MatchEnum> other)
            => new(SetEquals(Empty)
                ? ImmutableHashSet.CreateRange(other)
                : _baseSet.Union(other).Remove(MatchEnum.Unmatched));
        
        /// <inheritdoc/>
        IImmutableSet<MatchEnum> IImmutableSet<MatchEnum>.Union(IEnumerable<MatchEnum> other) => Union(other);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is MatchSet cast && SetEquals(cast);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCodeUtils.GenerateForEnumerablesStruct(this, false);

        /// <inheritdoc/>
        public override string ToString()
            => Count == 0
                ? VcfConstants.MissingValueString
                : this
                    .OrderBy(it => (int)it).Select(it => it.ToStringDescription())
                    .StringJoin(WittyerConstants.BorderDistanceDelimiter);
    }
    
    /// <summary>
    /// Value used in WHAT sample tag
    /// </summary>
    public enum MatchEnum
    {
        /// <summary>
        /// Unknown value, usually non-overlapping or not supported type
        /// </summary>
        [Description(VcfConstants.MissingValueString)]
        Unmatched = 0,

        /// <summary>
        /// Coordinate match, meaning limited overlap (not reaching threshold), but location is approximately correct.
        /// This match type is essentially a pre-requisite for all other matches since we only ever compare when coordinate location is correct.
        /// </summary>
        [Description("c")]
        Coordinate = 1,
        
        /// <summary>
        /// Genotype match, this can combined with any others besides Unmatched
        /// </summary>
        [Description("g")]
        Genotype = 2,

        /// <summary>
        /// Allele match, distance overlap meet minimum threshold and also local match (pre-requisite).
        /// Says nothing about sequence matching. The Length matches for all types with Length except Insertions.
        /// Also, for CopyNumber Variants that are not CNTR means CN matches.
        /// </summary>
        [Description("a")]
        Allele = 4,

        /// <summary>
        /// Length match, distance overlap meet minimum threshold and also local match (pre-requisite).
        /// Says nothing about sequence matching.
        /// This is same as Allele match, except that Length matches for all types, including Insertions and for
        /// CopyNumber Variants, if CN/RUC mismatches, it would not be an Allele match but could still be a Length match.
        /// </summary>
        [Description("l")]
        Length = 8,

        /// <summary>
        /// Partial sequence match 
        /// </summary>
        [Description("p")]
        PartialSequence = 16,

        /// <summary>
        /// Sequence match (within threshold), means length and coordinate also correct.
        /// </summary>
        [Description("s")]
        Sequence = 32,
    }
}
