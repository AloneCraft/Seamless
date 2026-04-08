namespace Seamless.Collections;

using Tsavorite.core;
public static class Store{
    public static MemoryManagedDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>Create<TKey,TValue>(long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null,bool recoverDevice=false)
        where TKey:IEquatable<TKey>
        where TValue:IEquatable<TValue> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction);
    public static StorageDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>Create<TKey,TValue>(IDevice LogDevice,IDevice ObjectLogDevice,long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null,bool recoverDevice=false)
        where TKey:IEquatable<TKey>
        where TValue:IEquatable<TValue> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir);
    public static MemoryUnmanagedDictionaryStore<TKey,TValue>CreateUnmanaged<TKey,TValue>(long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0)
        where TKey:unmanaged,IEquatable<TKey>
        where TValue:unmanaged,IEquatable<TValue> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction);
    public static StorageUnmanagedDictionaryStore<TKey,TValue>CreateUnmanaged<TKey,TValue>(IDevice LogDevice,IDevice ObjectLogDevice,long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null)
        where TKey:unmanaged,IEquatable<TKey>
        where TValue:unmanaged,IEquatable<TValue> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir);
    /// <summary>TKey Unmanaged, TValue Managed の Dictionary ストアを作成する。</summary>
    public static MemoryManagedDictionaryStore<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>>CreateUnmanagedKey<TKey,TValue>(long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0)
        where TKey:unmanaged,IEquatable<TKey>
        where TValue:IEquatable<TValue> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction);
    /// <summary>TKey Unmanaged, TValue Managed の Dictionary ストアを作成する（ストレージ付き）。</summary>
    public static StorageDictionaryStore<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>>CreateUnmanagedKey<TKey,TValue>(IDevice LogDevice,IDevice ObjectLogDevice,long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null)
        where TKey:unmanaged,IEquatable<TKey>
        where TValue:IEquatable<TValue> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir);
    /// <summary>TKey Managed, TValue Unmanaged の Dictionary ストアを作成する。</summary>
    public static MemoryManagedDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>>CreateUnmanagedValue<TKey,TValue>(long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0)
        where TKey:IEquatable<TKey>
        where TValue:unmanaged,IEquatable<TValue> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction);
    /// <summary>TKey Managed, TValue Unmanaged の Dictionary ストアを作成する（ストレージ付き）。</summary>
    public static StorageDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>>CreateUnmanagedValue<TKey,TValue>(IDevice LogDevice,IDevice ObjectLogDevice,long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null)
        where TKey:IEquatable<TKey>
        where TValue:unmanaged,IEquatable<TValue> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir);
    public static MemoryManagedHashSetStore   <T          >Create<T          >(long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null,bool recoverDevice=false)
        where T:IEquatable<T> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction);
    public static StorageHashSetStore<T> Create<T>(IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir=null,long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,bool recoverDevice=false)
        where T:IEquatable<T> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir);
}
