using System.Text.Json;
using Seamless.Tsavorite.Stores;
namespace Seamless.Tsavorite.Collections;

/// <summary>
/// Store ファクトリ、E/// </summary>
public readonly struct Store {
    private readonly long IndexSize, PageSize, SegmentSize, MemorySize;
    private readonly double MutableFraction, StorageLimitFraction;
    private readonly long StorageReserveBytes;
    private readonly string? CheckpointDir;
    private Store(
        long IndexSize,
        long PageSize,
        long SegmentSize,
        long MemorySize,
        double MutableFraction,
        double StorageLimitFraction,
        long StorageReserveBytes,
        string? CheckpointDir
    ) {
        if(double.IsNaN(StorageLimitFraction)||StorageLimitFraction<0||StorageLimitFraction>1) {
            throw new ArgumentOutOfRangeException(nameof(StorageLimitFraction));
        }
        if(StorageReserveBytes<0) {
            throw new ArgumentOutOfRangeException(nameof(StorageReserveBytes));
        }
        this.IndexSize=IndexSize;
        this.PageSize=PageSize;
        this.SegmentSize=SegmentSize;
        this.MemorySize=MemorySize;
        this.MutableFraction=MutableFraction;
        this.StorageLimitFraction=StorageLimitFraction;
        this.StorageReserveBytes=StorageReserveBytes;
        this.CheckpointDir=CheckpointDir;
    }
    /// <summary>構�Eパラメータを指定して Store を生成する、E/summary>
    public static Store Create(
        long IndexSize = Const.IndexSize,
        long PageSize = Const.PageSize,
        long SegmentSize = Const.SegmentSize,
        long MemorySize = Const.LogMemorySize,
        double MutableFraction = 1.0,
        string? CheckpointDir = null,
        double StorageLimitFraction = 1.0,
        long StorageReserveBytes = 0
    ) => new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,StorageLimitFraction,StorageReserveBytes,CheckpointDir);

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
        var fallbackIndexSize = Math.Max(IndexSize,minimumIndexSize);
        if(string.IsNullOrWhiteSpace(CheckpointDir)) {
            return fallbackIndexSize;
        }

        return TryGetRecommendedIndexSize(CheckpointDir,token,fallbackIndexSize,out var indexSize)
            ? indexSize
            : fallbackIndexSize;
    }

    public Store WithRecoveryIndexSize(Guid? token = null,long minimumIndexSize = Const.IndexSize) {
        var resolvedIndexSize = ResolveRecoveryIndexSize(token,minimumIndexSize);
        return resolvedIndexSize==IndexSize
            ? this
            : new(resolvedIndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,StorageLimitFraction,StorageReserveBytes,CheckpointDir);
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

    private const string CheckpointIndexMetadataDirectoryName = ".Tsavorite";
    private const string LatestCheckpointIndexMetadataFileName = "latest.index-size.json";

    private sealed record CheckpointIndexMetadata(Guid Token,long EntryCount,long RecommendedIndexSize);

    private static bool UsesMemoryPackSerializer<T>() => RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    // ── Memory Dictionary ──────────────────────────────────────────
    /// <summary>Dictionary with the default serializer for each type.</summary>
    public DictionaryStore<TKey,TValue> Create<TKey, TValue>() =>
        new(
            IndexSize,
            PageSize,
            SegmentSize,
            MemorySize,
            MutableFraction,
            keyUsesMemoryPackSerializer: UsesMemoryPackSerializer<TKey>(),
            valueUsesMemoryPackSerializer: UsesMemoryPackSerializer<TValue>());

    /// <summary>Managed Key ÁEManaged Value</summary>
    public DictionaryStore<TKey,TValue> Create<TKey, TValue>(
        BlittableStrategy _0,
        BlittableStrategy _1
    )
        where TKey : unmanaged
        where TValue : unmanaged =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,keyUsesMemoryPackSerializer: false,valueUsesMemoryPackSerializer: false);

    public DictionaryStore<TKey,TValue> Create<TKey, TValue>(
        MemoryPackSerializerStrategy _0,
        MemoryPackSerializerStrategy _1
    ) =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,keyUsesMemoryPackSerializer: true,valueUsesMemoryPackSerializer: true);

    public DictionaryStore<TKey,TValue> Create<TKey, TValue>(
        MemoryPackSerializerStrategy _0,
        BlittableStrategy _1
    ) where TValue : unmanaged =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,keyUsesMemoryPackSerializer: true,valueUsesMemoryPackSerializer: false);

    public DictionaryStore<TKey,TValue> Create<TKey, TValue>(
        BlittableStrategy _0,
        MemoryPackSerializerStrategy _1
    ) where TKey : unmanaged =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,keyUsesMemoryPackSerializer: false,valueUsesMemoryPackSerializer: true);

    // ── Storage Dictionary ─────────────────────────────────────────
    /// <summary>Storage Dictionary with the default serializer for each type.</summary>
    public DictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice
    ) =>
        new(
            IndexSize,
            PageSize,
            SegmentSize,
            MemorySize,
            MutableFraction,
            LogDevice,
            ObjectLogDevice,
            CheckpointDir,
            StorageLimitFraction,
            StorageReserveBytes,
            keyUsesMemoryPackSerializer: UsesMemoryPackSerializer<TKey>(),
            valueUsesMemoryPackSerializer: UsesMemoryPackSerializer<TValue>());

    /// <summary>Managed Key ÁEManaged Value�E�ストレージ付き�E�E/summary>
    public DictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        BlittableStrategy _0,
        BlittableStrategy _1
    )
        where TKey : unmanaged
        where TValue : unmanaged =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,StorageLimitFraction,StorageReserveBytes,keyUsesMemoryPackSerializer: false,valueUsesMemoryPackSerializer: false);

    public DictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        MemoryPackSerializerStrategy _0,
        MemoryPackSerializerStrategy _1
    ) =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,StorageLimitFraction,StorageReserveBytes,keyUsesMemoryPackSerializer: true,valueUsesMemoryPackSerializer: true);

    public DictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        MemoryPackSerializerStrategy _0,
        BlittableStrategy _1
    ) where TValue : unmanaged =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,StorageLimitFraction,StorageReserveBytes,keyUsesMemoryPackSerializer: true,valueUsesMemoryPackSerializer: false);

    public DictionaryStore<TKey,TValue> Create<TKey, TValue>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        BlittableStrategy _0,
        MemoryPackSerializerStrategy _1
    ) where TKey : unmanaged =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,StorageLimitFraction,StorageReserveBytes,keyUsesMemoryPackSerializer: false,valueUsesMemoryPackSerializer: true);

    // ── Managed HashSet ────────────────────────────────────────────
    /// <summary>Memory HashSet with the default serializer.</summary>
    public HashSetStore<TKey> Create<TKey>() =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction);

    /// <summary>Memory HashSet (named overload)</summary>
    public HashSetStore<TKey> CreateHashSet<TKey>() =>
        Create<TKey>(new MemoryPackSerializerStrategy());
    /// <summary>Storage HashSet</summary>
    public HashSetStore<TKey> Create<TKey>(
        IDevice LogDevice,
        IDevice ObjectLogDevice
    ) =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,StorageLimitFraction,StorageReserveBytes);
    //public HashSetStore<TKey> CreateHashSet<TKey>(
    //    IDevice LogDevice,
    //    IDevice ObjectLogDevice
    //) =>
    //    this.Create<TKey>(LogDevice,ObjectLogDevice,new MemoryPackSerializerStrategy());

    public HashSetStore<TKey> Create<TKey>(BlittableStrategy _0)
        where TKey : unmanaged =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction);

    public HashSetStore<TKey> Create<TKey>(MemoryPackSerializerStrategy _0) =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction);

    public HashSetStore<TKey> Create<TKey>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        BlittableStrategy _0
    )
        where TKey : unmanaged =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,StorageLimitFraction,StorageReserveBytes);

    public HashSetStore<TKey> Create<TKey>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        MemoryPackSerializerStrategy _0
    ) =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,StorageLimitFraction,StorageReserveBytes);
    /// <summary>Memory Set</summary>
    public SetStore<TKey,TRecord> CreateSet<TKey, TRecord>()
        where TRecord : IRecord<TKey> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction);
    /// <summary>Storage Set</summary>
    public SetStore<TKey,TRecord> CreateSet<TKey, TRecord>(
        IDevice LogDevice,
        IDevice ObjectLogDevice
    )
        where TRecord : IRecord<TKey> =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,StorageLimitFraction,StorageReserveBytes);

    /// <summary>Memory Set using blittable serialization.</summary>
    public SetStore<TKey,TRecord> CreateSet<TKey, TRecord>(BlittableStrategy _0)
        where TRecord : unmanaged, IRecord<TKey>
        where TKey : unmanaged =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction);
    /// <summary>Storage Set using blittable serialization.</summary>
    public SetStore<TKey,TRecord> CreateSet<TKey, TRecord>(
        IDevice LogDevice,
        IDevice ObjectLogDevice,
        BlittableStrategy _0
    )
        where TRecord : unmanaged, IRecord<TKey>
        where TKey : unmanaged =>
        new(IndexSize,PageSize,SegmentSize,MemorySize,MutableFraction,LogDevice,ObjectLogDevice,CheckpointDir,StorageLimitFraction,StorageReserveBytes);

}
