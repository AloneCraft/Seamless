using Common;

using Seamless.Devices;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Tsavorite.core;

namespace Seamless.Collections;
/// <summary>
/// Tsavorite ストアを管理する抽象基底クラス。
/// DictionaryStore および HashSetStore の共通実装を提供する。
/// </summary>
public abstract partial class CollectionStore<TKey, TValue>:CollectionStore, IDisposable where TKey : IEquatable<TKey> {
    //private protected readonly GKVSettings Settings;
#if TSAVORITE2_0BETA3
    private protected KVSettings Settings{get;private set;}
	public readonly TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Store;
    private void Initialize(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir){
    }
#elif TSAVORITE1_1_1
    private protected KVSettings<SpanByte,SpanByte> Settings{get;private set;}
    public readonly TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store;
#endif
	public long LogMemorySize { get; }
	public double MutableFraction { get; }
	private bool Disposed;

    private CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) {
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
#if TSAVORITE2_0BETA3
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
        var StoreFunctions =new StoreFunctions<SpanByteComparer,DefaultRecordDisposer>(new SpanByteComparer(),NullSerializer,new DefaultRecordDisposer());
		this.Store=new TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>(
			this.Settings,
			StoreFunctions,
			static (AllocSettings,StoreFuncs) =>new SpanByteAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>(
                AllocSettings,
                StoreFuncs
            )
        );
        [ExcludeFromCodeCoverage]
        static IObjectSerializer<IHeapObject>? NullSerializer() => null;
#elif TSAVORITE1_1_1
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
        var StoreFunctions = new StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>(
            new SpanByteComparer(),
            NullSerializer,
            NullSerializer,
            new SpanByteRecordDisposer()
        );
        this.Store=new TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>(
            this.Settings,
            StoreFunctions,
            static (AllocSettings,StoreFuncs) =>new SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>(
                AllocSettings,
                StoreFuncs
            ));
        [ExcludeFromCodeCoverage]
		static IObjectSerializer<SpanByte>? NullSerializer() => null;
#endif
	}
	private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir) : 
        this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),CheckpointDir) {
	}
	//[ExcludeFromCodeCoverage]
	//private static IObjectSerializer<IHeapObject>? NullSerializer() => null;
	private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir,IDevice LogDevice,IDevice ObjectLogDevice) : 
        this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir) {
	}
    public void AssertCheckpointDir(){
        if(string.IsNullOrEmpty(this.Settings.CheckpointDir))
            throw new InvalidOperationException($"{nameof(this.Settings.CheckpointDir)} is not set.");
    }
    /// <summary>
	/// データベース全体の完全なバックアップ（Full Checkpoint）
	/// </summary>
	/// <param name="CheckpointType"></param>
	/// <param name="ct"></param>
	/// <returns></returns>
	/// <exception cref="InvalidOperationException"></exception>
	public async ValueTask<Guid> SaveAsync(CheckpointType CheckpointType=CheckpointType.Snapshot,CancellationToken ct=default){
        this.AssertCheckpointDir();
        var(success,token)=await this.Store.TakeFullCheckpointAsync(CheckpointType,ct);
        await this.Store.CompleteCheckpointAsync(ct);
        if(success) 
            return token;
        throw new InvalidOperationException("Checkpoint failed.");
    }
	/// <summary>
	/// データベース全体の完全なバックアップ（Full Checkpoint）
	/// </summary>
	/// <param name="CheckpointType"></param>
	/// <returns></returns>
    public Guid Save(CheckpointType CheckpointType=CheckpointType.Snapshot)=>this.SaveAsync(CheckpointType).GetAwaiter().GetResult();
    /// <summary>
    /// データ（ログ）部分のみのバックアップ（Incremental/Log Checkpoint）
    /// インデックスは復元せず、ログを先頭から再生してインデックスを再構築するリカバリー。
    /// インデックスチェックポイントが存在しない場合や、インデックスチェックポイントが破損している場合の復旧に使用する。
    /// </summary>
    /// <param name="CheckpointType"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async ValueTask<Guid> SaveLogOnlyAsync(CheckpointType CheckpointType=CheckpointType.Snapshot,CancellationToken ct=default){
        this.AssertCheckpointDir();
		var(success,token)=await this.Store.TakeHybridLogCheckpointAsync(CheckpointType,cancellationToken:ct);
		await this.Store.CompleteCheckpointAsync(ct);
        if(success) 
            return token;
		throw new InvalidOperationException($"{nameof(this.SaveLogOnly)}Checkpoint failed.");
	}
    /// <summary>
    /// データ（ログ）部分のみのバックアップ（Incremental/Log Checkpoint）
    /// インデックスは復元せず、ログを先頭から再生してインデックスを再構築するリカバリー。
    /// インデックスチェックポイントが存在しない場合や、インデックスチェックポイントが破損している場合の復旧に使用する。
    /// </summary>
    /// <param name="CheckpointType"></param>
    /// <returns></returns>
    public Guid SaveLogOnly(CheckpointType CheckpointType=CheckpointType.Snapshot)=>this.SaveLogOnlyAsync(CheckpointType).GetAwaiter().GetResult();
    /// <summary>
    /// 最新のデータに復元
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async ValueTask LoadAsync(CancellationToken ct=default){
        this.AssertCheckpointDir();
		await this.Store.RecoverAsync(cancellationToken:ct);
	}
    /// <summary>
    /// 最新のデータに復元
    /// </summary>
    public void Load()=>this.LoadAsync().GetAwaiter().GetResult();
    /// <summary>
    /// 指定した過去地点(token)へのピンポイント復元
    /// </summary>
    /// <param name="token"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async ValueTask LoadAsync(Guid token,CancellationToken ct=default){
        this.AssertCheckpointDir();
        await this.Store.RecoverAsync(Guid.Empty,token,cancellationToken:ct);
    }
    /// <summary>
    /// 指定した過去地点(token)へのピンポイント復元
    /// </summary>
    /// <param name="token"></param>
    public void Load(Guid token){
        var ct=CancellationToken.None;
        this.LoadAsync(token,ct).GetAwaiter().GetResult();
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
#if TSAVORITE2_0BETA3
    var UntilAddress=this.Store.Log.Compact<TKey,TValue,Empty>(
        CompactUntil,
        CompactionType.Lookup
    );
#elif TSAVORITE1_1_1
        var UntilAddress = this.Store.Log.Compact<SpanByte,SpanByteAndMemory,Empty,SpanByteFunctions<Empty>>(
            new(),
            CompactUntil,
            CompactionType.Lookup
        );
#endif
		Store.Log.ShiftBeginAddress(UntilAddress,true,true);
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
