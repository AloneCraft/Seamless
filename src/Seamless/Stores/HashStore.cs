using System.Runtime.CompilerServices;
using System.Text.Json;
namespace Seamless.Stores;

/// <summary>Managed/Unmanaged オーバーロード解決用タグ。</summary>
public readonly struct ManagedTag;
/// <summary>Managed/Unmanaged オーバーロード解決用タグ。</summary>
public readonly struct UnmanagedTag;
/// <summary>
/// Store ファクトリ。
/// Create オーバーロードは default 引数で ManagedTag/UnmanagedTag を受け取ることで、Managed/Unmanaged のオーバーロード解決を行う。
/// </summary>
public readonly struct HashStore {
    private readonly long IndexSize, PageSize, SegmentSize, MemorySize;
    private readonly double MutableFraction;
    private readonly string? CheckpointDir;
    private HashStore(
        long IndexSize,
        long PageSize,
        long SegmentSize,
        long MemorySize,
        double MutableFraction,
        string? CheckpointDir
    ) {
        this.IndexSize=IndexSize;
        this.PageSize=PageSize;
        this.SegmentSize=SegmentSize;
        this.MemorySize=MemorySize;
        this.MutableFraction=MutableFraction;
        this.CheckpointDir=CheckpointDir;
    }
    /// <summary>構成パラメータを指定して HashStore を生成する。</summary>
    public static HashStore Create(
        long IndexSize = Const.IndexSize,
        long PageSize = Const.PageSize,
        long SegmentSize = Const.SegmentSize,
        long MemorySize = Const.LogMemorySize,
        double MutableFraction = 1.0,
        string? CheckpointDir = null
    ) => new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,CheckpointDir);

    public static long GetIndexSizeForCapacity(long requiredCapacity,long minimumIndexSize = Const.IndexSize) {
        if(requiredCapacity<0)
            throw new ArgumentOutOfRangeException(nameof(requiredCapacity));
        if(minimumIndexSize<=0)
            throw new ArgumentOutOfRangeException(nameof(minimumIndexSize));

        var targetSize = Math.Max(minimumIndexSize,Math.Max(1L,requiredCapacity));
        long size = 1;
        while(size<targetSize) {
            size<<=1;
        }

        return size;
    }

    public long ResolveRecoveryIndexSize(Guid? token = null,long minimumIndexSize = Const.IndexSize) {
        var fallbackIndexSize = Math.Max(this.IndexSize,minimumIndexSize);
        if(string.IsNullOrWhiteSpace(this.CheckpointDir)) {
            return fallbackIndexSize;
        }

        return TryGetRecommendedIndexSize(this.CheckpointDir,token,fallbackIndexSize,out var indexSize)
            ? indexSize
            : fallbackIndexSize;
    }

    public HashStore WithRecoveryIndexSize(Guid? token = null,long minimumIndexSize = Const.IndexSize) {
        var resolvedIndexSize = this.ResolveRecoveryIndexSize(token,minimumIndexSize);
        return resolvedIndexSize==this.IndexSize
            ? this
            : new(resolvedIndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,this.CheckpointDir);
    }

    internal static void WriteRecoveryIndexMetadata(string checkpointDir,Guid token,long entryCount,long minimumIndexSize = Const.IndexSize) {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointDir);

        var metadata = new CheckpointIndexMetadata(
            token,
            entryCount,
            GetIndexSizeForCapacity(entryCount,minimumIndexSize));

        var metadataDir = Path.Combine(checkpointDir,CheckpointIndexMetadataDirectoryName);
        Directory.CreateDirectory(metadataDir);

        var json = JsonSerializer.Serialize(metadata);
        File.WriteAllText(GetCheckpointIndexMetadataPath(checkpointDir,token),json);
        File.WriteAllText(Path.Combine(metadataDir,LatestCheckpointIndexMetadataFileName),json);
    }

    private static bool TryGetRecommendedIndexSize(string checkpointDir,Guid? token,long minimumIndexSize,out long indexSize) {
        indexSize=minimumIndexSize;
        if(string.IsNullOrWhiteSpace(checkpointDir)) {
            return false;
        }

        foreach(var path in EnumerateCheckpointIndexMetadataPaths(checkpointDir,token)) {
            if(!File.Exists(path)) {
                continue;
            }

            try {
                var metadata = JsonSerializer.Deserialize<CheckpointIndexMetadata>(File.ReadAllText(path));
                if(metadata is null) {
                    continue;
                }

                indexSize=Math.Max(minimumIndexSize,GetIndexSizeForCapacity(metadata.EntryCount,minimumIndexSize));
                return true;
            }
            catch {
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateCheckpointIndexMetadataPaths(string checkpointDir,Guid? token) {
        if(token is Guid checkpointToken) {
            yield return GetCheckpointIndexMetadataPath(checkpointDir,checkpointToken);
        }

        yield return Path.Combine(checkpointDir,CheckpointIndexMetadataDirectoryName,LatestCheckpointIndexMetadataFileName);
    }

    private static string GetCheckpointIndexMetadataPath(string checkpointDir,Guid token) {
        return Path.Combine(checkpointDir,CheckpointIndexMetadataDirectoryName,$"{token:N}.index-size.json");
    }

    private const string CheckpointIndexMetadataDirectoryName = ".seamless";
    private const string LatestCheckpointIndexMetadataFileName = "latest.index-size.json";

    private sealed record CheckpointIndexMetadata(Guid Token,long EntryCount,long RecommendedIndexSize);

    // ── Memory Dictionary ──────────────────────────────────────────
    /// <summary>Managed Key × Managed Value</summary>
    public ManagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        ManagedTag _0 = default,
        ManagedTag _1 = default
    )
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Managed Key × Unmanaged Value</summary>
    [OverloadResolutionPriority(1)]
    public ManagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        ManagedTag _0 = default,
        UnmanagedTag _1 = default
    )
        where TKey : IEquatable<TKey>
        where TValue : unmanaged, IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Unmanaged Key × Managed Value</summary>
    [OverloadResolutionPriority(1)]
    public ManagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        UnmanagedTag _0 = default,
        ManagedTag _1 = default
    )
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Unmanaged Key × Unmanaged Value</summary>
    [OverloadResolutionPriority(2)]
    public UnmanagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        UnmanagedTag _0 = default,
        UnmanagedTag _1 = default
    )
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged, IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);

    // ── Storage Dictionary ─────────────────────────────────────────
    /// <summary>Managed Key × Managed Value（ストレージ付き）</summary>
    public ManagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        ManagedTag _0 = default,
        ManagedTag _1 = default
    )
        where TKey : IEquatable<TKey>
        where TValue : IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
    /// <summary>Managed Key × Unmanaged Value（ストレージ付き）</summary>
    [OverloadResolutionPriority(1)]
    public ManagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        ManagedTag _0 = default,
        UnmanagedTag _1 = default
    )
        where TKey : IEquatable<TKey>
        where TValue : unmanaged, IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
    /// <summary>Unmanaged Key × Managed Value（ストレージ付き）</summary>
    [OverloadResolutionPriority(1)]
    public ManagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        UnmanagedTag _0 = default,
        ManagedTag _1 = default
    )
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
    /// <summary>Unmanaged Key × Unmanaged Value（ストレージ付き）</summary>
    [OverloadResolutionPriority(2)]
    public UnmanagedDictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        UnmanagedTag _0 = default,
        UnmanagedTag _1 = default
    )
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged, IEquatable<TValue> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);

    // ── Managed HashSet ────────────────────────────────────────────
    /// <summary>Memory ManagedHashSet</summary>
    public ManagedHashSetStore<TKey> Create<TKey>(ManagedTag _0 = default)
        where TKey : IEquatable<TKey> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Storage ManagedHashSet</summary>
    public ManagedHashSetStore<TKey> Create<TKey>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        ManagedTag _0 = default
    )
        where TKey : IEquatable<TKey> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);

    // ── Unmanaged HashSet ──────────────────────────────────────────
    /// <summary>Memory UnmanagedHashSet</summary>
    [OverloadResolutionPriority(1)]
    public UnmanagedHashSetStore<TKey> Create<TKey>(UnmanagedTag _0 = default)
        where TKey : unmanaged, IEquatable<TKey> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction);
    /// <summary>Storage UnmanagedHashSet</summary>
    [OverloadResolutionPriority(1)]
    public UnmanagedHashSetStore<TKey> Create<TKey>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        UnmanagedTag _0 = default
    )
        where TKey : unmanaged, IEquatable<TKey> =>
        new(this.IndexSize,this.PageSize,this.SegmentSize,this.MemorySize,this.MutableFraction,LogDevice,ObjectLogDevice,this.CheckpointDir);
}
