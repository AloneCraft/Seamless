using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using MemoryPack;

namespace Seamless.Collections;


public readonly struct UnmanagedSerializer<T> : ISerializer<T> where T : unmanaged {
    public static ReadOnlySpan<byte> Serialize(ref T value) =>
        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref value), Unsafe.SizeOf<T>());
    public static T Deserialize(ReadOnlySpan<byte> bytes) {
        if (bytes.IsEmpty) return default;
        return MemoryMarshal.Read<T>(bytes);
    }
}
