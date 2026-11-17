using Seamless.Collections.Tsavorite;
using Tsavorite.core;
namespace Seamless.Database;

/// <summary>Table 実体の運用形態。</summary>
public enum DatabaseStorageMode {
    /// <summary>メモリ範囲内で運用する。ディスクI/Oを行わない。</summary>
    Memory,
    /// <summary>ストレージデバイスへ退避しながら運用する。</summary>
    Storage,
}
public enum DatabaseRecordSerialization{Default,CanonicalV1,RecordGraphV1}

/// <summary>コンテナ作成オプション。メモリ範囲内運用とストレージ利用をパラメータで切り替える。</summary>
public sealed record DatabaseContainerOptions {
    [global::System.Text.Json.Serialization.JsonIgnore]
    internal bool PreparedTransactionWal{get;init;}
    /// <summary>新規の保存用SharedV2で、処理済み保存までWALの付随情報を保持する。</summary>
    public string? TransactionMetadataConsumer{get;init;}
    public global::Seamless.Transactions.TransactionConcurrencyMode TransactionConcurrency{get;init;}=global::Seamless.Transactions.TransactionConcurrencyMode.Exclusive;
    /// <summary>新規の版付き保存プロファイルを明示導入する。Exclusiveで再オープンするときも指定する。</summary>
    public global::Seamless.Transactions.TransactionHistoryOptions? TransactionHistory{get;init;}
    /// <summary>新規の型付き SharedV2 領域でだけ選択する。既存領域の serializer は変更しない。</summary>
    public DatabaseRecordSerialization RecordSerialization{get;init;}=DatabaseRecordSerialization.Default;
    public GatewayDatabaseProfile? GatewayProfile{get;init;}
    [global::System.Text.Json.Serialization.JsonIgnore]
    internal GatewayVersionedBinding? GatewayVersionedBinding{get;init;}
    [global::System.Text.Json.Serialization.JsonIgnore]
    internal GatewayVersionedSeed? GatewayVersionedSeed{get;init;}
    [global::System.Text.Json.Serialization.JsonIgnore]
    public global::Seamless.Execution.ResourceBudget? ParentBudget{get;init;}
    /// <summary>SharedV2の業務Storeの既存profile/growth設定。未指定は従来の既定。私有query領域とは別。</summary>
    public global::Seamless.Execution.StoreOptions? SharedStoreOptions{get;init;}
    public DatabaseStorageMode StorageMode { get; init; } = DatabaseStorageMode.Memory;
    /// <summary><see cref="DatabaseStorageMode.Storage"/> でテーブルログを配置するルートディレクトリ。未指定ならカレントディレクトリ配下のデータベース名。</summary>
    public string? StorageDirectory { get; init; }
    public long IndexSize { get; init; } = Const.IndexSize;
    public long PageSize { get; init; } = Const.PageSize;
    public long SegmentSize { get; init; } = Const.SegmentSize;
    public long MemorySize { get; init; } = Const.LogMemorySize;
    /// <summary>Optional per-relation Tsavorite sizes keyed by the full database object name.</summary>
    public IReadOnlyDictionary<string,DatabaseTableStorageOptions> TableStorage { get; init; }=
        new Dictionary<string,DatabaseTableStorageOptions>(StringComparer.Ordinal);
    /// <summary>DBが管理するTsavoriteの常駐配列、変更集合、シリアライズ、一時索引の共有メモリ上限。</summary>
    public long ManagedMemoryLimitBytes { get; init; } = 256L<<20;
    /// <summary>DBが管理する変更集合と一時索引の共有一時ストレージ上限。</summary>
    public long TemporaryStorageLimitBytes { get; init; } = 16L<<30;
    /// <summary>一時資源の配置先。未指定ならDBの保存先またはOS一時領域を使う。</summary>
    public string? TemporaryDirectory { get; init; }
    /// <summary>トランザクションのインメモリ変更量の上限。超過時はTsavorite一時ストアへ退避する。</summary>
    public long TransactionMemoryLimit { get; init; } = 64L<<20;
    /// <summary>一つのqueryが使うworker数の上限。取引同士の並行方式とは独立し、host共通CPU枠にも従う。</summary>
    public int MaximumQueryWorkers{get;init;}=Math.Max(1,Environment.ProcessorCount);

    /// <summary>メモリ範囲内運用の既定オプション。</summary>
    public static DatabaseContainerOptions InMemory { get; } = new();

    /// <summary>ストレージ利用の既定オプション。</summary>
    public static DatabaseContainerOptions OnStorage(string storageDirectory) => new() {
        StorageMode=DatabaseStorageMode.Storage,
        StorageDirectory=storageDirectory,
    };
}

/// <summary>Storage sizing for one table or materialized view.</summary>
public sealed record DatabaseTableStorageOptions {
    public long IndexSize { get; init; }
    public long PageSize { get; init; }
    public long SegmentSize { get; init; }
    public long MemorySize { get; init; }
}
