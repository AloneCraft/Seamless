using Common;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Seamless.Collections.Tests;
using Tsavorite.core;

namespace Seamless.Collections;
/// <summary>
/// Tsavorite ストアを管理する抽象基底クラス。
/// DictionaryStore および HashSetStore の共通実装を提供する。
/// </summary>
public abstract class CollectionStore<TKey, TValue>:CollectionStore, IDisposable where TKey : IEquatable<TKey> {
	private protected readonly KVSettings Settings;
	public readonly TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Store;
	public long LogMemorySize { get; }
	public double MutableFraction { get; }
	private bool Disposed;
	private readonly long ObjectLogSegmentSize;
	private CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,bool このクラスでDeviceをDisposeするべきか) {
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
		long ObjectLogSegmentSize;
		if(ObjectLogDevice.Capacity<0) {
			ObjectLogSegmentSize=Const.MinObjectLogCapacity;
		} else {
			Debug.Assert(ObjectLogDevice.Capacity%SegmentSize==0,$"ObjectLogDevice.Capacity must be a multiple of SegmentSize ({SegmentSize}), but was {ObjectLogDevice.Capacity}");
			ObjectLogSegmentSize=ObjectLogDevice.Capacity;
		}
		this.ObjectLogSegmentSize=ObjectLogSegmentSize;
		var Settings = new KVSettings {
			IndexSize=IndexSize,
			LogMemorySize=MemorySize,
			PageSize=PageSize,
			SegmentSize=SegmentSize,

			MutableFraction=MutableFraction,
			LogDevice=LogDevice,
			ObjectLogDevice=ObjectLogDevice,
			ObjectLogSegmentSize=ObjectLogSegmentSize,
		};
		//Const.MinObjectLogCapacity
		this.Settings=Settings;
		this.LogMemorySize=MemorySize;
		this.MutableFraction=MutableFraction;
		//MemoryOnlyDevice使用時はシリアライザ不要。ページデータはヒープ上で保持されるためインライン格納で十分。
		var StoreFunctions = new StoreFunctions<SpanByteComparer,DefaultRecordDisposer>(new SpanByteComparer(),NullSerializer,new DefaultRecordDisposer());
		this.Store=new TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>(
			Settings,
			StoreFunctions,
			static (AllocSettings,StoreFuncs) => {
				var a = new ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>(
					AllocSettings,
					StoreFuncs
				);
				return a;
			});
	}
	private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction) : this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),true) {
	}
	[ExcludeFromCodeCoverage]
	private static IObjectSerializer<IHeapObject>? NullSerializer() => null;
	private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice) : this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,false) {
	}
	public void CompactWithLookup() {
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

		if(CompactUntil<=BeginAddress) return;
		var untilAddress = Store.Log.Compact<TKey,TValue,Empty>(
			CompactUntil,
			CompactionType.Lookup
		);
		Store.Log.ShiftBeginAddress(untilAddress,true,true);
	}
	public (long BeginAddress,long HeadAddress,long ReadOnlyAddress,long TailAddress) Address {
		get {
			var Log = this.Store.Log;
			return (Log.BeginAddress,Log.HeadAddress,Log.ReadOnlyAddress,Log.TailAddress);
		}
	}
	~CollectionStore() {
		this.Dispose(false);
	}

	public void Dispose() {
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing) {
		if(this.Disposed) return;
		this.Disposed=true;
		if(disposing) {
			// マネージリソースの解放
			this.Store.Dispose();
			this.Settings.Dispose();
		} else {
		}
	}
}
