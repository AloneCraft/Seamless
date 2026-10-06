using Seamless.Collections.Tsavorite;
namespace Seamless.Collections.Tsavorite;

public abstract class CollectionStore {
    /// <summary>CollectionStoreの境界条件を検証する。</summary>
    [Conditional("DEBUG")]
    private protected static void AssertIsPowerOfTwo(long value,string paramName) {
        Debug.Assert(value>0,$"{paramName} must be positive");
        Debug.Assert((value&(value-1))==0,$"{paramName} must be a power of 2, but was {value}");
    }

    private protected static void ValidatePowerOfTwo(long value,string paramName) {
        if(value<=0)
            throw new ArgumentOutOfRangeException(paramName,$"{paramName} must be positive.");
        if((value&(value-1))!=0)
            throw new ArgumentOutOfRangeException(paramName,$"{paramName} must be a power of 2, but was {value}.");
    }

    private protected abstract long GetRecoveryEntryCount();

#if DEBUG
    // シングル用の共有の読み取りセッションを、別のスレッドが同時に使っていないかを確かめる(Debug ビルドだけ)。同じスレッドの入れ子は許す。
    private int scanOwnerThreadId;
    private int scanDepth;
    internal void EnterScan() {
        var me = Environment.CurrentManagedThreadId;
        var owner = Interlocked.CompareExchange(ref this.scanOwnerThreadId,me,0);
        if(owner!=0&&owner!=me) {
            throw new InvalidOperationException(
                $"The collection created by Create() is being scanned by thread {owner} while thread {me} started another scan. "+
                "A collection created by Create() must be used from one thread at a time; use BeginParallel().Create() for each thread. "+
                "Create() で作ったコレクションを複数のスレッドから同時に走査しています。スレッドごとに BeginParallel().Create() で作ってください(BUG-0016)。");
        }
        this.scanDepth++;
    }
    internal void ExitScan() {
        if(--this.scanDepth==0) {
            Volatile.Write(ref this.scanOwnerThreadId,0);
        }
    }
#endif

    /// <summary>並列区間を仕上げる。区間の最後の操作用オブジェクトの破棄か、区間の破棄から 1 回だけ呼ばれる。</summary>
    internal abstract void FinishParallelRegion(ParallelRegionState region,bool fromFinalizer);
}



public abstract partial class CollectionStore<TKey, TValue>
   :CollectionStore<
        TKey,
        TValue,
        SpanByteStoreFunctions,
        SpanByteStoreAllocator
    > {
}



public abstract partial class CollectionStore<TKey, TValue> {
    private protected CollectionStore(long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes,long? ObjectLogSegmentSize = null) :
        base(
            new SpanByteStoreFunctions(new SpanByteComparer(),static () => null,DefaultRecordTriggers.Instance),
            static (allocSettings,storeFuncs) => new SpanByteStoreAllocator(allocSettings,storeFuncs),
            IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,OverflowMutableStrategyKind,StorageLimitFraction,StorageReserveBytes,ObjectLogSegmentSize
        ) {
    }

    /// <summary>共通ストアから決めた配置で作る。デバイスと予算の予約はこのデータ本体が所有する。</summary>
    private protected CollectionStore(TsavoriteLayout layout) :
        this(layout.IndexSize,layout.PageSize,layout.SegmentSize,layout.LogMemorySize,layout.MutableFraction,layout.LogDevice,layout.ObjectLogDevice,layout.CheckpointDirectory,ResolveOverflowKind(layout)) {
        this.AttachLayout(layout);
    }

    private static OverflowMutableStrategyKind ResolveOverflowKind(TsavoriteLayout layout) {
        if(layout.Kind==global::Seamless.Execution.StoreKind.Memory) {
            return OverflowMutableStrategyKind.Memory;
        } else {
            return OverflowMutableStrategyKind.Storage;
        }
    }



















}




/// <summary>
/// CollectionStoreの境界条件を検証する。
/// CollectionStoreの境界条件を検証する。
/// </summary>
public abstract partial class CollectionStore<
    TKey,
    TValue,
    TStoreFunctions,
    TAllocator
>
    :CollectionStore, IDisposable, global::Seamless.Execution.IStoreComponent where TStoreFunctions : IStoreFunctions
    where TAllocator : IAllocator<TStoreFunctions> {
    private protected KVSettings Settings { get; private set; }
    public readonly TsavoriteKV<TStoreFunctions,TAllocator> Store;
    // 共通ストアから作ったときだけ設定される。旧 API で作ったときは null で、予算の確認はしない。
    private TsavoriteLayout? layout;
    // 予算を予約済みのログ末尾アドレス。旧 API では long.MaxValue にして確認を 1 回の比較で済ませる。
    private long reservedTailAddress = long.MaxValue;
    // ディスク上のログの先頭(ここより前のセグメントは物理的に消えている)。切り捨ては保存が確定するまで保留するので、論理的な先頭(Log.BeginAddress)より小さい。
    // ディスクの予約はこの位置から数える(詰め直しで先頭を進めても、保存が確定するまでファイルは残っているため)。
    private long diskBaseAddress;
    // ログの先頭を進めた(詰め直し・全削除)ので、前の保存へは戻れなくなる。次の保存で、前の保存を記録から外させる。
    private bool olderSavesInvalid;
    private long invalidationSerial;
    // 保存した(チェックポイントを取った)が、マニフェストがまだ確定していない状態。確定の通知(SaveCommitted)で後始末をする。
    private SaveCommit? pendingCommit;
    private sealed record SaveCommit(long BeginAddress,bool InvalidatesOlder,long Serial,int Generation);
    // 全削除で別の世代へ切り替えたあと、保存が確定するまで残っている古い世代のファイルの大きさ(ディスクの予約に含める)。
    private long retiredDiskBytes;
    private int accessorCount;
    public long LogMemorySize { get; }
    public double MutableFraction { get; }
    internal OverflowMutableStrategyKind OverflowMutableStrategyKind { get; }
    internal StorageOverflowMutableStrategy StorageOverflowMutableStrategy { get; }
    private bool Disposed;
    // Tsavorite は自前で渡された管理を破棄しないので、Dispose で破棄する。CheckpointDir が無いときは null。
    private readonly DeviceLogCommitCheckpointManager? checkpointManager;
    // 保存(Save)したが、まだ Flush していないチェックポイントの番号。
    private readonly global::System.Collections.Generic.HashSet<Guid> unflushedTokens = [];
    private int CheckpointInProgress;
    private bool CheckpointFailed;
    private bool UsesStorageDevice => this.Settings is not null&&(this.Settings.LogDevice is not MemoryOnlyDevice||this.Settings.ObjectLogDevice is not MemoryOnlyDevice);

    /// <summary>CollectionStoreの境界条件を検証する。</summary>
    private protected CollectionStore(
        TStoreFunctions StoreFunctions,
        Func<AllocatorSettings,TStoreFunctions,TAllocator> allocatorFactory,
        long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction,IDevice LogDevice,IDevice ObjectLogDevice,string? CheckpointDir,OverflowMutableStrategyKind OverflowMutableStrategyKind,double StorageLimitFraction = StorageOverflowMutableStrategy.DefaultLimitFraction,long StorageReserveBytes = StorageOverflowMutableStrategy.DefaultReserveBytes,long? ObjectLogSegmentSize = null) {
        ValidatePowerOfTwo(IndexSize,nameof(IndexSize));
        ValidatePowerOfTwo(PageSize,nameof(PageSize));
        ValidatePowerOfTwo(SegmentSize,nameof(SegmentSize));
        ValidatePowerOfTwo(MemorySize,nameof(MemorySize));
        if(ObjectLogSegmentSize is { } explicitObjectLogSegmentSize) {
            ValidatePowerOfTwo(explicitObjectLogSegmentSize,nameof(ObjectLogSegmentSize));
            AssertIsPowerOfTwo(explicitObjectLogSegmentSize,nameof(ObjectLogSegmentSize));
        }
        AssertIsPowerOfTwo(IndexSize,nameof(IndexSize));
        AssertIsPowerOfTwo(PageSize,nameof(PageSize));
        AssertIsPowerOfTwo(SegmentSize,nameof(SegmentSize));
        AssertIsPowerOfTwo(MemorySize,nameof(MemorySize));
        if(IndexSize<64)
            throw new ArgumentOutOfRangeException(nameof(IndexSize),$"{nameof(IndexSize)} must be >= 64.");
        if(PageSize<1L<<12)
            throw new ArgumentOutOfRangeException(nameof(PageSize),$"{nameof(PageSize)} must be >= 4096.");
        if(PageSize>1L<<30)
            throw new ArgumentOutOfRangeException(nameof(PageSize),$"{nameof(PageSize)} must be <= 1GB (1L<<30).");
        if(PageSize>SegmentSize)
            throw new ArgumentOutOfRangeException(nameof(SegmentSize),"SegmentSize must be at least equal to PageSize.");
        if(MemorySize<2*PageSize)
            throw new ArgumentOutOfRangeException(nameof(MemorySize),$"{nameof(MemorySize)} must be >= 2*{nameof(PageSize)}.");
        if(SegmentSize>1L<<62)
            throw new ArgumentOutOfRangeException(nameof(SegmentSize),$"{nameof(SegmentSize)} must be <= 1L<<62.");
        if(ObjectLogSegmentSize is { } objectLogSegmentSizeValue&&objectLogSegmentSizeValue<PageSize)
            throw new ArgumentOutOfRangeException(nameof(ObjectLogSegmentSize),$"{nameof(ObjectLogSegmentSize)} must be >= {nameof(PageSize)}.");
        if(double.IsNaN(MutableFraction)||MutableFraction<0||MutableFraction>1)
            throw new ArgumentOutOfRangeException(nameof(MutableFraction));
        if(ObjectLogDevice.Capacity!=-1&&(ObjectLogDevice.Capacity<Const.MinObjectLogCapacity||ObjectLogDevice.Capacity>Const.MaxObjectLogCapacity))
            throw new ArgumentOutOfRangeException(nameof(ObjectLogDevice),$"{nameof(ObjectLogDevice)} capacity must be -1 or between {Const.MinObjectLogCapacity} and {Const.MaxObjectLogCapacity}.");
        this.LogMemorySize=MemorySize;
        this.MutableFraction=MutableFraction;
        this.OverflowMutableStrategyKind=OverflowMutableStrategyKind;
        this.StorageOverflowMutableStrategy=new(LogDevice.Capacity,StorageLimitFraction,StorageReserveBytes);
        long objectLogSegmentSize;
        if(ObjectLogDevice.Capacity<0) {
            objectLogSegmentSize=ObjectLogSegmentSize??Const.MinObjectLogCapacity;
        } else {
            objectLogSegmentSize=ObjectLogSegmentSize??ObjectLogDevice.Capacity;
            if(ObjectLogDevice.Capacity%objectLogSegmentSize!=0)
                throw new ArgumentOutOfRangeException(nameof(ObjectLogSegmentSize),$"ObjectLogDevice.Capacity must be a multiple of ObjectLogSegmentSize ({objectLogSegmentSize}), but was {ObjectLogDevice.Capacity}.");
            if(objectLogSegmentSize>ObjectLogDevice.Capacity)
                throw new ArgumentOutOfRangeException(nameof(ObjectLogSegmentSize),$"{nameof(ObjectLogSegmentSize)} must be <= ObjectLogDevice.Capacity.");
        }
        this.checkpointManager=CreateCheckpointManager(CheckpointDir);
        this.Settings=new() {
            IndexSize=IndexSize,
            LogMemorySize=MemorySize,
            PageSize=PageSize,
            SegmentSize=SegmentSize,

            MutableFraction=MutableFraction,
            LogDevice=LogDevice,
            ObjectLogDevice=ObjectLogDevice,
            ObjectLogSegmentSize=objectLogSegmentSize,
            CheckpointDir=CheckpointDir,
            CheckpointManager=this.checkpointManager,
            // 削除した最新の墓標を、連鎖から外して無効化させない(既定は無効化する)。無効化されると、メモリ上では古いレコードも無効なので削除は見えるが、
            // ログデバイス上の古いレコードは有効なまま残り、ログを走査する復元で削除したキーが復活する。墓標を有効なまま残せば、復元でも削除が見える。
            RevivificationSettings=global::Tsavorite.core.RevivificationSettings.InChainOnly,
        };
        this.Store=new TsavoriteKV<TStoreFunctions,TAllocator>(
            this.Settings,
            StoreFunctions,
            allocatorFactory
        );
    }

    /// <summary>
    /// チェックポイントの書き込みをバッファ付きにする。Tsavorite 既定のバッファなし書き込みは長さがセクター
    /// (通常 512 バイト)の倍数でないと Win32 エラー 87 で失敗するが、索引は 64 バイトから始まり、主索引の書き込み長は
    /// 索引と同じ長さになる。2.0.1-beta.5 ではこの失敗が黙って捨てられ主索引が保存されていなかった(2.1.8 は例外にする)。
    /// </summary>
    private static DeviceLogCommitCheckpointManager? CreateCheckpointManager(string? checkpointDir) {
        if(string.IsNullOrEmpty(checkpointDir)) {
            return null;
        }
        return new DeviceLogCommitCheckpointManager(
            new LocalStorageNamedDeviceFactoryCreator(disableFileBuffering: false),
            new DefaultCheckpointNamingScheme(new DirectoryInfo(checkpointDir).FullName),
            removeOutdated: false
        );
    }

    private protected CollectionStore(
        TStoreFunctions StoreFunctions,
        Func<AllocatorSettings,TStoreFunctions,TAllocator> allocatorFactory,
        long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction)
       : this(StoreFunctions,allocatorFactory,IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) {
    }

    private protected void AttachLayout(TsavoriteLayout layout) {
        this.layout=layout;
        this.indexBytes=layout.IndexSize;
        this.reservedTailAddress=0;
        this.diskBaseAddress=this.Store.Log.BeginAddress;
        this.SetManagedCount(0);
    }

    // 件数の管理(決定8)。旧 API で作ったデータ本体も含め、件数はデータ本体が 1 つだけ持つ(操作用オブジェクトごとには持たない)。
    // 操作用オブジェクトは追加・削除・新規の代入のたびに増減し、一括の集合演算や読み込みのあとは
    // 「不明」にして、次に件数を求めるときに全走査で数え直す。作った直後の空のデータ本体は 0 件で分かっている。
    private long managedCount;
    private int managedCountKnown = 1;

    internal bool TryGetManagedCount(out long count) {
        if(Volatile.Read(ref this.managedCountKnown)==1) {
            count=Interlocked.Read(ref this.managedCount);
            return true;
        }
        count=0;
        return false;
    }

    // 増減するのはシングル用の操作用オブジェクトだけで、同時に 1 つしか無いので排他命令は要らない。
    internal void AddManagedCount(long delta) => this.managedCount+=delta;

    internal void SetManagedCount(long count) {
        Interlocked.Exchange(ref this.managedCount,count);
        Volatile.Write(ref this.managedCountKnown,1);
    }

    internal void InvalidateManagedCount() => Volatile.Write(ref this.managedCountKnown,0);

    /// <summary>データ本体の件数。管理している件数が分かっていればそれを返し、不明なら全走査で数え直す。</summary>
    public long GetLongCount() => this.CountCore(owners: 0);

    /// <summary>操作用オブジェクトから件数を求める。分かっていればそれを、不明なら全走査で数える。</summary>
    internal long GetLongCountFromAccessor() => this.CountCore(owners: 1);

    // 全走査で数えた結果を件数として残すのは、生きている操作用オブジェクトが呼び出し元(owners 個)だけのとき。
    // 他の操作用オブジェクトがあると、書き込みと件数の増減の間に走査が入り、その書き込みを二重に数えるおそれがある。
    // 関所の中で数えるので、その間に新しい操作用オブジェクトは作られない。
    private long CountCore(int owners) {
        if(this.TryGetManagedCount(out var count)) {
            return count;
        }
        this.storeGate.Wait();
        try {
            if(this.TryGetManagedCount(out count)) {
                return count;
            }
            count=this.ScanLiveRecordCount();
            if(Volatile.Read(ref this.accessorCount)<=owners) {
                this.SetManagedCount(count);
            }
            return count;
        } finally {
            this.storeGate.Release();
        }
    }

    /// <summary>件数。分かっていればそれを、不明なら全走査で数える。操作用オブジェクトは作らない(関所の中で呼ぶ)。</summary>
    private protected override long GetRecoveryEntryCount() {
        if(this.TryGetManagedCount(out var count)) {
            return count;
        }
        return this.ScanLiveRecordCount();
    }

    /// <summary>削除の印を除いたレコードの数を、一時的なセッションで全走査して数える。</summary>
    private long ScanLiveRecordCount() => this.ScanLiveRecords().Count;

    private IterateLookupCounter ScanLiveRecords() {
        using var session = this.Store.NewSession<FixedSpanByteKey,EqualityEmpty,bool,EqualityEmpty,ContainsDelete>(new());
        var counter = new IterateLookupCounter();
        var cursor = 0L;
        session.IterateLookup(ref counter,ref cursor,resetCursor: false);
        session.BasicContext.CompletePending(wait: true);
        return counter;
    }

    /// <summary>
    /// ログの使われ方を、全走査して測る。生きているレコードの大きさと無駄の量が分かるので、詰め直し(<see cref="Compact"/>)をするかの判断に使う。
    /// 件数に応じた時間がかかるので、高頻度の監視には向かない。取得した時点の値であり、並行して書き込んでいる間は整合しない。
    /// </summary>
    public TsavoriteLogUsage MeasureLogUsage() {
        ObjectDisposedException.ThrowIf(this.Disposed,this);
        this.storeGate.Wait();
        try {
            var log = this.Store.Log;
            var logBytes = log.TailAddress-log.BeginAddress;
            var live = this.ScanLiveRecords();
            return new(live.Count,live.LiveBytes,logBytes);
        } finally {
            this.storeGate.Release();
        }
    }

    // 索引の自動拡張の目安。1 バケット(実効 7 件)あたり平均 4 件を超えたら倍にする(EXP-0003)。
    private const long AverageEntriesPerBucket = 4;
    // 現在の索引の大きさ(バイト)。予算の計算に使う。
    private long indexBytes;

    /// <summary>
    /// 書き込み前に呼ぶ。ログ末尾が予約済みの範囲を超えるなら、ページ単位で予算を追加予約し、必要なら索引を拡張する。
    /// 上限を超えると ResourceLimitException。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnsureCapacity(long tailAddress,int keyLength,int valueLength) {
        var requiredTailAddress = tailAddress+MemoryOverflowMutableStrategy.GetRequiredBytes(keyLength,valueLength);
        if(requiredTailAddress<=this.reservedTailAddress) {
            return;
        }
        this.GrowReservation(requiredTailAddress,keyLength+valueLength);
    }

    // データ本体の関所。索引の拡大・予算の予約・保存・読み込み・件数の走査・統計・詰め直しを順番に行う。
    // Tsavorite の状態機械(索引の拡大・チェックポイント)は同時に 1 つしか走れず、拡大は他の操作とも並行できない(EXP-0004)。
    // 予約と索引拡張はページに 1 回程度の頻度なので、書き込みの通常経路では通らない。
    // 保存の中で await するので Monitor ではなく SemaphoreSlim にする。再入できないので、関所の中では「…Core」の版を呼ぶ。
    private readonly SemaphoreSlim storeGate = new(1,1);

    private void GrowReservation(long requiredTailAddress,int payloadBytes) {
        this.storeGate.Wait();
        try {
            if(requiredTailAddress<=this.reservedTailAddress) {
                return;
            }
            this.GrowReservationCore(requiredTailAddress,payloadBytes);
        } finally {
            this.storeGate.Release();
        }
    }

    private void GrowReservationCore(long requiredTailAddress,int payloadBytes) {
        var layout = this.layout;
        if(layout is null) {
            this.reservedTailAddress=long.MaxValue;
            return;
        }
        var pageSize = this.Settings.PageSize;
        var target = checked((requiredTailAddress+pageSize-1)/pageSize*pageSize);
        var logBytes = target-this.Store.Log.BeginAddress;
        long memoryBytes;
        if(layout.Disk is null) {
            memoryBytes=checked(this.indexBytes+logBytes);
        } else {
            memoryBytes=checked(this.indexBytes+Math.Min(logBytes,this.LogMemorySize));
        }
        var previousMemoryBytes = layout.Memory.Bytes;
        layout.Memory.Resize(Math.Max(previousMemoryBytes,memoryBytes));
        if(layout.Disk is not null) {
            try {
                layout.Disk.Resize(Math.Max(layout.Disk.Bytes,this.retiredDiskBytes+target-this.diskBaseAddress));
            } catch {
                layout.Memory.Resize(previousMemoryBytes);
                throw;
            }
        }
        this.reservedTailAddress=target;
        // レコードの大きさが分かる書き込み時だけ判定する(復元時の予約では推定できないので拡張しない)。
        if(payloadBytes>0) {
            this.GrowIndexIfCrowded(layout,payloadBytes);
        }
    }

    /// <summary>
    /// ログの大きさから件数を推定し、索引が混んでいれば倍に拡張する。拡張分のメモリは予算から先に予約し、
    /// 予約できなければ拡張しない(正しさは変わらず、性能だけが落ちる)。
    /// </summary>
    private void GrowIndexIfCrowded(TsavoriteLayout layout,int payloadBytes) {
        if(!layout.AutoGrowIndex) {
            return;
        }
        this.lastPayloadBytes=payloadBytes;
        // Tsavorite の GrowIndexAsync は、他のセッションの操作と並行すると
        // StateMachineDriver 内で IndexOutOfRangeException を起こして停止する(EXP-0004。2.0.1-beta.5 と 2.1.8 で確認)。
        // 他の操作用オブジェクトが生きている間と、並列区間の間は拡張しない(正しさは変わらず、性能だけが落ちる)。
        // 並列区間で見送った拡張は、区間を閉じたとき(FinishParallelRegion)か、次のシングル用の作成で取り戻す。
        if(Volatile.Read(ref this.accessorCount)>1||this.parallelRegion is not null) {
            return;
        }
        // 1 レコード ≒ レコード情報(8)＋長さ情報＋キー＋値を 8 バイト境界へ切り上げた大きさとみなす。
        var recordBytes = Math.Max(16L,(payloadBytes+16L+7)&~7L);
        var estimatedRecords = (this.Store.Log.TailAddress-this.Store.Log.BeginAddress)/recordBytes;
        while(estimatedRecords>this.Store.IndexSize*AverageEntriesPerBucket) {
            if(!this.TryDoubleIndex(layout)) {
                return;
            }
        }
    }

    /// <summary>索引を倍にする。拡張分のメモリを予算から先に予約し、予約できなければ拡張せずに false。関所の中で、他の操作が無いときに呼ぶ。</summary>
    private bool TryDoubleIndex(TsavoriteLayout layout) {
        var grownBytes = checked(this.indexBytes*2);
        var previousMemoryBytes = layout.Memory.Bytes;
        try {
            layout.Memory.Resize(checked(previousMemoryBytes+grownBytes-this.indexBytes));
        } catch(global::Seamless.Execution.ResourceLimitException) {
            return false;
        }
        if(!this.Store.GrowIndexAsync().GetAwaiter().GetResult()) {
            layout.Memory.Resize(previousMemoryBytes);
            return false;
        }
        this.indexBytes=grownBytes;
        return true;
    }

    // 並列区間。開いている間(閉じ中を含む)は、シングル用の作成・2つ目の区間・読み込みを例外にする。
    private ParallelRegionState? parallelRegion;

    /// <summary>
    /// 並列区間を開く。操作用オブジェクトが 1 つも無いときだけ開ける。<paramref name="expectedCount"/> は区間を閉じたときの全体の見込み件数で、
    /// 索引をその件数に合う大きさまで先に広げる(予算が足りなければ広げずに続ける。旧 API と AutoGrowIndex=false では広げない)。
    /// </summary>
    private protected ParallelRegion<TAccessor> BeginParallelCore<TAccessor>(long expectedCount,Func<ParallelRegionState,TAccessor> create)
        where TAccessor : class {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedCount);
        ArgumentNullException.ThrowIfNull(create);
        ObjectDisposedException.ThrowIf(this.Disposed,this);
        this.storeGate.Wait();
        try {
            if(this.parallelRegion is not null)
                throw new InvalidOperationException("A parallel region is already open on this store.");
            if(Volatile.Read(ref this.accessorCount)!=0)
                throw new InvalidOperationException("Dispose the collections created by Create() before beginning a parallel region.");
            // 操作用オブジェクトが無いので、索引を広げても他の操作と並行しない(EXP-0004)。
            var layout = this.layout;
            if(layout is not null&&layout.AutoGrowIndex&&expectedCount>0) {
                var targetBytes = TsavoriteLayout.GetIndexSize(expectedCount);
                while(this.indexBytes<targetBytes) {
                    if(!this.TryDoubleIndex(layout)) {
                        break;
                    }
                }
            }
            var region = new ParallelRegionState(this);
            this.parallelRegion=region;
            return new ParallelRegion<TAccessor>(region,create);
        } finally {
            this.storeGate.Release();
        }
    }

    internal override void FinishParallelRegion(ParallelRegionState region,bool fromFinalizer) {
        if(fromFinalizer) {
            // ファイナライザからは関所を待たない。見送った索引の拡大は、次のシングル用の作成(OnAccessorCreated)で取り戻す。
            Interlocked.CompareExchange(ref this.parallelRegion,null,region);
            return;
        }
        this.storeGate.Wait();
        try {
            if(!ReferenceEquals(this.parallelRegion,region)) {
                return;
            }
            this.parallelRegion=null;
            // 区間の操作用オブジェクトはすべて破棄済みなので、索引を広げても他の操作と並行しない。
            var layout = this.layout;
            if(layout is not null&&!this.Disposed&&this.lastPayloadBytes>0&&Volatile.Read(ref this.accessorCount)==0) {
                this.GrowIndexIfCrowded(layout,this.lastPayloadBytes);
            }
        } finally {
            this.storeGate.Release();
        }
    }

    /// <summary>件数を全走査で数える。件数として残さない(マルチ用の ScanCount。他のスレッドが書いている途中でも使える)。</summary>
    internal long ScanCount() => this.ScanLiveRecordCount();

    /// <summary>全削除でログを捨てる前に呼ぶ。永続・一時ストアは、前の保存のログを上書きしないよう、新しい世代のファイルへ切り替える(BUG-0023)。</summary>
    internal void BeginClear() {
        var truncation = this.layout?.DeferredLogDevice;
        if(truncation is null) {
            return;
        }
        this.storeGate.Wait();
        try {
            truncation.StartNewGeneration();
        } finally {
            this.storeGate.Release();
        }
    }

    /// <summary>全削除などでログを捨てたあとに呼ぶ。予約を索引分だけに戻す。</summary>
    internal void ResetCapacity() {
        var layout = this.layout;
        if(layout is null) {
            return;
        }
        this.storeGate.Wait();
        try {
            layout.Memory.Resize(this.indexBytes);
            if(layout.Disk is not null){
                // 古い世代のファイルは、新しい保存が確定するまで残る。その分を予約に残す。
                this.retiredDiskBytes+=layout.Disk.Bytes;
                layout.Disk.Resize(this.retiredDiskBytes);
            }
            this.reservedTailAddress=0;
            // ログを作り直したので、ファイルは(保留なしで)消えている。前の保存のデータも無くなった。
            this.diskBaseAddress=this.Store.Log.BeginAddress;
            this.MarkOlderSavesInvalid();
        } finally {
            this.storeGate.Release();
        }
    }

    /// <summary>
    /// 集合演算(IntersectWith など)が一時的に使う作業リストの置き場所。共通ストア(一時・永続)から作った場合は
    /// データ本体のフォルダの中に置き、ストアの破棄や開き直しで一緒に片付くようにする。メモリストアと旧 API では %TEMP% を使う。
    /// </summary>
    internal string CreateWorkListPath(string operationName) {
        string root;
        if(this.layout is not null&&this.layout.Kind!=global::Seamless.Execution.StoreKind.Memory) {
            root=Path.Combine(this.layout.Owner.GetComponentDirectory(this.layout.Name),TsavoriteLayout.WorkListDirectoryName);
        } else {
            root=Path.Combine(Path.GetTempPath(),"Tsavorite","worklists");
        }
        var directoryPath = Path.Combine(root,operationName,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return Path.Combine(directoryPath,"items.dat");
    }

    /// <summary>作業リストのファイルが伸びるときに、ストアのディスク上限で確かめる口。共通ストアから作っていなければ null。</summary>
    internal WorkListQuota? CreateWorkListQuota() {
        if(this.layout is null||this.layout.Kind==global::Seamless.Execution.StoreKind.Memory) {
            return null;
        }
        return new WorkListQuota(this.layout.Owner.Budget.ReserveTemporaryStorage(0));
    }

    /// <summary>作業リスト 1 本分のディスク予約。作業リストを破棄したら Dispose する。</summary>
    internal sealed class WorkListQuota(global::Seamless.Execution.ResourceReservation reservation):IPersistedStorageQuota, IDisposable {
        public void EnsureTotalBytes(long totalBytes) {
            if(totalBytes>reservation.Bytes) {
                reservation.Resize(totalBytes);
            }
        }
        public void Dispose() => reservation.Dispose();
    }

    // 最後に書き込んだレコードのキー＋値の大きさ。操作用オブジェクトを作るときの拡張判定に使う。
    private int lastPayloadBytes;

    /// <summary>
    /// 操作用オブジェクトを作るとき、セッションを作る前に呼ぶ(権利を取る)。例外なら権利は取れていない。
    /// 取れたら、操作用オブジェクトの破棄(またはコンストラクタの失敗)で <see cref="OnAccessorDisposed"/> を 1 回だけ呼ぶ。
    /// <paramref name="region"/> は並列区間のマルチ用、null はシングル用。
    /// </summary>
    internal void OnAccessorCreated(ParallelRegionState? region) {
        ObjectDisposedException.ThrowIf(this.Disposed,this);
        // 拡張・保存・件数の走査の途中に新しい操作用オブジェクトが操作を始めないよう、関所を通してから使わせる。
        this.storeGate.Wait();
        try {
            if(region is null) {
                if(this.parallelRegion is not null)
                    throw new InvalidOperationException("Create() is not available while a parallel region is open. Dispose the region and the collections created from it first.");
                // シングル用は同時に 1 つだけ。件数を排他命令なしで増減し、索引を他の操作と並行せずに広げられるようにするため。
                if(Volatile.Read(ref this.accessorCount)!=0)
                    throw new InvalidOperationException("Only one collection created by Create() can be open at a time on this store. Dispose it first, or use BeginParallel() to work from multiple threads.");
                var count = Interlocked.Increment(ref this.accessorCount);
                try {
                    // 他に操作用オブジェクトが無いとき(=並行する操作が無いとき)に、並列中に見送った索引の拡張を取り戻す。
                    if(this.layout is not null&&count==1&&this.lastPayloadBytes>0) {
                        this.GrowIndexIfCrowded(this.layout,this.lastPayloadBytes);
                    }
                } catch {
                    Interlocked.Decrement(ref this.accessorCount);
                    throw;
                }
            } else {
                if(!ReferenceEquals(this.parallelRegion,region))
                    throw new ObjectDisposedException(nameof(ParallelRegionState),"The parallel region has been disposed.");
                // 閉じ中なら ObjectDisposedException。
                region.Enter();
                Interlocked.Increment(ref this.accessorCount);
                // マルチ用は件数を増減しないので、作った時点から件数は不明(区間を閉じたあとに数え直す)。
                this.InvalidateManagedCount();
            }
        } finally {
            this.storeGate.Release();
        }
    }

    /// <summary>操作用オブジェクトの権利を返す。ファイナライザからは関所を待たない。</summary>
    internal void OnAccessorDisposed(ParallelRegionState? region,bool fromFinalizer) {
        Interlocked.Decrement(ref this.accessorCount);
        region?.Leave(fromFinalizer);
    }

    /// <summary>生きている操作用オブジェクトの数(診断用)。</summary>
    internal int AccessorCount => Volatile.Read(ref this.accessorCount);

    string global::Seamless.Execution.IStoreComponent.Name => this.layout?.Name??string.Empty;

    string global::Seamless.Execution.IStoreComponent.Save() {
        this.AssertCheckpointDir();
        this.ThrowIfCheckpointInProgress();
        try {
            this.storeGate.Wait();
            try {
                return this.SaveComponentCore();
            } finally {
                this.storeGate.Release();
            }
        } finally {
            this.ReleaseCheckpoint();
        }
    }

    // 保存中の印を付けて、関所の中で呼ぶ。
    private string SaveComponentCore() {
        var token = this.TakeFullCheckpointCoreAsync(CheckpointType.Snapshot,writeRecoveryIndexMetadata: false,default).GetAwaiter().GetResult();
        // checkpoint だけでは、復元に必要なログのページがまだデバイスへ書き終わっていないことがある(EXP-0002)。
        // 保存が戻った時点で復元できるよう、ログを書き出す。ただし末尾(セクターの途中)まで書くと、次の書き出しが末尾を含むセクターを頭から書き直し、
        // その間に更新・削除で無効化された古いレコードが書き込まれて、この保存の復元で見えなくなる。セクター境界までに限れば、末尾を含む
        // セクターはスナップショット(保存時点の不変のコピー)に残る。
        this.FlushLogToSectorBoundary();
        // 件数は、操作用オブジェクトが無い(=保存の間に書き込みが無い)ときだけ記録する。あれば開き直したあとに数え直す。
        // 書き込みと件数の増減の間に保存が入ると、保存した中身と件数がずれるため。関所の中なので新しい操作用オブジェクトは作られない。
        long? count = null;
        if(Volatile.Read(ref this.accessorCount)==0&&this.TryGetManagedCount(out var managed)) {
            count=managed;
        }
        // この保存が必要とするログの先頭と、前の保存を外す必要があるか。関所の中で、チェックポイントを取った直後なので、先頭はチェックポイントのものと同じ。
        var generation = this.layout?.DeferredLogDevice?.Generation??0;
        this.pendingCommit=new(this.Store.Log.BeginAddress,this.olderSavesInvalid,this.invalidationSerial,generation);
        return new TsavoriteSavedState(token,this.Store.IndexSize*64,count,generation).Serialize();
    }

    bool global::Seamless.Execution.IStoreComponent.InvalidatesOlderSaves => this.pendingCommit is { InvalidatesOlder: true };

    void global::Seamless.Execution.IStoreComponent.SaveCommitted() {
        this.storeGate.Wait();
        try {
            this.CommitSaveCore();
        } finally {
            this.storeGate.Release();
        }
    }

    // 保存が確定した(マニフェストが書けた)あとの後始末。保留していたファイルの切り捨てを、この保存が必要とする先頭より前だけ実行し、ディスクの予約を物理的な大きさへ戻す。
    // 消せなくても保存は確定済みなので例外にしない(保留のまま残り、次の保存の確定でやり直す)。
    private void CommitSaveCore() {
        var commit = this.pendingCommit;
        if(commit is null) {
            return;
        }
        this.pendingCommit=null;
        if(commit.InvalidatesOlder&&this.invalidationSerial==commit.Serial) {
            this.olderSavesInvalid=false;
        }
        var truncation = this.layout?.DeferredLogDevice;
        if(truncation is null) {
            return;
        }
        // 保存のあとに全削除で世代が変わっていたら、保存が必要とするのは古い世代なので、何も消さない。
        if(truncation.Generation!=commit.Generation) {
            return;
        }
        var released = truncation.Release(commit.BeginAddress);
        // 前の保存が記録から外れたので、古い世代のファイルを消す(新しい保存は新しい世代を指す)。
        var retired = true;
        if(commit.InvalidatesOlder) {
            retired=truncation.RetireBefore(commit.Generation);
            if(retired&&!truncation.HasRetired) {
                this.retiredDiskBytes=0;
            }
        }
        if(released&&retired&&!truncation.HasPending) {
            this.diskBaseAddress=Math.Max(this.diskBaseAddress,commit.BeginAddress);
            this.ShrinkReservationCore();
        }
    }

    // 全削除でログのファイルを別の世代へ切り替えたあと、保存せずに全削除より前の保存へは戻せない(前の世代のデバイスの状態を安全に戻せない)。
    // 何も変えずに例外にする。ストアを開き直せば、前の保存へ戻れる。
    private void ThrowIfOtherGeneration(TsavoriteSavedState saved) {
        var truncation = this.layout?.DeferredLogDevice;
        if(truncation is not null&&truncation.Generation!=saved.Generation) {
            throw new InvalidOperationException("The checkpoint was saved before Clear() switched the log files and cannot be loaded in this session. Save first, or reopen the store to return to it.");
        }
    }

    void global::Seamless.Execution.IStoreComponent.ValidateLoad(string state) {
        this.AssertCheckpointDir();
        var saved = TsavoriteSavedState.Parse(state);
        this.ThrowIfOtherGeneration(saved);
        // Tsavorite は保存番号のフォルダの中身から復元する。フォルダが無ければ読み込めない。
        var checkpointDir = this.Settings.CheckpointDir!;
        foreach(var kindDirectory in new[] { "cpr-checkpoints","index-checkpoints" }) {
            if(!Directory.Exists(Path.Combine(checkpointDir,kindDirectory,saved.Token.ToString()))) {
                throw new InvalidOperationException($"The checkpoint '{saved.Token}' of '{this.layout?.Name}' cannot be loaded: its {kindDirectory} folder does not exist.");
            }
        }
    }

    // 前の保存へは戻れなくなる変更をした印を付ける(関所の中で呼ぶ)。
    private void MarkOlderSavesInvalid() {
        this.olderSavesInvalid=true;
        this.invalidationSerial++;
    }

    void global::Seamless.Execution.IStoreComponent.Load(string state) {
        if(Volatile.Read(ref this.accessorCount)!=0)
            throw new InvalidOperationException("Dispose all collections created from this store before loading a checkpoint.");
        var saved = TsavoriteSavedState.Parse(state);
        this.ThrowIfOtherGeneration(saved);
        this.storeGate.Wait();
        try {
            this.LoadCoreAsync(saved.Token,default).GetAwaiter().GetResult();
            if(this.layout is not null) {
                this.indexBytes=this.Store.IndexSize*64;
                this.layout.Memory.Resize(this.indexBytes);
            }
            this.reservedTailAddress=0;
            var requiredTailAddress = this.Store.Log.TailAddress+MemoryOverflowMutableStrategy.GetRequiredBytes(0,0);
            this.GrowReservationCore(requiredTailAddress,0);
            if(saved.Count is long count) {
                this.SetManagedCount(count);
            } else {
                this.InvalidateManagedCount();
            }
        } finally {
            this.storeGate.Release();
        }
    }

    public void AssertCheckpointDir() {
        if(string.IsNullOrEmpty(this.Settings.CheckpointDir))
            throw new InvalidOperationException($"{nameof(this.Settings.CheckpointDir)} is not set.");
    }

    public async ValueTask<Guid> SaveAsync(CheckpointType CheckpointType = CheckpointType.Snapshot,CancellationToken ct = default) {
        this.AssertCheckpointDir();
        // 保存中の再実行は、関所で待たずに例外にする(先に印を付ける)。
        this.ThrowIfCheckpointInProgress();
        try {
            await this.storeGate.WaitAsync(ct);
            try {
                var directToken = await this.TakeFullCheckpointCoreAsync(CheckpointType,writeRecoveryIndexMetadata: true,ct);
                this.CommitDirectSaveCore();
                return directToken;
            } finally {
                this.storeGate.Release();
            }
        } finally {
            this.ReleaseCheckpoint();
        }
    }

    // 共通ストアから作った場合は索引の大きさをストアの保存に記録するので、全走査による件数計算と
    // checkpoint フォルダ内へのメタデータ書き込み(BUG-0006 の共有違反が起きる箇所)は行わない。
    // 保存中の印を付けて、関所の中で呼ぶ。
    private async ValueTask<Guid> TakeFullCheckpointCoreAsync(CheckpointType CheckpointType,bool writeRecoveryIndexMetadata,CancellationToken ct) {
        try {
            long recoveryEntryCount = 0;
            if(writeRecoveryIndexMetadata) {
                recoveryEntryCount=this.GetRecoveryEntryCount();
            }
            var (success,token)=await this.Store.TakeFullCheckpointAsync(CheckpointType,ct);
            await this.Store.CompleteCheckpointAsync(ct);
            if(success) {
                this.CheckpointFailed=false;
                if(writeRecoveryIndexMetadata) {
                    this.WriteRecoveryIndexMetadata(token,recoveryEntryCount);
                }
                this.MarkUnflushed(token);
                return token;
            }
            throw new InvalidOperationException($"{nameof(this.Save)}Checkpoint failed.");
        } catch {
            this.CheckpointFailed=true;
            throw;
        }
    }

    public Guid Save(CheckpointType CheckpointType = CheckpointType.Snapshot) => this.SaveAsync(CheckpointType).GetAwaiter().GetResult();

    public async ValueTask<Guid> SaveLogOnlyAsync(CheckpointType CheckpointType = CheckpointType.Snapshot,CancellationToken ct = default) {
        this.AssertCheckpointDir();
        // 保存中の再実行は、関所で待たずに例外にする(先に印を付ける)。
        this.ThrowIfCheckpointInProgress();
        try {
            await this.storeGate.WaitAsync(ct);
            try {
                var recoveryEntryCount=this.GetRecoveryEntryCount();
                var (success,token)=await this.Store.TakeHybridLogCheckpointAsync(CheckpointType,cancellationToken: ct);
                await this.Store.CompleteCheckpointAsync(ct);
                if(success) {
                    this.CheckpointFailed=false;
                    this.WriteRecoveryIndexMetadata(token,recoveryEntryCount);
                    this.MarkUnflushed(token);
                    this.CommitDirectSaveCore();
                    return token;
                }
                throw new InvalidOperationException($"{nameof(this.SaveLogOnly)}Checkpoint failed.");
            } catch {
                this.CheckpointFailed=true;
                throw;
            } finally {
                this.storeGate.Release();
            }
        } finally {
            this.ReleaseCheckpoint();
        }
    }

    private void ThrowIfCheckpointInProgress() {
        if(Interlocked.CompareExchange(ref this.CheckpointInProgress,1,0)!=0)
            throw new InvalidOperationException("Checkpoint already in progress.");
    }

    private void ReleaseCheckpoint() {
        Volatile.Write(ref this.CheckpointInProgress,0);
    }

    private void WriteRecoveryIndexMetadata(Guid token,long recoveryEntryCount) {
        global::Seamless.Collections.Tsavorite.Store.WriteRecoveryIndexMetadata(this.Settings.CheckpointDir!,token,recoveryEntryCount);
    }

    public Guid SaveLogOnly(CheckpointType CheckpointType = CheckpointType.Snapshot) => this.SaveLogOnlyAsync(CheckpointType).GetAwaiter().GetResult();

    /// <summary>
    /// 復元した結果、末尾ページのスロットが先頭ページのスロットと重なっていたら例外にする。この状態では先頭側のページの内容が消えているので、
    /// 黙って欠落させない。<see cref="RecoverWithSizeTrackerAsync"/> で防いでいるので通常は起きない。防ぎきれなくなった(Garnet の内部が変わった)ときの安全網。
    /// 【記録】2026-09-30 の 600 試行(保存時・開き直し時のメモリと件数をランダムに振った)で、追跡なしの復元では実際の欠落とこの条件が完全に一致した(見逃し 0・誤検知 0)。
    /// </summary>
    internal void ThrowIfRecoveredTailCollidesWithHead() {
        var log = this.Store.Log;
        var pageSize = this.Settings.PageSize;
        if(log.TailAddress/pageSize-log.HeadAddress/pageSize>=log.BufferSize) {
            throw new InvalidDataException("Recovery lost log pages: the recovered log tail page shares its memory slot with the head page. Reopen with the original LogMemorySize or larger, or load an earlier checkpoint.");
        }
    }

    /// <summary>
    /// 復元の間だけ LogSizeTracker を付けて復元する。Garnet 2.0.1-beta.6 以降(2.1.8 でも確認)の復元は、LogSizeTracker が無いと復元中にページを追い出さず
    /// (<c>TrimLogPages</c> が何もしない)、バッファに収まらないページの内容を失う。次の 2 つの場合に起きる。
    /// (1) 保存時にログの末尾がページ境界ちょうどで、バッファが満杯だった(復元後の末尾ページのスロットが先頭ページのスロットと同じで、先頭ページが消える)。
    /// (2) 保存時より小さいメモリで開き直した(HeadAddress が進まず、上書きされたページが「メモリ上にある」ままになる)。
    /// 追跡は開始せず(背景の追い出しは動かさない)、復元が追い出しを判断するためだけに使い、終わったら外す。目標は復元の判断に使われないので、メモリ量(下限 4 ページ)とする。
    /// 2.0.1-beta.5 は追跡が無くても正しく復元した。これで、既に境界で保存済みのチェックポイントも復元できる。
    /// 【記録】2026-09-30 の実験：追跡ありの復元は、境界の件数の掃引・メモリを縮めた開き直しのランダム 600 試行・管理型の辞書・FoldOver・SaveLogOnly で欠落 0、
    /// 強制終了ハーネス、および 2.1.8 で境界保存され欠落していた元のチェックポイントの復元でも欠落 0。
    /// </summary>
    private async ValueTask RecoverWithSizeTrackerAsync(Guid? token,CancellationToken ct) {
        var tracker = new LogSizeTracker<TStoreFunctions,TAllocator>(this.Store.Log,Math.Max(this.Settings.LogMemorySize,4*this.Settings.PageSize),0,0,null!);
        this.Store.Log.SetLogSizeTracker(tracker);
        try {
            if(token is Guid checkpointToken) {
                await this.Store.RecoverAsync(Guid.Empty,checkpointToken,cancellationToken: ct);
            } else {
                await this.Store.RecoverAsync(cancellationToken: ct);
            }
        } finally {
            this.Store.Log.SetLogSizeTracker(null!);
        }
    }

    private void FlushLogToSectorBoundary() {
        var log = this.Store.Log;
        var sectorSize = Math.Max(512L,this.Settings.LogDevice.SectorSize);
        var alignedTail = log.TailAddress/sectorSize*sectorSize;
        if(alignedTail>log.FlushedUntilAddress) {
            log.ShiftReadOnlyAddress(alignedTail,true);
        }
    }

    private void MarkUnflushed(Guid token) {
        lock(this.unflushedTokens) {
            this.unflushedTokens.Add(token);
        }
    }

    /// <summary>
    /// 保存(Save)したチェックポイントと、ログのデバイスのファイルを、電源断でも残るようディスクへ確実に書き出す。
    /// Save は書き込みを終えるだけで、OS のキャッシュに残っていてもよい。Save を続けて呼んだあとに 1 回呼べば、まとめて書き出せる。
    /// 共通ストアの永続ストアは、保存のたびにマニフェストを書く前に自分で呼ぶ。
    /// </summary>
    public void Flush() {
        ObjectDisposedException.ThrowIf(this.Disposed,this);
        Guid[] tokens;
        lock(this.unflushedTokens) {
            tokens=[..this.unflushedTokens];
        }
        foreach(var path in this.GetFlushTargets(tokens)) {
            FlushFile(path);
        }
        lock(this.unflushedTokens) {
            this.unflushedTokens.ExceptWith(tokens);
        }
    }

    /// <summary>Flush が書き出すファイルの一覧。指定した保存番号のチェックポイントと、ログとオブジェクトログのデバイスのファイル。</summary>
    internal List<string> GetFlushTargets(IReadOnlyCollection<Guid> tokens) {
        var targets = new List<string>();
        var checkpointDir = this.Settings.CheckpointDir;
        if(!string.IsNullOrEmpty(checkpointDir)&&Directory.Exists(checkpointDir)) {
            foreach(var token in tokens) {
                // チェックポイントの種類ごとのフォルダ(index-checkpoints など)の下に、保存番号の名前のフォルダができる。
                foreach(var kindDirectory in Directory.EnumerateDirectories(checkpointDir)) {
                    var tokenDirectory = Path.Combine(kindDirectory,token.ToString());
                    if(Directory.Exists(tokenDirectory)) {
                        targets.AddRange(Directory.EnumerateFiles(tokenDirectory,"*",SearchOption.AllDirectories));
                    }
                }
                foreach(var path in global::Seamless.Collections.Tsavorite.Store.EnumerateCheckpointIndexMetadataPaths(checkpointDir,token)) {
                    if(File.Exists(path)) {
                        targets.Add(path);
                    }
                }
            }
        }
        AddDeviceFiles(targets,this.Settings.LogDevice);
        AddDeviceFiles(targets,this.Settings.ObjectLogDevice);
        return targets;
    }

    private static void AddDeviceFiles(List<string> targets,IDevice device) {
        var name = device.FileName;
        if(device is MemoryOnlyDevice||string.IsNullOrEmpty(name)) {
            return;
        }
        var path = Path.GetFullPath(name);
        var directory = Path.GetDirectoryName(path);
        if(directory is null||!Directory.Exists(directory)) {
            return;
        }
        var fileName = Path.GetFileName(path);
        // セグメントのファイルは「名前.番号」(番号を付けないデバイスは「名前」だけ)。同じ接頭辞の別のファイル(名前_obj.0 など)は含めない。
        foreach(var file in Directory.EnumerateFiles(directory,fileName+"*")) {
            var suffix = Path.GetFileName(file).AsSpan(fileName.Length);
            if(suffix.IsEmpty||(suffix[0]=='.'&&suffix.Length>1&&suffix[1..].IndexOfAnyExceptInRange('0','9')<0)) {
                targets.Add(file);
            }
        }
    }

    private static void FlushFile(string path) {
        try {
            // デバイスが開いているのと同じファイルなので、読み書きと削除の共有を許して開く(一時ファイルは閉じると削除されるので削除も許す)。
            using var handle = File.OpenHandle(path,FileMode.Open,FileAccess.Write,FileShare.ReadWrite|FileShare.Delete);
            RandomAccess.FlushToDisk(handle);
        } catch(FileNotFoundException) {
        } catch(DirectoryNotFoundException) {
        }
    }

    public async ValueTask LoadAsync(CancellationToken ct = default) {
        await this.storeGate.WaitAsync(ct);
        try {
            await this.LoadCoreAsync(null,ct);
        } finally {
            this.storeGate.Release();
        }
    }

    public void Load() => this.LoadAsync().GetAwaiter().GetResult();

    public async ValueTask LoadAsync(Guid token,CancellationToken ct = default) {
        await this.storeGate.WaitAsync(ct);
        try {
            await this.LoadCoreAsync(token,ct);
        } finally {
            this.storeGate.Release();
        }
    }

    // 関所の中で呼ぶ。
    private async ValueTask LoadCoreAsync(Guid? token,CancellationToken ct) {
        this.AssertCheckpointDir();
        if(this.parallelRegion is not null)
            throw new InvalidOperationException("Dispose the parallel region and the collections created from it before loading a checkpoint.");
        this.retiredDiskBytes=0;
        await this.RecoverWithSizeTrackerAsync(token,ct);
        this.ThrowIfRecoveredTailCollidesWithHead();
        this.CheckpointFailed=false;
        // ログを保存した状態へ戻したので、保留していた切り捨ては、戻した先が必要とするファイルを消してしまう。捨てる。
        this.layout?.DeferredLogDevice?.DiscardPending();
        this.pendingCommit=null;
        this.olderSavesInvalid=false;
        this.diskBaseAddress=this.Store.Log.BeginAddress;
        // 復元した中身の件数は分からない(共通ストアの読み込みは、保存した件数があればこのあと戻す)。
        this.InvalidateManagedCount();
    }

    public void Load(Guid token) {
        var ct = CancellationToken.None;
        this.LoadAsync(token,ct).GetAwaiter().GetResult();
    }

    public (long BeginAddress,long HeadAddress,long ReadOnlyAddress,long TailAddress) Address {
        get {
            var Log = this.Store.Log;
            return (Log.BeginAddress,Log.HeadAddress,Log.ReadOnlyAddress,Log.TailAddress);
        }
    }

    /// <summary>
    /// 索引とログの観測値を返す。取得した時点の値であり、並行して書き込んでいる間は項目どうしが整合しない。
    /// EntryCount は索引の使用中の枠の数であり、コレクションの件数ではない。
    /// EntryCount の取得で索引全体と溢れバケットを走査するため、高頻度の監視には向かない。
    /// </summary>
    public TsavoriteStatistics GetStatistics() {
        // 索引の拡大と同時に索引を走査しない(EntryCount は索引全体を見る)。
        this.storeGate.Wait();
        try {
            var indexSize = this.Store.IndexSize;
            var log = this.Store.Log;
            var beginAddress = log.BeginAddress;
            var headAddress = log.HeadAddress;
            var readOnlyAddress = log.ReadOnlyAddress;
            var tailAddress = log.TailAddress;
            long? count = null;
            if(this.TryGetManagedCount(out var managedCount)) {
                count=managedCount;
            }
            return new(
                indexSize,
                checked(indexSize*64),
                this.Store.EntryCount,
                this.Store.OverflowBucketCount,
                this.Store.OverflowBucketAllocations,
                beginAddress,
                headAddress,
                readOnlyAddress,
                tailAddress,
                tailAddress-headAddress,
                headAddress-beginAddress,
                this.Store.HighWaterAllocatedPageCount,
                count,
                tailAddress-beginAddress);
        } finally {
            this.storeGate.Release();
        }
    }

    /// <summary>
    /// 索引のバケットの分布を文字列で返す。索引全体を走査する高コストな操作。
    /// 取得した時点の値であり、並行して書き込んでいる間の整合性は保証しない。
    /// </summary>
    public string DumpIndexDistribution() {
        this.storeGate.Wait();
        try {
            return this.Store.DumpDistribution();
        } finally {
            this.storeGate.Release();
        }
    }

    /// <summary>旧 API。先頭のセグメント 1 つ分だけを詰める。全体を詰めるときは <see cref="Compact"/> を使う。</summary>
    public void CompactWithLookup() {
        this.storeGate.Wait();
        try {
            this.CompactWithLookupCore();
        } finally {
            this.storeGate.Release();
        }
    }

    /// <summary>
    /// 詰め直す。生きているレコードをログの末尾へ少しずつ移し、先頭を進めて、削除や上書きで無駄になった領域を空きへ戻す。
    /// 件数と中身は変わらない。操作用オブジェクトも並列区間も無いときだけ呼べる(あれば <see cref="InvalidOperationException"/>)。
    /// 空きが足りなければ、データを移す前に <see cref="global::Seamless.Execution.ResourceLimitException"/> になる。
    /// 永続・一時ストアでは、ディスク上のファイルは次の共通ストアの保存が確定するまで消えない。保存すると、これより前の保存は記録から外れ、戻れなくなる。
    /// </summary>
    public CompactionResult Compact() {
        ObjectDisposedException.ThrowIf(this.Disposed,this);
        this.storeGate.Wait();
        try {
            return this.CompactCore();
        } finally {
            this.storeGate.Release();
        }
    }

    // 関所の中で呼ぶ。
    private CompactionResult CompactCore() {
        if(Volatile.Read(ref this.accessorCount)!=0)
            throw new InvalidOperationException("Dispose the collections created by Create() before compacting.");
        if(this.parallelRegion is not null)
            throw new InvalidOperationException("Dispose the parallel region and the collections created from it before compacting.");
        var log = this.Store.Log;
        var beginBefore = log.BeginAddress;
        var tailBefore = log.TailAddress;
        // いまあるログを書き換えさせないよう、全体を読み取り専用にして、移す範囲をここまでに決める(移した写しは範囲の外)。
        log.ShiftReadOnlyAddress(tailBefore,true);
        var target = log.SafeReadOnlyAddress;
        var pageSize = this.Settings.PageSize;
        var chunkBytes = this.GetCompactionChunkBytes();
        var cursor = log.BeginAddress;
        while(cursor<target) {
            var iterationBytes = chunkBytes;
            if(this.OverflowMutableStrategyKind==OverflowMutableStrategyKind.Memory) {
                // メモリ種はメモリ上のログが全部で、退避先は捨てる。写しを書くために新しいページを開くと、先頭のページが追い出される。
                // 先頭の側のページはまだ読み終えていないので、追い出されると生きているレコードを失う(EXP: 満杯のログで 123 件が消えた)。
                // 写しが開くページの数だけ空きがあるときだけ進め、足りなければ何も移さずに例外にする(余裕として 2 ページ残す)。
                var usedPages = log.TailAddress/pageSize-log.BeginAddress/pageSize+1;
                var freePages = log.BufferSize-usedPages-2;
                if(freePages<1) {
                    throw new global::Seamless.Execution.ResourceLimitException(global::Seamless.Execution.ResourceLimitKind.Memory,checked((usedPages+3)*pageSize),checked(log.BufferSize*pageSize));
                }
                iterationBytes=Math.Min(iterationBytes,freePages*pageSize);
            }
            // 1 区切りは、ページ(レコードがまたがない境界)の倍数にそろえる。移した写しは末尾に書かれるので、その分の予算の空きも先に確かめる。
            var until = Math.Min(target,cursor/pageSize*pageSize+iterationBytes);
            try {
                this.GrowReservationCore(log.TailAddress+(until-cursor)+MemoryOverflowMutableStrategy.GetRequiredBytes(0,0),0);
            } catch(global::Seamless.Execution.ResourceLimitException) when(chunkBytes>pageSize) {
                // 空きが 1 区切りに足りないので、区切りを半分にして続ける(1 ページでも足りなければ、そのまま例外)。
                chunkBytes/=2;
                continue;
            }
            var done = log.Compact<TKey,TValue,EqualityEmpty>(until,CompactionType.Lookup);
            if(done<=cursor) {
                break;
            }
            cursor=done;
            this.ShrinkReservationCore();
        }
        if(log.BeginAddress>beginBefore) {
            // 先頭を進めたので、前の保存のデータは(次の保存の確定後に)消える。
            this.MarkOlderSavesInvalid();
        }
        this.ShrinkReservationCore();
        return new(beginBefore,log.BeginAddress,tailBefore,log.TailAddress);
    }

    // 1 区切りの大きさ。メモリ上のログの 1/16(最大 16MB)を、ページの倍数にそろえる。大きいほど呼び出しが少なく、小さいほど必要な空きが少ない。
    private long GetCompactionChunkBytes() {
        var pageSize = this.Settings.PageSize;
        var chunk = Math.Min(16L<<20,this.LogMemorySize/16);
        chunk=chunk/pageSize*pageSize;
        return Math.Max(pageSize,chunk);
    }

    // 詰め直しのあとなど、実際に使っている大きさへ予算の予約を戻す。メモリは論理的な先頭から、ディスクは物理的な先頭(保留中のファイルを含む)から数える。
    private void ShrinkReservationCore() {
        var layout = this.layout;
        if(layout is null) {
            return;
        }
        var pageSize = this.Settings.PageSize;
        var log = this.Store.Log;
        var requiredTail = log.TailAddress+MemoryOverflowMutableStrategy.GetRequiredBytes(0,0);
        var target = checked((requiredTail+pageSize-1)/pageSize*pageSize);
        var logBytes = target-log.BeginAddress;
        long memoryBytes;
        if(layout.Disk is null) {
            memoryBytes=checked(this.indexBytes+logBytes);
        } else {
            memoryBytes=checked(this.indexBytes+Math.Min(logBytes,this.LogMemorySize));
        }
        layout.Memory.Resize(memoryBytes);
        layout.Disk?.Resize(Math.Max(0,this.retiredDiskBytes+target-this.diskBaseAddress));
        this.reservedTailAddress=target;
    }

    // 共通ストアを通さない直接の保存(SaveAsync など)は、マニフェストが無く、保存が戻った時点で確定している。保留していた切り捨てをここで実行する。
    private void CommitDirectSaveCore() {
        var truncation = this.layout?.DeferredLogDevice;
        if(truncation is null) {
            return;
        }
        if(truncation.Release(this.Store.Log.BeginAddress)&&!truncation.HasPending) {
            this.diskBaseAddress=Math.Max(this.diskBaseAddress,this.Store.Log.BeginAddress);
            this.ShrinkReservationCore();
        }
        this.olderSavesInvalid=false;
    }

    private void CompactWithLookupCore() {
        var store = this.Store;
        var beginAddress = store.Log.BeginAddress;
        var segmentSize = this.Settings.SegmentSize;
        var nextSegmentStart = (beginAddress/segmentSize+1)*segmentSize;
        var compactUntil = nextSegmentStart;
        var safeReadOnlyAddress = store.Log.SafeReadOnlyAddress;
        if(compactUntil>safeReadOnlyAddress) {
            compactUntil=safeReadOnlyAddress;
        }
        if(compactUntil<=beginAddress) {
            return;
        }
        var untilAddress = this.Store.Log.Compact<TKey,TValue,EqualityEmpty>(
            compactUntil,
            CompactionType.Lookup
        );
        store.Log.ShiftBeginAddress(untilAddress,true,true);
        if(store.Log.BeginAddress>beginAddress) {
            this.MarkOlderSavesInvalid();
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
            // 共通ストアから作った場合は、確定は Save(checkpoint)だけなので破棄時の書き出しは要らない。
            if(this.layout is null&&this.Store is not null&&this.UsesStorageDevice&&!this.CheckpointFailed) {
                this.Store.Log.FlushAndEvict(wait: true);
            }
        }
        this.Store?.Dispose();
        this.checkpointManager?.Dispose();
        this.Settings?.Dispose();
        if(disposing&&this.layout is not null) {
            this.layout.Owner.Unregister(this);
            this.layout.Dispose();
        }
    }

    ~CollectionStore() => this.Dispose(false);
}

/// <summary>削除の印を除いたレコードを数える走査関数。データ本体の件数の数え直しに使う。</summary>
// 構造体にすると Tsavorite の走査の中でコピーされ、数えた結果が戻らない(クラスにする)。
internal sealed class IterateLookupCounter:IScanIteratorFunctions {
    public long Count;
    /// <summary>生きているレコードがログで使っている大きさ(レコードの確保サイズの合計)。</summary>
    public long LiveBytes;
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
        where TSourceLogRecord : ISourceLogRecord {
        if(logRecord.Info.Tombstone) {
            cursorRecordResult=CursorRecordResult.Skip;
        } else {
            cursorRecordResult=CursorRecordResult.Accept;
            this.Count++;
            this.LiveBytes+=logRecord.AllocatedSize;
        }
        return true;
    }
    public bool OnStart(long beginAddress,long endAddress) {
        this.Count=0;
        this.LiveBytes=0;
        return true;
    }
    public void OnStop(bool completed,long numberOfRecords) {
    }
    public void OnException(Exception exception,long numberOfRecords) {
    }
}