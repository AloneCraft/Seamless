namespace Seamless.Database;

/// <summary>Table 実体の運用形態。</summary>
public enum DatabaseStorageMode {
    /// <summary>メモリ範囲内で運用する。ディスクI/Oを行わない。</summary>
    Memory,
    /// <summary>ストレージデバイスへ退避しながら運用する。</summary>
    Storage,
}

/// <summary>コンテナ作成オプション。メモリ範囲内運用とストレージ利用をパラメータで切り替える。</summary>
public sealed record DatabaseContainerOptions {
    public DatabaseStorageMode StorageMode { get; init; } = DatabaseStorageMode.Memory;
    /// <summary><see cref="DatabaseStorageMode.Storage"/> でテーブルログを配置するルートディレクトリ。未指定ならカレントディレクトリ配下のデータベース名。</summary>
    public string? StorageDirectory { get; init; }
    public long IndexSize { get; init; } = Const.IndexSize;
    public long PageSize { get; init; } = Const.PageSize;
    public long SegmentSize { get; init; } = Const.SegmentSize;
    public long MemorySize { get; init; } = Const.LogMemorySize;

    /// <summary>メモリ範囲内運用の既定オプション。</summary>
    public static DatabaseContainerOptions InMemory { get; } = new();

    /// <summary>ストレージ利用の既定オプション。</summary>
    public static DatabaseContainerOptions OnStorage(string storageDirectory) => new() {
        StorageMode=DatabaseStorageMode.Storage,
        StorageDirectory=storageDirectory,
    };
}
