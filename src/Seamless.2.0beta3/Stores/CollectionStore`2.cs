using System.Diagnostics;
namespace Seamless.Stores;

public abstract partial class CollectionStore<
    TKey,
    TValue,
    TStoreFunctions,
    TAllocator
>
    where TKey:IEquatable<TKey>
    where TStoreFunctions:IStoreFunctions
    where TAllocator:IAllocator<TStoreFunctions>{
    private protected KVSettings Settings{get; private set;}
    public readonly TsavoriteKV<TStoreFunctions,TAllocator> Store;
    /// <summary>テスト専用シーム: TakeCheckpointAsync 完了後・CompleteCheckpointAsync 前に呼び出されるコールバック。</summary>
    private protected CollectionStore(
        TStoreFunctions StoreFunctions,
        Func<AllocatorSettings,TStoreFunctions,TAllocator> allocatorFactory,
        long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind){
        AssertIsPowerOfTwo(IndexSize,nameof(IndexSize));
        AssertIsPowerOfTwo(PageSize,nameof(PageSize));
        AssertIsPowerOfTwo(SegmentSize,nameof(SegmentSize));
        AssertIsPowerOfTwo(MemorySize,nameof(MemorySize));
        Debug.Assert(IndexSize>=64,$"{nameof(IndexSize)} must be >= 64");
        Debug.Assert(PageSize>=1L<<12,$"{nameof(PageSize)} must be >= 4096");
        Debug.Assert(PageSize<=1L<<30,$"{nameof(PageSize)} must be <= 1GB (1L<<30)");
        Debug.Assert(PageSize<=SegmentSize,"SegmentSize must be at least equal to PageSize.");
        Debug.Assert(MemorySize>=2*PageSize,$"{nameof(MemorySize)} must be >= 2*{nameof(PageSize)}");
        Debug.Assert(SegmentSize<=1L<<62,$"{nameof(SegmentSize)} must be <= 1L<<62");
        Debug.Assert(ObjectLogDevice.Capacity is>=Const.MinObjectLogCapacity and<=Const.MaxObjectLogCapacity or-1);
        this.LogMemorySize=MemorySize;
        this.MutableFraction=MutableFraction;
        this.OverflowMutableStrategyKind=OverflowMutableStrategyKind;
        //MemoryOnlyDevice使用時はシリアライザ不要。ページデータはヒープ上で保持されるためインライン格納で十分。
        long objectLogSegmentSize;
        if(ObjectLogDevice.Capacity<0){
            objectLogSegmentSize=Const.MinObjectLogCapacity;
        }
        else{
            Debug.Assert(ObjectLogDevice.Capacity%SegmentSize==0,$"ObjectLogDevice.Capacity must be a multiple of SegmentSize ({SegmentSize}), but was {ObjectLogDevice.Capacity}");
            objectLogSegmentSize=ObjectLogDevice.Capacity;
        }
        this.Settings=new(){
            IndexSize=IndexSize,
            LogMemorySize=MemorySize,
            PageSize=PageSize,
            SegmentSize=SegmentSize,

            MutableFraction=MutableFraction,
            LogDevice=LogDevice,
            ObjectLogDevice=ObjectLogDevice,
            ObjectLogSegmentSize=objectLogSegmentSize,
            CheckpointDir=CheckpointDir,
        };
        this.Store=new TsavoriteKV<TStoreFunctions,TAllocator>(
            this.Settings,
            StoreFunctions,
            allocatorFactory
        );
    }
    public void CompactWithLookup(){
        var store=this.Store;
        var beginAddress=store.Log.BeginAddress;
        var segmentSize=this.Settings.SegmentSize;
        var nextSegmentStart=(beginAddress/segmentSize+1)*segmentSize;
        var compactUntil=nextSegmentStart;
        var safeReadOnlyAddress=store.Log.SafeReadOnlyAddress;
        if(compactUntil>safeReadOnlyAddress){
            compactUntil=safeReadOnlyAddress;
        }
        if(compactUntil<=beginAddress){
            return;
        }
        var untilAddress=this.Store.Log.Compact<TKey,TValue,EqualityEmpty>(
            compactUntil,
            CompactionType.Lookup
        );
        store.Log.ShiftBeginAddress(untilAddress,true,true);
    }
}
