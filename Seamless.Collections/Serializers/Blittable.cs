namespace Seamless.Collections.Serializers;

public readonly struct Blittable<T>:IStrategy<T>
    where T : unmanaged {
    public static ReadOnlySpan<byte> Serialize(ref T value) =>
        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T,byte>(ref value),Unsafe.SizeOf<T>());

    public static T Deserialize(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty)
            return default;
        return MemoryMarshal.Read<T>(bytes);
    }
}

public readonly struct Blittable:IStrategy{
    public static bool UsesMemoryPackSerializer=>false;
}
