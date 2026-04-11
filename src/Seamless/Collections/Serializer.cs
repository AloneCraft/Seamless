using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using MemoryPack;

namespace Seamless.Collections;

public interface ISerializer<T> {
    static abstract ReadOnlySpan<byte> Serialize(ref T value);
    static abstract T Deserialize(ReadOnlySpan<byte> bytes);
}

public readonly struct ManagedSerializer<T> : ISerializer<T> {
    public static ReadOnlySpan<byte> Serialize(ref T value) => MemoryPackSerializer.Serialize(value);
    public static T Deserialize(ReadOnlySpan<byte> bytes) {
        if (bytes.IsEmpty) return default!;
        return MemoryPackSerializer.Deserialize<T>(bytes)!;
    }
}

public readonly struct UnmanagedSerializer<T> : ISerializer<T> where T : unmanaged {
    public static ReadOnlySpan<byte> Serialize(ref T value) =>
        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref value), Unsafe.SizeOf<T>());
    public static T Deserialize(ReadOnlySpan<byte> bytes) {
        if (bytes.IsEmpty) return default;
        return MemoryMarshal.Read<T>(bytes);
    }
}
