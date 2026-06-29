namespace Seamless.Serialization;

public readonly struct MemoryPackSerializer<T>:ISerializer<T>{
    public static ReadOnlySpan<byte> Serialize(ref T value)=>MemoryPack.MemoryPackSerializer.Serialize(value);
    public static T Deserialize(ReadOnlySpan<byte> bytes){
        if(bytes.IsEmpty) return default!;
        return MemoryPack.MemoryPackSerializer.Deserialize<T>(bytes)!;
    }
}
