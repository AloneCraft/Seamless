using Seamless.Devices;

namespace Seamless.Stores;

/// <summary>
/// Tsavorite ストアを管理する抽象基底クラス。
/// DictionaryStore および HashSetStore の共通実装を提供する。
/// </summary>
public abstract partial class CollectionStore<
    TKey,
    TValue,
    TStoreFunctions,
    TAllocator
>:CollectionStore, IDisposable {
    public long LogMemorySize { get; }
    public double MutableFraction { get; }
    internal OverflowMutableStrategyKind OverflowMutableStrategyKind { get; }
    private bool Disposed;
    private bool UsesStorageDevice => this.Settings.LogDevice is not MemoryOnlyDevice||this.Settings.ObjectLogDevice is not MemoryOnlyDevice;

    private protected CollectionStore(
        TStoreFunctions StoreFunctions,
        Func<AllocatorSettings,TStoreFunctions,TAllocator> allocatorFactory,
        long IndexSize,long PageSize,long SegmentSize,long MemorySize,double MutableFraction)
       : this(StoreFunctions,allocatorFactory,IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,new MemoryOnlyDevice(SegmentSize*2),new MemoryOnlyDevice(Const.MinObjectLogCapacity),null,OverflowMutableStrategyKind.Memory) {
    }

    public void AssertCheckpointDir() {
        if(string.IsNullOrEmpty(this.Settings.CheckpointDir))
            throw new InvalidOperationException($"{nameof(this.Settings.CheckpointDir)} is not set.");
    }

    /// <summary>
    /// データベース全体の完全なバックアップ（Full Checkpoint）
    /// </summary>
    public async ValueTask<Guid> SaveAsync(CheckpointType CheckpointType = CheckpointType.Snapshot,CancellationToken ct = default) {
        this.AssertCheckpointDir();
        var (success,token)=await this.Store.TakeFullCheckpointAsync(CheckpointType,ct);
        await this.Store.CompleteCheckpointAsync(ct);
        if(success)
            return token;
        throw new InvalidOperationException($"{nameof(this.Save)}Checkpoint failed.");
    }

    /// <summary>
    /// データベース全体の完全なバックアップ（Full Checkpoint）
    /// </summary>
    public Guid Save(CheckpointType CheckpointType = CheckpointType.Snapshot) => this.SaveAsync(CheckpointType).GetAwaiter().GetResult();

    /// <summary>
    /// データ（ログ）部分のみのバックアップ（Incremental/Log Checkpoint）
    /// インデックスは復元せず、ログを先頭から再生してインデックスを再構築するリカバリー。
    /// インデックスチェックポイントが存在しない場合や、インデックスチェックポイントが破損している場合の復旧に使用する。
    /// </summary>
    public async ValueTask<Guid> SaveLogOnlyAsync(CheckpointType CheckpointType = CheckpointType.Snapshot,CancellationToken ct = default) {
        this.AssertCheckpointDir();
        var (success,token)=await this.Store.TakeHybridLogCheckpointAsync(CheckpointType,cancellationToken: ct);
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
    public Guid SaveLogOnly(CheckpointType CheckpointType = CheckpointType.Snapshot) => this.SaveLogOnlyAsync(CheckpointType).GetAwaiter().GetResult();

    /// <summary>
    /// 最新のデータに復元
    /// </summary>
    public async ValueTask LoadAsync(CancellationToken ct = default) {
        this.AssertCheckpointDir();
        await this.Store.RecoverAsync(cancellationToken: ct);
    }

    /// <summary>
    /// 最新のデータに復元
    /// </summary>
    public void Load() => this.LoadAsync().GetAwaiter().GetResult();

    /// <summary>
    /// 指定した過去地点(token)へのピンポイント復元
    /// </summary>
    public async ValueTask LoadAsync(Guid token,CancellationToken ct = default) {
        this.AssertCheckpointDir();
        await this.Store.RecoverAsync(Guid.Empty,token,cancellationToken: ct);
    }

    /// <summary>
    /// 指定した過去地点(token)へのピンポイント復元
    /// </summary>
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

    public void Dispose() {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if(this.Disposed) return;
        this.Disposed=true;
        if(disposing) {
            // Tsavorite の LocalStorageDevice は pending read を残したまま Dispose から戻ることがある。
            if(this.UsesStorageDevice) {
                this.Store.Log.FlushAndEvict(wait: true);
            }
        }
        this.Store.Dispose();
        this.Settings.Dispose();
    }

    ~CollectionStore() => this.Dispose(false);
}