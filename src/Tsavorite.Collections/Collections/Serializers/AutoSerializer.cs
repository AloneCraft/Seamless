namespace Tsavorite.Collections;

public readonly struct AutoSerializer<T>:ISerializer<T> {
    public static ReadOnlySpan<byte> Serialize(ref T value) {
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            return MemoryPack.MemoryPackSerializer.Serialize(value);
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T,byte>(ref value),Unsafe.SizeOf<T>());
    }

    public static T Deserialize(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty)
            return default!;
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            return MemoryPack.MemoryPackSerializer.Deserialize<T>(bytes)!;
        if(bytes.Length<Unsafe.SizeOf<T>())
            throw new ArgumentException("The serialized value is shorter than the target type size.",nameof(bytes));
        return Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(bytes));
    }
}
