using System.Runtime.CompilerServices;
using Seamless.Stores;

namespace Seamless.Collections;

public static class TempList {
    public static ManagedList<T> Create<T>(
        string path,
        long initialCapacity = 16,
        long initialDataCapacity = 4096,
        ManagedTag _ = default
    ) => new(path,initialCapacity,initialDataCapacity);

    [OverloadResolutionPriority(1)]
    public static UnmanagedList<T> Create<T>(
        string path,
        long initialCapacity = 16,
        UnmanagedTag _ = default
    )
        where T : unmanaged =>
        new(path,initialCapacity);
}