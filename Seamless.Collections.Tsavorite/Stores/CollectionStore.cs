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
        this.SetManagedCount(0);
    }

    // 件数の管理(決定8)。共通ストアから作ったデータ本体だけが使い、旧 API では従来どおり走査で数える。
    // 件数を管理する操作用オブジェクトは追加・削除のたびに増減し、挿入か上書きか分からない操作や一括の集合演算のあとは
    // 「不明」にして、次に件数を求めるときに全走査で数え直す。
    private long managedCount;
    private int managedCountKnown;

    /// <summary>件数をデータ本体で管理しているか(共通ストアから作ったか)。</summary>
    internal bool TracksCount => this.layout is not null;

    internal bool TryGetManagedCount(out long count) {
        if(this.layout is not null&&Volatile.Read(ref this.managedCountKnown)==1) {
            count=Interlocked.Read(ref this.managedCount);
            return true;
        }
        count=0;
        return false;
    }

    internal void AddManagedCount(long delta) {
        if(this.layout is not null) {
            Interlocked.Add(ref this.managedCount,delta);
        }
    }

    internal void SetManagedCount(long count) {
        if(this.layout is null) {
            return;
        }
        Interlocked.Exchange(ref this.managedCount,count);
        Volatile.Write(ref this.managedCountKnown,1);
    }

    internal void InvalidateManagedCount() => Volatile.Write(ref this.managedCountKnown,0);

    /// <summary>
    /// 件数を保持しない操作用オブジェクト(CreateUncounted)で集計した増減を、データ本体の件数へまとめて反映する。
    /// 旧 API で作ったデータ本体では NotSupportedException。
    /// </summary>
    public void ApplyCountDelta(long delta) {
        if(this.layout is null)
            throw new NotSupportedException("Count management is available only for stores created from Seamless.Execution.Store.");
        Interlocked.Add(ref this.managedCount,delta);
    }

    /// <summary>データ本体の件数。管理している件数が分かっていればそれを返し、不明なら全走査で数え直す。</summary>
    public long GetLongCount() {
        if(this.TryGetManagedCount(out var count)) {
            return count;
        }
        count=this.GetRecoveryEntryCount();
        this.SetManagedCount(count);
        return count;
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

    // 予約と索引拡張はページに 1 回程度の頻度なので、スレッドごとの操作用オブジェクトからの同時呼び出しをここで直列にする。
    private readonly object capacityGate = new();

    private void GrowReservation(long requiredTailAddress,int payloadBytes) {
        lock(this.capacityGate) {
            if(requiredTailAddress<=this.reservedTailAddress) {
                return;
            }
            this.GrowReservationCore(requiredTailAddress,payloadBytes);
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
                layout.Disk.Resize(Math.Max(layout.Disk.Bytes,logBytes));
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
        // 他の操作用オブジェクトが生きている間は拡張しない(正しさは変わらず、性能だけが落ちる)。
        if(Volatile.Read(ref this.accessorCount)>1) {
            return;
        }
        // 1 レコード ≒ レコード情報(8)＋長さ情報＋キー＋値を 8 バイト境界へ切り上げた大きさとみなす。
        var recordBytes = Math.Max(16L,(payloadBytes+16L+7)&~7L);
        var estimatedRecords = (this.Store.Log.TailAddress-this.Store.Log.BeginAddress)/recordBytes;
        while(estimatedRecords>this.Store.IndexSize*AverageEntriesPerBucket) {
            var grownBytes = checked(this.indexBytes*2);
            var previousMemoryBytes = layout.Memory.Bytes;
            try {
                layout.Memory.Resize(checked(previousMemoryBytes+grownBytes-this.indexBytes));
            } catch(global::Seamless.Execution.ResourceLimitException) {
                return;
            }
            if(!this.Store.GrowIndexAsync().GetAwaiter().GetResult()) {
                layout.Memory.Resize(previousMemoryBytes);
                return;
            }
            this.indexBytes=grownBytes;
        }
    }

    /// <summary>全削除などでログを捨てたあとに呼ぶ。予約を索引分だけに戻す。</summary>
    internal void ResetCapacity() {
        var layout = this.layout;
        if(layout is null) {
            return;
        }
        lock(this.capacityGate) {
            layout.Memory.Resize(this.indexBytes);
            layout.Disk?.Resize(0);
            this.reservedTailAddress=0;
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

    internal void OnAccessorCreated() {
        var count = Interlocked.Increment(ref this.accessorCount);
        if(this.layout is null) {
            return;
        }
        // 拡張中に新しい操作用オブジェクトが操作を始めないよう、拡張と同じロックを通してから使わせる。
        lock(this.capacityGate) {
            // 他に操作用オブジェクトが無いとき(=並行する操作が無いとき)に、並列中に見送った索引の拡張を取り戻す。
            if(count==1&&this.lastPayloadBytes>0) {
                this.GrowIndexIfCrowded(this.layout,this.lastPayloadBytes);
            }
        }
    }

    internal void OnAccessorDisposed() => Interlocked.Decrement(ref this.accessorCount);

    string global::Seamless.Execution.IStoreComponent.Name => this.layout?.Name??string.Empty;

    string global::Seamless.Execution.IStoreComponent.Save() {
        var token = this.TakeFullCheckpointAsync(CheckpointType.Snapshot,writeRecoveryIndexMetadata: false,default).GetAwaiter().GetResult();
        // checkpoint だけでは、復元に必要なログのページがまだデバイスへ書き終わっていないことがある(EXP-0002)。
        // 保存が戻った時点で復元できるよう、ログを書き出す。ただし末尾(セクターの途中)まで書くと、次の書き出しが末尾を含むセクターを頭から書き直し、
        // その間に更新・削除で無効化された古いレコードが書き込まれて、この保存の復元で見えなくなる。セクター境界までに限れば、末尾を含む
        // セクターはスナップショット(保存時点の不変のコピー)に残る。
        this.FlushLogToSectorBoundary();
        long? count = null;
        if(this.TryGetManagedCount(out var managed)) {
            count=managed;
        }
        return new TsavoriteSavedState(token,this.Store.IndexSize*64,count).Serialize();
    }

    void global::Seamless.Execution.IStoreComponent.Load(string state) {
        if(Volatile.Read(ref this.accessorCount)!=0)
            throw new InvalidOperationException("Dispose all collections created from this store before loading a checkpoint.");
        var saved = TsavoriteSavedState.Parse(state);
        this.Load(saved.Token);
        if(this.layout is not null) {
            this.indexBytes=this.Store.IndexSize*64;
            this.layout.Memory.Resize(this.indexBytes);
        }
        this.reservedTailAddress=0;
        this.EnsureCapacity(this.Store.Log.TailAddress,0,0);
        if(saved.Count is long count) {
            this.SetManagedCount(count);
        } else {
            this.InvalidateManagedCount();
        }
    }

    public void AssertCheckpointDir() {
        if(string.IsNullOrEmpty(this.Settings.CheckpointDir))
            throw new InvalidOperationException($"{nameof(this.Settings.CheckpointDir)} is not set.");
    }

    public ValueTask<Guid> SaveAsync(CheckpointType CheckpointType = CheckpointType.Snapshot,CancellationToken ct = default) =>
        this.TakeFullCheckpointAsync(CheckpointType,writeRecoveryIndexMetadata: true,ct);

    // 共通ストアから作った場合は索引の大きさをストアの保存に記録するので、全走査による件数計算と
    // checkpoint フォルダ内へのメタデータ書き込み(BUG-0006 の共有違反が起きる箇所)は行わない。
    private async ValueTask<Guid> TakeFullCheckpointAsync(CheckpointType CheckpointType,bool writeRecoveryIndexMetadata,CancellationToken ct) {
        this.AssertCheckpointDir();
        this.ThrowIfCheckpointInProgress();
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
        } finally {
            this.ReleaseCheckpoint();
        }
    }

    public Guid Save(CheckpointType CheckpointType = CheckpointType.Snapshot) => this.SaveAsync(CheckpointType).GetAwaiter().GetResult();

    public async ValueTask<Guid> SaveLogOnlyAsync(CheckpointType CheckpointType = CheckpointType.Snapshot,CancellationToken ct = default) {
        this.AssertCheckpointDir();
        this.ThrowIfCheckpointInProgress();
        try {
            var recoveryEntryCount=this.GetRecoveryEntryCount();
            var (success,token)=await this.Store.TakeHybridLogCheckpointAsync(CheckpointType,cancellationToken: ct);
            await this.Store.CompleteCheckpointAsync(ct);
            if(success) {
                this.CheckpointFailed=false;
                this.WriteRecoveryIndexMetadata(token,recoveryEntryCount);
                this.MarkUnflushed(token);
                return token;
            }
            throw new InvalidOperationException($"{nameof(this.SaveLogOnly)}Checkpoint failed.");
        } catch {
            this.CheckpointFailed=true;
            throw;
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
        this.AssertCheckpointDir();
        await this.RecoverWithSizeTrackerAsync(null,ct);
        this.ThrowIfRecoveredTailCollidesWithHead();
        this.CheckpointFailed=false;
    }

    public void Load() => this.LoadAsync().GetAwaiter().GetResult();

    public async ValueTask LoadAsync(Guid token,CancellationToken ct = default) {
        this.AssertCheckpointDir();
        await this.RecoverWithSizeTrackerAsync(token,ct);
        this.ThrowIfRecoveredTailCollidesWithHead();
        this.CheckpointFailed=false;
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
        var indexSize = this.Store.IndexSize;
        var log = this.Store.Log;
        var beginAddress = log.BeginAddress;
        var headAddress = log.HeadAddress;
        var readOnlyAddress = log.ReadOnlyAddress;
        var tailAddress = log.TailAddress;
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
            this.Store.HighWaterAllocatedPageCount);
    }

    /// <summary>
    /// 索引のバケットの分布を文字列で返す。索引全体を走査する高コストな操作。
    /// 取得した時点の値であり、並行して書き込んでいる間の整合性は保証しない。
    /// </summary>
    public string DumpIndexDistribution() => this.Store.DumpDistribution();

    public void CompactWithLookup() {
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
