using System.Runtime.CompilerServices;
using Seamless.Collections;
namespace Seamless.Store;

/// <summary>Managed/Unmanaged オーバーロード解決用タグ。</summary>
public readonly struct ManagedTag;
/// <summary>Managed/Unmanaged オーバーロード解決用タグ。</summary>
public readonly struct UnmanagedTag;

/// <summary>Store ファクトリ。</summary>
public readonly struct Factory{
    private readonly long IndexSize,PageSize,SegmentSize,MemorySize;
    private readonly double MutableFraction;
    private readonly string? CheckpointDir;
    private Factory(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir){
        this.IndexSize=IndexSize;
        this.PageSize=PageSize;
        this.SegmentSize=SegmentSize;
        this.MemorySize=MemorySize;
        this.MutableFraction=MutableFraction;
        this.CheckpointDir=CheckpointDir;
    }
    /// <summary>構成パラメータを指定して Factory を生成する。</summary>
    public static Factory Create(long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null)
        => new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir);
    // ── Memory Dictionary ──────────────────────────────────────────
    /// <summary>Managed Key × Managed Value</summary>
    public MemoryManagedDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>Create<TKey,TValue>(ManagedTag _0=default,ManagedTag _1=default)
        where TKey:IEquatable<TKey>
        where TValue:IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Managed Key × Unmanaged Value</summary>
    [OverloadResolutionPriority(1)]public MemoryManagedDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>>Create<TKey,TValue>(ManagedTag _0=default,UnmanagedTag _1=default)
        where TKey:IEquatable<TKey>
        where TValue:unmanaged,IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Unmanaged Key × Managed Value</summary>
    [OverloadResolutionPriority(1)]public MemoryManagedDictionaryStore<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>>Create<TKey,TValue>(UnmanagedTag _0=default,ManagedTag _1=default)
        where TKey:unmanaged,IEquatable<TKey>
        where TValue:IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Unmanaged Key × Unmanaged Value</summary>
    [OverloadResolutionPriority(2)]public MemoryUnmanagedDictionaryStore<TKey,TValue>Create<TKey,TValue>(UnmanagedTag _0=default,UnmanagedTag _1=default)
        where TKey:unmanaged,IEquatable<TKey>
        where TValue:unmanaged,IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    // ── Storage Dictionary ─────────────────────────────────────────
    /// <summary>Managed Key × Managed Value（ストレージ付き）</summary>
    public StorageDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>Create<TKey,TValue>(IDevice LogDevice,IDevice ObjectLogDevice,ManagedTag _0=default,ManagedTag _1=default)
        where TKey:IEquatable<TKey>
        where TValue:IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
    /// <summary>Managed Key × Unmanaged Value（ストレージ付き）</summary>
    [OverloadResolutionPriority(1)]public StorageDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>>Create<TKey,TValue>(IDevice LogDevice,IDevice ObjectLogDevice,ManagedTag _0=default,UnmanagedTag _1=default)
        where TKey:IEquatable<TKey>
        where TValue:unmanaged,IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
    /// <summary>Unmanaged Key × Managed Value（ストレージ付き）</summary>
    [OverloadResolutionPriority(1)]public StorageDictionaryStore<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>>Create<TKey,TValue>(IDevice LogDevice,IDevice ObjectLogDevice,UnmanagedTag _0=default,ManagedTag _1=default)
        where TKey:unmanaged,IEquatable<TKey>
        where TValue:IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
    /// <summary>Unmanaged Key × Unmanaged Value（ストレージ付き）</summary>
    [OverloadResolutionPriority(2)]public StorageUnmanagedDictionaryStore<TKey,TValue>Create<TKey,TValue>(IDevice LogDevice,IDevice ObjectLogDevice,UnmanagedTag _0=default,UnmanagedTag _1=default)
        where TKey:unmanaged,IEquatable<TKey>
        where TValue:unmanaged,IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
    // ── HashSet ────────────────────────────────────────────────────
    /// <summary>Memory ManagedHashSet</summary>
    public MemoryManagedHashSetStore<T>Create<T>()
        where T:IEquatable<T> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Storage ManagedHashSet</summary>
    public StorageHashSetStore<T>Create<T>(IDevice LogDevice,IDevice ObjectLogDevice)
        where T:IEquatable<T> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
    // ── Unmanaged HashSet ──────────────────────────────────────────
    /// <summary>Memory UnmanagedHashSet</summary>
    public MemoryUnmanagedHashSetStore<TKey>CreateUnmanagedHashSet<TKey>()
        where TKey:unmanaged,IEquatable<TKey> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
}
