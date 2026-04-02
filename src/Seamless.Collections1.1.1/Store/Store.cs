using Common;
using Tsavorite.core;

namespace Seamless.Collections;
public static class Store{
    public static DictionaryStore<TKey,TValue>Create<TKey,TValue>(IDevice LogDevice,IDevice ObjectLogDevice,long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null,bool recoverDevice=false)
        where TKey:IEquatable<TKey>
        where TValue:IEquatable<TValue> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir,recoverDevice,LogDevice,ObjectLogDevice);
    public static DictionaryStore<TKey,TValue>Create<TKey,TValue>(long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null,bool recoverDevice=false)
        where TKey:IEquatable<TKey>
        where TValue:IEquatable<TValue> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir,recoverDevice);
    public static HashSetStore   <T          >Create<T          >(long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null,bool recoverDevice=false)
        where T:IEquatable<T> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir,recoverDevice);
    public static HashSetStore   <T          >Create<T          >(IDevice LogDevice,IDevice ObjectLogDevice,long IndexSize=Const.IndexSize,long PageSize=Const.PageSize,long SegmentSize=Const.SegmentSize,long MemorySize=Const.LogMemorySize,double MutableFraction=1.0,string? CheckpointDir=null,bool recoverDevice=false)
        where T:IEquatable<T> =>
        new(LogDevice,ObjectLogDevice,IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir,recoverDevice);
}
