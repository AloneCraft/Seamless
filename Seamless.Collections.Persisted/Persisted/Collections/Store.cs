namespace Seamless.Persisted.Collections;

public readonly struct Store {
    private readonly string? temporaryRoot;

    private Store(string? temporaryRoot) {
        this.temporaryRoot=temporaryRoot;
    }

    public static Store Create(string? temporaryRoot = null) => new(temporaryRoot);

    public PersistedList<T> Create<T>(
        string path,
        long initialCapacity = 16,
        long initialDataCapacity = 4096
    ) => RuntimeHelpers.IsReferenceOrContainsReferences<T>()
        ? new SerializedList<T,MemoryPackSerializer<T>>(path,initialCapacity,initialDataCapacity)
        : CreateBlittableList<T>(path,initialCapacity,null);

    public PersistedList<T> Create<T>(
        string path,
        StorageTuning tuning,
        long initialCapacity = 16,
        long initialDataCapacity = 4096
    ) => RuntimeHelpers.IsReferenceOrContainsReferences<T>()
        ? new SerializedList<T,MemoryPackSerializer<T>>(path,initialCapacity,initialDataCapacity,tuning)
        : CreateBlittableList<T>(path,initialCapacity,tuning);

    [OverloadResolutionPriority(1)]
    public BlittableList<T> Create<T>(
        string path,
        long initialCapacity = 16,
        BlittableStrategy _ = default
    )
        where T : unmanaged =>
        new(path,initialCapacity);

    public BlittableList<T> Create<T>(
        string path,
        StorageTuning tuning,
        long initialCapacity = 16,
        BlittableStrategy _ = default
    )
        where T : unmanaged =>
        new(path,initialCapacity,tuning);

    public SerializedList<T> Create<T>(
        string path,
        long initialCapacity = 16,
        long initialDataCapacity = 4096,
        MemoryPackSerializerStrategy _ = default
    ) =>
        new(path,initialCapacity,initialDataCapacity);

    public SerializedList<T> Create<T>(
        string path,
        StorageTuning tuning,
        long initialCapacity = 16,
        long initialDataCapacity = 4096,
        MemoryPackSerializerStrategy _ = default
    ) =>
        new(path,initialCapacity,initialDataCapacity,tuning);

    public PersistedList<T> CreateTemp<T>(
        long initialCapacity = 16,
        long initialDataCapacity = 4096
    ) => this.Create<T>(this.CreateTemporaryPath(),initialCapacity,initialDataCapacity);

    public PersistedList<T> CreateTemp<T>(
        StorageTuning tuning,
        long initialCapacity = 16,
        long initialDataCapacity = 4096
    ) => this.Create<T>(this.CreateTemporaryPath(),tuning,initialCapacity,initialDataCapacity);

    [OverloadResolutionPriority(1)]
    public BlittableList<T> CreateTemp<T>(
        long initialCapacity = 16,
        BlittableStrategy _ = default
    )
        where T : unmanaged =>
        this.Create<T>(this.CreateTemporaryPath(),initialCapacity,_);

    public BlittableList<T> CreateTemp<T>(
        StorageTuning tuning,
        long initialCapacity = 16,
        BlittableStrategy _ = default
    )
        where T : unmanaged =>
        this.Create<T>(this.CreateTemporaryPath(),tuning,initialCapacity,_);

    public AppendOnlyList<T> CreateAppendOnly<T>(
        string path,
        long initialDataCapacity = 4096,
        StorageTuning? tuning = null
    ) => new(path,initialDataCapacity,tuning);

    public AppendOnlyList<T> CreateTempAppendOnly<T>(long initialDataCapacity = 4096,StorageTuning? tuning = null) =>
        this.CreateAppendOnly<T>(this.CreateTemporaryPath(),initialDataCapacity,tuning);

    private static PersistedList<T> CreateBlittableList<T>(string path,long initialCapacity,StorageTuning? tuning) =>
        (PersistedList<T>)Activator.CreateInstance(
            typeof(BlittableList<>).MakeGenericType(typeof(T)),
            path,
            initialCapacity,
            tuning)!;

    private string CreateTemporaryPath() {
        var root = string.IsNullOrWhiteSpace(this.temporaryRoot)
            ? Path.Combine(Path.GetTempPath(),"Seamless","Persisted")
            : this.temporaryRoot!;
        var directory = Path.Combine(root,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory,"items.dat");
    }
}
