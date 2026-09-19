namespace Seamless.Database.SqlServer;

public readonly record struct SqlServerImportSizing(
    long MaximumRowBytes,
    long MaximumTableRows,
    long IndexSize,
    long PageSize,
    long SegmentSize,
    long MemorySize);
