
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
#elif TSAVORITE1_1_1
    private protected KVSettings<SpanByte,SpanByte> Settings{get;private set;}
    public readonly TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store;
#endif
	public long LogMemorySize { get; }
	public double MutableFraction { get; }
	private bool Disposed;
    /// <summary>テスト専用シーム: TakeCheckpointAsync 完了後・CompleteCheckpointAsync 前に呼び出されるコールバック。</summary>
    private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) {
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
	private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction): 
        this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null){
	}
	//[ExcludeFromCodeCoverage]
	//private static IObjectSerializer<IHeapObject>? NullSerializer() => null;
	//private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) : 
 //       this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir) {
	//}
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
        // Task.Yield: ここで必ず一度スレッドを手放し、継続をスレッドプールへ投稿する。
        // TakeFullCheckpointAsync がデータ量少・高速 NVMe 等で同期完了した場合でも
        // この yield によって呼び出し元へ制御が戻り、_checkpointLock=1 の状態が外部から観測可能になる。
        // これにより「ロック保持中に 2 回目の SaveAsync を呼ぶ」テストが確実に機能する。
        await Task.Yield();
        // await: TakeFullCheckpointAsync / CompleteCheckpointAsync の I/O 完了を非同期で待機する。
        // スレッドをブロックせず、完了コールバックで後続処理を再開する。
        var(success,token)=await this.Store.TakeFullCheckpointAsync(CheckpointType,ct);
        await this.Store.CompleteCheckpointAsync(ct);
        if(success) 
            return token;
        throw new InvalidOperationException($"{nameof(this.Save)}Checkpoint failed.");
		//// Interlocked.CompareExchange: _checkpointLock が 0(未使用)のときだけ 1(使用中)に書き換える。
		//// 読み取りと書き込みを不可分(アトミック)に行うため、複数スレッドが同時に入ってもロックは必ず1つだけ獲得できる。
		//// 戻り値は交換前の値なので、0 以外 ＝ すでに他がロック中 → 例外を投げて即リターン。
		//if(Interlocked.CompareExchange(ref this.CheckpointLock,1,0)!=0)
  //          throw new InvalidOperationException($"{nameof(this.Save)}Checkpoint failed.");
		//// try-finally: await 中に例外が発生した場合や OperationCanceledException が伝播した場合でも
		//// finally ブロックは必ず実行されるため、ロックの解放漏れを防ぐ。
		//try{
		//	// Task.Yield: ここで必ず一度スレッドを手放し、継続をスレッドプールへ投稿する。
		//	// TakeFullCheckpointAsync がデータ量少・高速 NVMe 等で同期完了した場合でも
		//	// この yield によって呼び出し元へ制御が戻り、_checkpointLock=1 の状態が外部から観測可能になる。
		//	// これにより「ロック保持中に 2 回目の SaveAsync を呼ぶ」テストが確実に機能する。
		//	await Task.Yield();
		//	// await: TakeFullCheckpointAsync / CompleteCheckpointAsync の I/O 完了を非同期で待機する。
		//	// スレッドをブロックせず、完了コールバックで後続処理を再開する。
		//	var(success,token)=await this.Store.TakeFullCheckpointAsync(CheckpointType,ct);
		//		await this.Store.CompleteCheckpointAsync(ct);
		//	if(success) 
		//		return token;
  //          throw new InvalidOperationException($"{nameof(this.Save)}Checkpoint failed.");
		//}finally{
		//	// Interlocked.Exchange: _checkpointLock を無条件に 0(未使用)へ戻す。
		//	// CompareExchange と同様にアトミックなので、ロック解放がスレッド間で確実に可視になる。
		//	Interlocked.Exchange(ref this.CheckpointLock,0);
		//}
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
		// SaveAsync と同じロック戦略: CompareExchange でアトミックに使用中フラグを立て、
		// 既に立っていれば即例外。try-finally でロック解放を保証する。
        // SaveAsync と同じ理由で Task.Yield により必ずスレッドを手放す。
        await Task.Yield();
        // await: TakeHybridLogCheckpointAsync / CompleteCheckpointAsync の I/O を非同期で待機する。
        var(success,token)=await this.Store.TakeHybridLogCheckpointAsync(CheckpointType,cancellationToken:ct);
        await this.Store.CompleteCheckpointAsync(ct);
        if(success) 
            return token;
        throw new InvalidOperationException($"{nameof(this.SaveLogOnly)}Checkpoint failed.");
		//if(Interlocked.CompareExchange(ref this.CheckpointLock,1,0)!=0)
  //          throw new InvalidOperationException($"{nameof(this.SaveLogOnly)}Checkpoint failed.");
		//try{
		//	// SaveAsync と同じ理由で Task.Yield により必ずスレッドを手放す。
		//	await Task.Yield();
		//	// await: TakeHybridLogCheckpointAsync / CompleteCheckpointAsync の I/O を非同期で待機する。
		//		var(success,token)=await this.Store.TakeHybridLogCheckpointAsync(CheckpointType,cancellationToken:ct);
		//		await this.Store.CompleteCheckpointAsync(ct);
		//	if(success) 
		//		return token;
  //          throw new InvalidOperationException($"{nameof(this.SaveLogOnly)}Checkpoint failed.");
		//}finally{
		//	Interlocked.Exchange(ref this.CheckpointLock,0);
		//}
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

		if(CompactUntil<=BeginAddress) 
            return;
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
