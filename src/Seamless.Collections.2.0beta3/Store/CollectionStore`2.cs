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
	//private static partial GStoreFunctions GetStoreFunctions(){
 //       return new GStoreFunctions(new SpanByteComparer(),NullSerializer,new DefaultRecordDisposer());
 //       [ExcludeFromCodeCoverage]
 //       static IObjectSerializer<IHeapObject>? NullSerializer() => null;
 //   }
	//private protected readonly KVSettings Settings;
	//public readonly TsavoriteKV<GStoreFunctions,GAllocator> Store;
	//public long LogMemorySize { get; }
	//public double MutableFraction { get; }
	//private bool Disposed;
	//private readonly long ObjectLogSegmentSize;
	//private CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir) {
	//	AssertIsPowerOfTwo(IndexSize,nameof(IndexSize));
	//	AssertIsPowerOfTwo(PageSize,nameof(PageSize));
	//	AssertIsPowerOfTwo(SegmentSize,nameof(SegmentSize));
	//	AssertIsPowerOfTwo(MemorySize,nameof(MemorySize));
	//	Debug.Assert(IndexSize>=64,$"{nameof(IndexSize)} must be >= 64");
	//	Debug.Assert(PageSize>=1L<<12,$"{nameof(PageSize)} must be >= 4096");
	//	Debug.Assert(PageSize<=1L<<30,$"{nameof(PageSize)} must be <= 1GB (1L<<30)");
	//	Debug.Assert(PageSize<=SegmentSize,"SegmentSize must be at least equal to PageSize.");
	//	Debug.Assert(MemorySize>=2*PageSize,$"{nameof(MemorySize)} must be >= 2*{nameof(PageSize)}");
	//	Debug.Assert(SegmentSize<=1L<<62,$"{nameof(SegmentSize)} must be <= 1L<<62");
	//	Debug.Assert(ObjectLogDevice.Capacity is >=Const.MinObjectLogCapacity and <=Const.MaxObjectLogCapacity or -1);
	//	long ObjectLogSegmentSize;
	//	if(ObjectLogDevice.Capacity<0) {
	//		ObjectLogSegmentSize=Const.MinObjectLogCapacity;
	//	} else {
	//		Debug.Assert(ObjectLogDevice.Capacity%SegmentSize==0,$"ObjectLogDevice.Capacity must be a multiple of SegmentSize ({SegmentSize}), but was {ObjectLogDevice.Capacity}");
	//		ObjectLogSegmentSize=ObjectLogDevice.Capacity;
	//	}
	//	this.ObjectLogSegmentSize=ObjectLogSegmentSize;
	//	var Settings = new KVSettings {
	//		IndexSize=IndexSize,
	//		LogMemorySize=MemorySize,
	//		PageSize=PageSize,
	//		SegmentSize=SegmentSize,

	//		MutableFraction=MutableFraction,
	//		LogDevice=LogDevice,
	//		ObjectLogDevice=ObjectLogDevice,
	//		ObjectLogSegmentSize=ObjectLogSegmentSize,
	//		CheckpointDir=CheckpointDir,
	//	};
	//	//Const.MinObjectLogCapacity
	//	this.Settings=Settings;
	//	this.LogMemorySize=MemorySize;
	//	this.MutableFraction=MutableFraction;
	//	//MemoryOnlyDevice使用時はシリアライザ不要。ページデータはヒープ上で保持されるためインライン格納で十分。
	//	var StoreFunctions = new GStoreFunctions(new SpanByteComparer(),NullSerializer,new DefaultRecordDisposer());
	//	this.Store=new TsavoriteKV<GStoreFunctions,GAllocator>(
	//		Settings,
	//		StoreFunctions,
	//		static (AllocSettings,StoreFuncs) => {
	//			var a = new GAllocator(
	//				AllocSettings,
	//				StoreFuncs
	//			);
	//			return a;
	//		});
	//}
	//private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir) : 
 //       this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),CheckpointDir) {
	//}
	//[ExcludeFromCodeCoverage]
	//private static IObjectSerializer<IHeapObject>? NullSerializer() => null;
	//private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,string? CheckpointDir,IDevice LogDevice,IDevice ObjectLogDevice) : 
 //       this(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir) {
	//}
 //   public void AssertCheckpointDir(){
 //       if(string.IsNullOrEmpty(this.Settings.CheckpointDir))
 //           throw new InvalidOperationException($"{nameof(this.Settings.CheckpointDir)} is not set.");
 //   }
 //   /// <summary>
	///// データベース全体の完全なバックアップ（Full Checkpoint）
	///// </summary>
	///// <param name="CheckpointType"></param>
	///// <param name="ct"></param>
	///// <returns></returns>
	///// <exception cref="InvalidOperationException"></exception>
	//public async ValueTask<Guid> SaveAsync(CheckpointType CheckpointType=CheckpointType.Snapshot,CancellationToken ct=default){
 //       this.AssertCheckpointDir();
 //       var(success,token)=await this.Store.TakeFullCheckpointAsync(CheckpointType,ct);
 //       await this.Store.CompleteCheckpointAsync(ct);
 //       if(success) 
 //           return token;
 //       throw new InvalidOperationException("Checkpoint failed.");
 //   }
	///// <summary>
	///// データベース全体の完全なバックアップ（Full Checkpoint）
	///// </summary>
	///// <param name="CheckpointType"></param>
	///// <returns></returns>
 //   public Guid Save(CheckpointType CheckpointType=CheckpointType.Snapshot)=>this.SaveAsync(CheckpointType).GetAwaiter().GetResult();
 //   /// <summary>
 //   /// データ（ログ）部分のみのバックアップ（Incremental/Log Checkpoint）
 //   /// インデックスは復元せず、ログを先頭から再生してインデックスを再構築するリカバリー。
 //   /// インデックスチェックポイントが存在しない場合や、インデックスチェックポイントが破損している場合の復旧に使用する。
 //   /// </summary>
 //   /// <param name="CheckpointType"></param>
 //   /// <param name="ct"></param>
 //   /// <returns></returns>
 //   public async ValueTask<Guid> SaveLogOnlyAsync(CheckpointType CheckpointType=CheckpointType.Snapshot,CancellationToken ct=default){
 //       this.AssertCheckpointDir();
	//	var(success,token)=await this.Store.TakeHybridLogCheckpointAsync(CheckpointType,cancellationToken:ct);
	//	await this.Store.CompleteCheckpointAsync(ct);
 //       if(success) 
 //           return token;
	//	throw new InvalidOperationException($"{nameof(this.SaveLogOnly)}Checkpoint failed.");
	//}
 //   /// <summary>
 //   /// データ（ログ）部分のみのバックアップ（Incremental/Log Checkpoint）
 //   /// インデックスは復元せず、ログを先頭から再生してインデックスを再構築するリカバリー。
 //   /// インデックスチェックポイントが存在しない場合や、インデックスチェックポイントが破損している場合の復旧に使用する。
 //   /// </summary>
 //   /// <param name="CheckpointType"></param>
 //   /// <returns></returns>
 //   public Guid SaveLogOnly(CheckpointType CheckpointType=CheckpointType.Snapshot)=>this.SaveLogOnlyAsync(CheckpointType).GetAwaiter().GetResult();
 //   /// <summary>
 //   /// 最新のデータに復元
 //   /// </summary>
 //   /// <param name="ct"></param>
 //   /// <returns></returns>
 //   /// <exception cref="InvalidOperationException"></exception>
 //   public async ValueTask LoadAsync(CancellationToken ct=default){
 //       this.AssertCheckpointDir();
	//	await this.Store.RecoverAsync(cancellationToken:ct);
	//}
 //   /// <summary>
 //   /// 最新のデータに復元
 //   /// </summary>
 //   public void Load()=>this.LoadAsync().GetAwaiter().GetResult();
 //   /// <summary>
 //   /// 指定した過去地点(token)へのピンポイント復元
 //   /// </summary>
 //   /// <param name="token"></param>
 //   /// <param name="ct"></param>
 //   /// <returns></returns>
 //   public async ValueTask LoadAsync(Guid token,CancellationToken ct=default){
 //       this.AssertCheckpointDir();
 //       await this.Store.RecoverAsync(Guid.Empty,token,cancellationToken:ct);
 //   }
 //   /// <summary>
 //   /// 指定した過去地点(token)へのピンポイント復元
 //   /// </summary>
 //   /// <param name="token"></param>
 //   public void Load(Guid token){
 //       var ct=CancellationToken.None;
 //       this.LoadAsync(token,ct).GetAwaiter().GetResult();
 //   }
 private partial long Compact(long CompactUntil){
		var UntilAddress = this.Store.Log.Compact<TKey, TValue, Empty>(
         CompactUntil,
         CompactionType.Lookup
     );
     return UntilAddress;
 }
	//public void CompactWithLookup() {
	//	var Store = this.Store;
	//	var BeginAddress = Store.Log.BeginAddress;
	//	var SegmentSize = this.Settings.SegmentSize;
	//	var NextSegmentStart = (BeginAddress/SegmentSize+1)*SegmentSize;
	//	var CompactUntil = NextSegmentStart;
	//	var SafeReadOnlyAddress = Store.Log.SafeReadOnlyAddress;
	//	if(CompactUntil>SafeReadOnlyAddress) {
	//		CompactUntil=SafeReadOnlyAddress;
	//	} else {
	//	}

	//	if(CompactUntil<=BeginAddress) return;
	//	var untilAddress = Store.Log.Compact<TKey,TValue,Empty>(
	//		CompactUntil,
	//		CompactionType.Lookup
	//	);
	//	Store.Log.ShiftBeginAddress(untilAddress,true,true);
	//}
	//public (long BeginAddress,long HeadAddress,long ReadOnlyAddress,long TailAddress) Address {
	//	get {
	//		var Log = this.Store.Log;
	//		return (Log.BeginAddress,Log.HeadAddress,Log.ReadOnlyAddress,Log.TailAddress);
	//	}
	//}
	//public void Dispose() {
	//	this.Dispose(true);
	//	GC.SuppressFinalize(this);
	//}
	//protected virtual void Dispose(bool disposing) {
	//	if(this.Disposed) return;
	//	this.Disposed=true;
	//	if(disposing) {
	//		// マネージリソースの解放
	//		this.Store.Dispose();
	//		this.Settings.Dispose();
	//	} else {
	//	}
	//}
 //   ~CollectionStore()=>this.Dispose(false);
}
