using Common;

using Seamless.Devices;

using System.Diagnostics;
using System.Runtime;

using Tsavorite.core;

namespace Seamless.Collections;
public abstract class CollectionStore<TKey, TValue>:CollectionStore, IDisposable where TKey : IEquatable<TKey> {
	private protected readonly KVSettings<SpanByte,SpanByte> Settings;
	public readonly TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store;
	public long LogMemorySize { get; }
	public double MutableFraction { get; }
	private readonly long SegmentSize;

    /// <param name="IndexSize"></param>
    /// <param name="PageSize"></param>
    /// <param name="SegmentSize"></param>
    /// <param name="MemorySize"></param>
    /// <param name="MutableFraction"></param>
    private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir,bool recoverDevice):
        this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir,recoverDevice,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity)){

    }
    //private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir=null) 
 //       : this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),CheckpointDir) {}
	private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir,bool recoverDevice,IDevice LogDevice,IDevice ObjectLogDevice) {
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
		var Settings = new KVSettings<SpanByte,SpanByte> {
			IndexSize=IndexSize,
			MemorySize=MemorySize,
			PageSize=PageSize,
			SegmentSize=SegmentSize,
			MutableFraction=MutableFraction,
			LogDevice=LogDevice,
			ObjectLogDevice=ObjectLogDevice,
			CheckpointDir=CheckpointDir,
		};
		this.Settings=Settings;
		this.SegmentSize=SegmentSize;
		this.LogMemorySize=MemorySize;
		this.MutableFraction=MutableFraction;
		var StoreFunctions = new StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>(
			new SpanByteComparer(),
			() => null,
			() => null,
			new SpanByteRecordDisposer()
		);
		this.Store=new TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>(
			Settings,
			StoreFunctions,
			static (AllocSettings,StoreFuncs) => new SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>(AllocSettings,StoreFuncs));
	}
    public async ValueTask<(bool success,Guid token)> SaveAsync(CheckpointType CheckpointType=CheckpointType.Snapshot,CancellationToken ct=default){
        if(string.IsNullOrEmpty(this.Settings.CheckpointDir))
            throw new InvalidOperationException($"{nameof(this.Settings.CheckpointDir)} is not set.");
        var(success,token)=await this.Store.TakeFullCheckpointAsync(CheckpointType,ct);
        await this.Store.CompleteCheckpointAsync(ct);
        if(!success) throw new InvalidOperationException("Checkpoint failed.");
        return(success,token);
    }
    public Guid Save(CheckpointType CheckpointType=CheckpointType.Snapshot){
        var(success,token)=this.SaveAsync(CheckpointType).GetAwaiter().GetResult();
        return token;
    }
    public async ValueTask LoadAsync(Guid token,CheckpointType CheckpointType=CheckpointType.Snapshot,CancellationToken ct=default){
        if(string.IsNullOrEmpty(this.Settings.CheckpointDir))
            throw new InvalidOperationException($"{nameof(this.Settings.CheckpointDir)} is not set.");
        await this.Store.RecoverAsync(Guid.Empty,token,cancellationToken:ct);
    }
    public void Load(Guid token,CheckpointType CheckpointType=CheckpointType.Snapshot){
        var ct=CancellationToken.None;
        this.LoadAsync(token,CheckpointType,ct).GetAwaiter().GetResult();
    }
	public void CompactWithLookup() {
		var Store = this.Store;
		// 1. 【境界の取得】
		var BeginAddress = Store.Log.BeginAddress;
		var SafeReadOnlyAddress = Store.Log.SafeReadOnlyAddress;
		// 2. 【compactUntil の算出】
		// 「今の開始地点から 1 セグメント分先」を目標にするのが最も効率的です。
		var SegmentSize = this.SegmentSize;
		var CompactUntil = BeginAddress+SegmentSize;

		// 安全ガード：HDD への書き出しが完了していない領域は掃除できません
		if(CompactUntil>SafeReadOnlyAddress) {
			CompactUntil=SafeReadOnlyAddress;
		}
		if(CompactUntil<=BeginAddress) return;
		var UntilAddress = Store.Log.Compact<SpanByte,SpanByteAndMemory,Empty,SpanByteFunctions<Empty>>(
			new SpanByteFunctions<Empty>(),
			CompactUntil,
			CompactionType.Lookup
		);
		Store.Log.ShiftBeginAddress(UntilAddress,true,true);
	}

	public (long BeginAddress,long HeadAddress,long ReadOnlyAddress,long TailAddress) Address {
		get {
			var Log = this.Store.Log;
			return (Log.BeginAddress,Log.HeadAddress,Log.ReadOnlyAddress,Log.TailAddress);
		}
	}
	~CollectionStore() => this.Dispose(false);
	public void Dispose() {
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}
	private bool Disposed;
	protected virtual void Dispose(bool disposing) {
		if(this.Disposed) return;
		this.Disposed=true;
		if(disposing) {
			// マネージリソースの解放
			this.Store.Dispose();
			this.Settings.Dispose();
		}
		// アンマネージリソースの解放があればここに
	}
}
