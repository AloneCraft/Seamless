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
        : CreateBlittableList<T>(path,initialCapacity);

    [OverloadResolutionPriority(1)]
    public BlittableList<T> Create<T>(
        string path,
        long initialCapacity = 16,
        BlittableStrategy _ = default
    )
        where T : unmanaged =>
        new(path,initialCapacity);

    public SerializedList<T> Create<T>(
        string path,
        long initialCapacity = 16,
        long initialDataCapacity = 4096,
        MemoryPackSerializerStrategy _ = default
    ) =>
        new(path,initialCapacity,initialDataCapacity);

    public PersistedList<T> CreateTemp<T>(
        long initialCapacity = 16,
        long initialDataCapacity = 4096
    ) => Create<T>(CreateTemporaryPath(),initialCapacity,initialDataCapacity);

    [OverloadResolutionPriority(1)]
    public BlittableList<T> CreateTemp<T>(
        long initialCapacity = 16,
        BlittableStrategy _ = default
    )
        where T : unmanaged =>
        Create<T>(CreateTemporaryPath(),initialCapacity,_);

    public AppendOnlyList<T> CreateAppendOnly<T>(
        string path,
        long initialDataCapacity = 4096
    ) => new(path,initialDataCapacity);

    public AppendOnlyList<T> CreateTempAppendOnly<T>(long initialDataCapacity = 4096) =>
        CreateAppendOnly<T>(CreateTemporaryPath(),initialDataCapacity);

    private static PersistedList<T> CreateBlittableList<T>(string path,long initialCapacity) =>
        (PersistedList<T>)Activator.CreateInstance(
            typeof(BlittableList<>).MakeGenericType(typeof(T)),
            path,
            initialCapacity)!;

    private string CreateTemporaryPath() {
        var root = string.IsNullOrWhiteSpace(temporaryRoot)
            ? Path.Combine(Path.GetTempPath(),"Seamless","Persisted")
            : temporaryRoot!;
        var directory = Path.Combine(root,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory,"items.dat");
    }
}