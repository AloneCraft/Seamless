namespace Seamless.Database;

/// <summary>DBが共有管理するメモリとディスクの計測値。TemporaryStorageBytes は永続ストアの課金も含む。</summary>
public readonly record struct DatabaseResourceUsage(
    long MemoryLimitBytes,
    long TemporaryStorageLimitBytes,
    long MemoryBytes,
    long TemporaryStorageBytes,
    long PeakMemoryBytes,
    long PeakTemporaryStorageBytes);
