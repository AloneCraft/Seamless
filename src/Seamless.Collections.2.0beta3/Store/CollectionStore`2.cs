using Seamless.Devices;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Seamless.Collections;

public abstract partial class CollectionStore<TKey,TValue,TSessionKey,TSessionValue,TStoreFunctions,TAllocator>
	where TKey : IEquatable<TKey>
	where TStoreFunctions : IStoreFunctions
	where TAllocator : IAllocator<TStoreFunctions>
{
    private protected KVSettings Settings{get;private set;}
	public readonly TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Store;
    /// <summary>テスト専用シーム: TakeCheckpointAsync 完了後・CompleteCheckpointAsync 前に呼び出されるコールバック。</summary>
    private protected CollectionStore(
        TStoreFunctions StoreFunctions,
        Func<AllocatorSettings, TStoreFunctions, TAllocator> allocatorFactory,
        long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) {
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
		Debug.Assert(ObjectLogDevice.Capacity is >=Const.MinObjectLogCapacity and <=Const.MaxObjectLogCapacity or -1);
		this.LogMemorySize=MemorySize;
		this.MutableFraction=MutableFraction;
		//MemoryOnlyDevice使用時はシリアライザ不要。ページデータはヒープ上で保持されるためインライン格納で十分。
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
        var storeFunctions =new StoreFunctions<SpanByteComparer,DefaultRecordDisposer>(new SpanByteComparer(),NullSerializer,new DefaultRecordDisposer());
		this.Store=new TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>(
			this.Settings,
			storeFunctions,
			static (AllocSettings,StoreFuncs) =>new SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>(
                AllocSettings,
                StoreFuncs
            )
        );
        [ExcludeFromCodeCoverage]
        static IObjectSerializer<IHeapObject>? NullSerializer() => null;
	}
	public virtual void CompactWithLookup() {
		var Store = this.Store;
		var BeginAddress = Store.Log.BeginAddress;
		var SegmentSize = this.Settings.SegmentSize;
		var NextSegmentStart = (BeginAddress/SegmentSize+1)*SegmentSize;
		var CompactUntil = NextSegmentStart;
		var SafeReadOnlyAddress = Store.Log.SafeReadOnlyAddress;
		if(CompactUntil>SafeReadOnlyAddress) {
			CompactUntil=SafeReadOnlyAddress;
		} else {
		}

		if(CompactUntil<=BeginAddress) 
			return;
		var UntilAddress=this.Store.Log.Compact<TKey,TValue,Empty>(
			CompactUntil,
			CompactionType.Lookup
		);
		Store.Log.ShiftBeginAddress(UntilAddress,true,true);
	}
}
