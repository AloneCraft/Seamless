using MemoryPack;

namespace Seamless.Collections;

public readonly struct ManagedSerializer<T> : ISerializer<T> {
    public static ReadOnlySpan<byte> Serialize(ref T value) => MemoryPackSerializer.Serialize(value);
    public static T Deserialize(ReadOnlySpan<byte> bytes) {
        if (bytes.IsEmpty) return default!;
        return MemoryPackSerializer.Deserialize<T>(bytes)!;
    }
}
