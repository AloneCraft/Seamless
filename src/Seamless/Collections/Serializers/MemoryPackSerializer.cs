namespace Seamless.Collections;

public readonly struct MemoryPackSerializer<T>:ISerializer<T>{
    public static ReadOnlySpan<byte> Serialize(ref T value)=>global::MemoryPack.MemoryPackSerializer.Serialize(value);
    public static T Deserialize(ReadOnlySpan<byte> bytes){
        if(bytes.IsEmpty) return default!;
        return global::MemoryPack.MemoryPackSerializer.Deserialize<T>(bytes)!;
    }
}
