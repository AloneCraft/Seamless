using System.Runtime.CompilerServices;
namespace Seamless.Stores;

/// <summary>Managed/Unmanaged オーバーロード解決用タグ。</summary>
public readonly struct ManagedTag;
/// <summary>Managed/Unmanaged オーバーロード解決用タグ。</summary>
public readonly struct UnmanagedTag;
/// <summary>
/// Store ファクトリ。
/// Create オーバーロードは default 引数で ManagedTag/UnmanagedTag を受け取ることで、Managed/Unmanaged のオーバーロード解決を行う。
/// </summary>
public readonly struct HashStore {
    private readonly long IndexSize, PageSize, SegmentSize, MemorySize;
    private readonly double MutableFraction;
    private readonly string? CheckpointDir;
    private HashStore(
        long IndexSize,
        long PageSize,
        long SegmentSize,
        long MemorySize,
        double MutableFraction,
        string? CheckpointDir
    ) {
        this.IndexSize=IndexSize;
        this.PageSize=PageSize;
        this.SegmentSize=SegmentSize;
        this.MemorySize=MemorySize;
        this.MutableFraction=MutableFraction;
        this.CheckpointDir=CheckpointDir;
    }
    /// <summary>構成パラメータを指定して HashStore を生成する。</summary>
    public static HashStore Create(
        long IndexSize = Const.IndexSize,
        long PageSize = Const.PageSize,
        long SegmentSize = Const.SegmentSize,
        long MemorySize = Const.LogMemorySize,
        double MutableFraction = 1.0,
        string? CheckpointDir = null
    ) => new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir);

    // ── Memory Dictionary ──────────────────────────────────────────
    /// <summary>Managed Key × Managed Value</summary>
    public ManagedManagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        ManagedTag _0 = default,
        ManagedTag _1 = default
    )
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Managed Key × Unmanaged Value</summary>
    [OverloadResolutionPriority(1)]
    public ManagedUnmanagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        ManagedTag _0 = default,
        UnmanagedTag _1 = default
    )
        where TKey : IEquatable<TKey>
        where TValue : unmanaged, IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Unmanaged Key × Managed Value</summary>
    [OverloadResolutionPriority(1)]
    public UnmanagedManagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        UnmanagedTag _0 = default,
        ManagedTag _1 = default
    )
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Unmanaged Key × Unmanaged Value</summary>
    [OverloadResolutionPriority(2)]
    public UnmanagedUnmanagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        UnmanagedTag _0 = default,
        UnmanagedTag _1 = default
    )
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged, IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);

    // ── Storage Dictionary ─────────────────────────────────────────
    /// <summary>Managed Key × Managed Value（ストレージ付き）</summary>
    public ManagedManagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        ManagedTag _0 = default,
        ManagedTag _1 = default
    )
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
    /// <summary>Managed Key × Unmanaged Value（ストレージ付き）</summary>
    [OverloadResolutionPriority(1)]
    public ManagedUnmanagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        ManagedTag _0 = default,
        UnmanagedTag _1 = default
    )
        where TKey : IEquatable<TKey>
        where TValue : unmanaged, IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
    /// <summary>Unmanaged Key × Managed Value（ストレージ付き）</summary>
    [OverloadResolutionPriority(1)]
    public UnmanagedManagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        UnmanagedTag _0 = default,
        ManagedTag _1 = default
    )
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
    /// <summary>Unmanaged Key × Unmanaged Value（ストレージ付き）</summary>
    [OverloadResolutionPriority(2)]
    public UnmanagedUnmanagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        UnmanagedTag _0 = default,
        UnmanagedTag _1 = default
    )
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged, IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);

    // ── Managed HashSet ────────────────────────────────────────────
    /// <summary>Memory ManagedHashSet</summary>
    public ManagedUnmanagedHashSetStore<TKey> Create<TKey>(ManagedTag _0 = default)
        where TKey : IEquatable<TKey> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Storage ManagedHashSet</summary>
    public ManagedUnmanagedHashSetStore<TKey> Create<TKey>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        ManagedTag _0 = default
    )
        where TKey : IEquatable<TKey> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);

    // ── Unmanaged HashSet ──────────────────────────────────────────
    /// <summary>Memory UnmanagedHashSet</summary>
    [OverloadResolutionPriority(1)]
    public UnmanagedUnmanagedHashSetStore<TKey> Create<TKey>(UnmanagedTag _0 = default)
        where TKey : unmanaged, IEquatable<TKey> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Storage UnmanagedHashSet</summary>
    [OverloadResolutionPriority(1)]
    public UnmanagedUnmanagedHashSetStore<TKey> Create<TKey>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        UnmanagedTag _0 = default
    )
        where TKey : unmanaged, IEquatable<TKey> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
}