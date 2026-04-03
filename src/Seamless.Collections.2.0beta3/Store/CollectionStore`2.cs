using Common;

using System.Diagnostics;
using Tsavorite.core;

namespace Seamless.Collections;
/// <summary>
/// Tsavorite ストアを管理する抽象基底クラス。
/// DictionaryStore および HashSetStore の共通実装を提供する。
/// </summary>
public abstract partial class CollectionStore<TKey, TValue>where TKey : IEquatable<TKey> {
    private partial void Initialize(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir){
        long ObjectLogSegmentSize;
        if(ObjectLogDevice.Capacity<0) {
            ObjectLogSegmentSize=Const.MinObjectLogCapacity;
        } else {
            Debug.Assert(ObjectLogDevice.Capacity%SegmentSize==0,$"ObjectLogDevice.Capacity must be a multiple of SegmentSize ({SegmentSize}), but was {ObjectLogDevice.Capacity}");
            ObjectLogSegmentSize=ObjectLogDevice.Capacity;
        }
        this.Settings=new(){
            IndexSize=IndexSize,
            LogMemorySize=MemorySize,
            PageSize=PageSize,
            SegmentSize=SegmentSize,

            MutableFraction=MutableFraction,
            LogDevice=LogDevice,
            ObjectLogDevice=ObjectLogDevice,
            ObjectLogSegmentSize=ObjectLogSegmentSize,
            CheckpointDir=CheckpointDir,
        };
    }
    private partial long Compact(long CompactUntil){
        var UntilAddress=this.Store.Log.Compact<TKey,TValue,Empty>(
            CompactUntil,
            CompactionType.Lookup
        );
        return UntilAddress;
    }
}
