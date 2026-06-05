using Seamless.Stores;

namespace Seamless.Collections;

public static class ListStore {
    public static List<T> Create<T>(
        string path,
        long initialCapacity = 16,
        long initialDataCapacity = 4096,
        MemoryPackSerializerStrategy _ = default
    ) => new(path,initialCapacity,initialDataCapacity);

    [OverloadResolutionPriority(1)]
    public static List<T,BlittableSerializer<T>> Create<T>(
        string path,
        long initialCapacity = 16,
        BlittableStrategy _ = default
    )
        where T : unmanaged =>
        new(path,initialCapacity);
}
