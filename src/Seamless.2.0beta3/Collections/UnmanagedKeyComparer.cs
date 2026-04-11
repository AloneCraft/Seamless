using System.Runtime.InteropServices;

namespace Seamless.Collections;

/// <summary>
/// Garnet 2.0 IKeyComparer implementation for unmanaged key types.
/// Performs typed comparison via MemoryMarshal.Read instead of byte-by-byte comparison.
/// </summary>
public readonly struct UnmanagedKeyComparer<TUnmanagedKey> : IKeyComparer
    where TUnmanagedKey : unmanaged, IEquatable<TUnmanagedKey>
{
    public readonly long GetHashCode64<TKey>(TKey key) where TKey : IKey, allows ref struct
        => MemoryMarshal.Read<TUnmanagedKey>(key.KeyBytes).GetHashCode();

    public readonly bool Equals<TFirstKey, TSecondKey>(TFirstKey k1, TSecondKey k2)
        where TFirstKey : IKey, allows ref struct
        where TSecondKey : IKey, allows ref struct
        => MemoryMarshal.Read<TUnmanagedKey>(k1.KeyBytes).Equals(MemoryMarshal.Read<TUnmanagedKey>(k2.KeyBytes));
}
