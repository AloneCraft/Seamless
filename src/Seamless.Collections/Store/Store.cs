namespace Seamless.Collections;
using Common;

using Tsavorite.core;
public static class Store{
    public static MemoryDictionaryStore<TKey,TValue>Create<TKey,TValue>(long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null,bool recoverDevice=false)
        where TKey:IEquatable<TKey>
        where TValue:IEquatable<TValue> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction);
    public static StorageDictionaryStore<TKey,TValue>Create<TKey,TValue>(IDevice LogDevice,IDevice ObjectLogDevice,long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null,bool recoverDevice=false)
        where TKey:IEquatable<TKey>
        where TValue:IEquatable<TValue> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir);
    public static MemoryHashSetStore   <T          >Create<T          >(long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null,bool recoverDevice=false)
        where T:IEquatable<T> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction);
    public static StorageHashSetStore<T> Create<T>(IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir=null,long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,bool recoverDevice=false)
        where T:IEquatable<T> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir);
}
