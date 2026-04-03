using Tsavorite.core;

namespace Seamless.Collections;
public abstract partial class CollectionStore<TKey, TValue>where TKey : IEquatable<TKey> {
    private partial void Initialize(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir){
        this.Settings = new(){
            IndexSize=IndexSize,
            MemorySize=MemorySize,
            PageSize=PageSize,
            SegmentSize=SegmentSize,

            MutableFraction=MutableFraction,
            LogDevice=LogDevice,
            ObjectLogDevice=ObjectLogDevice,

            CheckpointDir=CheckpointDir,
        };
	}
    /// <summary>
    /// UntilAddress までのログを compaction します。返り値は compaction 後の BeginAddress です。
    /// </summary>
    /// <param name="CompactUntil"></param>
    /// <returns>新しいUntilAddress</returns>
    private partial long Compact(long CompactUntil){
        var UntilAddress = this.Store.Log.Compact<SpanByte,SpanByteAndMemory,Empty,SpanByteFunctions<Empty>>(
            new SpanByteFunctions<Empty>(),
            CompactUntil,
            CompactionType.Lookup
        );
        return UntilAddress;
	}
}
