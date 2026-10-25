namespace Seamless.Collections.Serializers;

public readonly struct MemoryPack<T>:IStrategy<T> {
    public static ReadOnlySpan<byte> Serialize(ref T value) => global::MemoryPack.MemoryPackSerializer.Serialize(value);
    public static T Deserialize(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty) return default!;
        return global::MemoryPack.MemoryPackSerializer.Deserialize<T>(bytes)!;
    }
}

public readonly struct MemoryPack:IStrategy{
    public static bool UsesMemoryPackSerializer=>true;
}
