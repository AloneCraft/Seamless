using Common;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Seamless.Devices;
using Tsavorite.core;

namespace Seamless.Collections;
/// <summary>
/// Tsavorite ストアを管理する抽象基底クラス。
/// DictionaryStore および HashSetStore の共通実装を提供する。
/// </summary>
public abstract class CollectionStore<TKey, TValue>:CollectionStore, IDisposable where TKey : IEquatable<TKey> {
	private protected readonly KVSettings Settings;
	public readonly TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Store;
	public long LogMemorySize { get; }
	public double MutableFraction { get; }
	private bool Disposed;
	private readonly long ObjectLogSegmentSize;
	private CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,bool このクラスでDeviceをDisposeするべきか,string? CheckpointDir=null) {
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
			CheckpointDir=CheckpointDir,
		};
		//Const.MinObjectLogCapacity
		this.Settings=Settings;
		this.LogMemorySize=MemorySize;
		this.MutableFraction=MutableFraction;
		//MemoryOnlyDevice使用時はシリアライザ不要。ページデータはヒープ上で保持されるためインライン格納で十分。
		var StoreFunctions = new StoreFunctions<SpanByteComparer,DefaultRecordDisposer>(new SpanByteComparer(),NullSerializer,new DefaultRecordDisposer());
		this.Store=new TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>(
			Settings,
			StoreFunctions,
			static (AllocSettings,StoreFuncs) => {
				var a = new SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>(
					AllocSettings,
					StoreFuncs
				);
				return a;
			});
	}
	private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir,bool recoverDevice) : 
        this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),true,CheckpointDir) {
	}
	[ExcludeFromCodeCoverage]
	private static IObjectSerializer<IHeapObject>? NullSerializer() => null;
	private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir,bool recoverDevice,IDevice LogDevice,IDevice ObjectLogDevice) : 
        this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,false,CheckpointDir) {
	}
	public async ValueTask SaveAsync(CancellationToken ct=default){
		if(string.IsNullOrEmpty(this.Settings.CheckpointDir))
			throw new InvalidOperationException($"{nameof(KVSettings.CheckpointDir)} is not set.");
        var (success,_)=await this.Store.TakeFullCheckpointAsync(CheckpointType.Snapshot,ct);
		if(!success) throw new InvalidOperationException("Checkpoint failed.");
       await this.Store.CompleteCheckpointAsync(ct);
	}
    public Guid Save(){
        if(string.IsNullOrEmpty(this.Settings.CheckpointDir))
            throw new InvalidOperationException($"{nameof(KVSettings.CheckpointDir)} is not set.");
        var(success,token)=this.Store.TakeFullCheckpointAsync(CheckpointType.Snapshot).AsTask().GetAwaiter().GetResult();
        if(!success) throw new InvalidOperationException("Checkpoint failed.");
        return token;
    }
	public async ValueTask LoadAsync(CancellationToken ct=default){
		if(string.IsNullOrEmpty(this.Settings.CheckpointDir))
			throw new InvalidOperationException($"{nameof(KVSettings.CheckpointDir)} is not set.");
		// 1st call: IndexCheckpointInfo.startLogicalAddress > beginAddress → RecoverFuzzyIndexAsync called (sets recoveryCountdown), TailAddress → finalLogicalAddress
		// 2nd call: TailAddress > firstValidAddress → internal Reset() + recoveryCountdown != null → recoveredICInfo = default → recoverFromAddress = beginAddress → ProcessReadSnapshotPage called for all pages
		await this.Store.RecoverAsync(numPagesToPreload: -1,undoNextVersion: false,recoverTo: -1,cancellationToken: ct);
		await this.Store.RecoverAsync(numPagesToPreload: -1,undoNextVersion: false,recoverTo: -1,cancellationToken: ct);
	}
    public void Load(Guid token){
        if(string.IsNullOrEmpty(this.Settings.CheckpointDir))
            throw new InvalidOperationException($"{nameof(KVSettings.CheckpointDir)} is not set.");
        long xx=this.Store.RecoverAsync(fullCheckpointToken:token).AsTask().GetAwaiter().GetResult();
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
    ~CollectionStore()=>this.Dispose(false);
}
