namespace Seamless.Database;

/// <summary>Table 実体の運用形態。</summary>
public enum DatabaseStorageMode {
    /// <summary>メモリ範囲内で運用する。ディスクI/Oを行わない。</summary>
    Memory,
    /// <summary>ストレージデバイスへ退避しながら運用する。</summary>
    Storage,
}
public enum DatabaseTransactionMode{Legacy,SharedV2}

/// <summary>コンテナ作成オプション。メモリ範囲内運用とストレージ利用をパラメータで切り替える。</summary>
public sealed record DatabaseContainerOptions {
    /// <summary>SharedV2は新規保存領域への明示導入。旧DBを自動変換しない。</summary>
    public DatabaseTransactionMode Transactions{get;init;}=DatabaseTransactionMode.Legacy;
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
    /// <summary>DBが管理する変更集合、シリアライズ、一時索引の共有メモリ上限。</summary>
    public long ManagedMemoryLimitBytes { get; init; } = 256L<<20;
    /// <summary>DBが管理する変更集合と一時索引の共有一時ストレージ上限。</summary>
    public long TemporaryStorageLimitBytes { get; init; } = 16L<<30;
    /// <summary>一時資源の配置先。未指定ならDBの保存先またはOS一時領域を使う。</summary>
    public string? TemporaryDirectory { get; init; }
    /// <summary>トランザクションのインメモリ変更量の上限。超過時はTsavorite一時ストアへ退避する。</summary>
    public long TransactionMemoryLimit { get; init; } = 64L<<20;

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
