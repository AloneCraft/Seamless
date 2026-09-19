namespace Seamless.Database;

/// <summary>DBが共有管理するメモリと一時ストレージの計測値。</summary>
public readonly record struct DatabaseResourceUsage(
    long MemoryLimitBytes,
    long TemporaryStorageLimitBytes,
    long MemoryBytes,
    long TemporaryStorageBytes,
    long PeakMemoryBytes,
    long PeakTemporaryStorageBytes);
