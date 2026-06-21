using Persistent.Collections;
using Tsavorite.Stores;

namespace Persistent;

public readonly struct Store {
    private readonly string? temporaryRoot;

    private Store(string? temporaryRoot) {
        this.temporaryRoot=temporaryRoot;
    }

    public static Store Create(string? temporaryRoot = null) => new(temporaryRoot);

    public Persistent.Collections.List<T> Create<T>(
        string path,
        long initialCapacity = 16,
        long initialDataCapacity = 4096
    ) => RuntimeHelpers.IsReferenceOrContainsReferences<T>()
        ? new Persistent.Collections.List<T,Tsavorite.Collections.MemoryPackSerializer<T>>(path,initialCapacity,initialDataCapacity)
        : CreateFixedList<T>(path,initialCapacity);

    [OverloadResolutionPriority(1)]
    public Persistent.Collections.List<T> Create<T>(
        string path,
        long initialCapacity = 16,
        BlittableStrategy _ = default
    )
        where T : unmanaged =>
        new Persistent.Collections.FixedList<T>(path,initialCapacity);

    public Persistent.Collections.List<T> Create<T>(
        string path,
        long initialCapacity = 16,
        long initialDataCapacity = 4096,
        MemoryPackSerializerStrategy _ = default
    ) =>
        new Persistent.Collections.List<T,Tsavorite.Collections.MemoryPackSerializer<T>>(path,initialCapacity,initialDataCapacity);

    public Persistent.Collections.List<T> CreateTemp<T>(
        long initialCapacity = 16,
        long initialDataCapacity = 4096
    ) => this.Create<T>(this.CreateTemporaryPath(),initialCapacity,initialDataCapacity);

    [OverloadResolutionPriority(1)]
    public Persistent.Collections.List<T> CreateTemp<T>(
        long initialCapacity = 16,
        BlittableStrategy _ = default
    )
        where T : unmanaged =>
        this.Create<T>(this.CreateTemporaryPath(),initialCapacity,_);


    private static Persistent.Collections.List<T> CreateFixedList<T>(string path,long initialCapacity) =>
        (Persistent.Collections.List<T>)Activator.CreateInstance(
            typeof(Persistent.Collections.FixedList<>).MakeGenericType(typeof(T)),
            path,
            initialCapacity)!;
    private string CreateTemporaryPath() {
        var root = string.IsNullOrWhiteSpace(this.temporaryRoot)
            ? Path.Combine(Path.GetTempPath(),"Persistent","Lists")
            : this.temporaryRoot!;
        var directory = Path.Combine(root,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory,"items.dat");
    }
}