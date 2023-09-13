using System.Collections.Concurrent;
using Ilmn.Das.App.Wittyer.Vcf.Variants;

namespace Ilmn.Das.App.Wittyer.Infrastructure;

public class PrimaryCategory
{
    public virtual bool HasBaseLevelStats => MainType.HasBaseLevelStats;
    public virtual bool HasLengths => MainType.HasLengths;
    public readonly WittyerType MainType;

    protected PrimaryCategory(WittyerType mainType) => MainType = mainType;

    public bool Is(WittyerType type) => this is not Category && MainType == type;
    protected static PrimaryCategory CreateProtected(WittyerType mainType) => new(mainType);
    public override string ToString() => MainType.ToString();
}

public class Category : PrimaryCategory
{
    private static readonly ConcurrentDictionary<string, PrimaryCategory> InstanceCache = new();
    public WittyerType SecondaryType { get; }
    public override bool HasLengths => base.HasLengths && SecondaryType.HasLengths;
    public override bool HasBaseLevelStats => base.HasBaseLevelStats && SecondaryType.HasBaseLevelStats;

    private Category(WittyerType mainType, WittyerType secondaryType) : base(mainType) => SecondaryType = secondaryType;

    public static Category Create(WittyerType mainType, WittyerType secondaryType)
    {
        var ret = new Category(mainType, secondaryType);
        var key = ret.ToString();
        return (Category) InstanceCache.GetOrAdd(key, ret);
    }

    public static PrimaryCategory Create(WittyerType mainType)
    {
        var ret = CreateProtected(mainType);
        var key = ret.ToString();
        return InstanceCache.GetOrAdd(key, ret);
    }

    public override string ToString() => $"{MainType}+{SecondaryType}";
}