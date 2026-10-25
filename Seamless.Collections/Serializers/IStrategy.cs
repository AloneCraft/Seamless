namespace Seamless.Collections.Serializers;

public interface IStrategy<T> {
    static abstract ReadOnlySpan<byte> Serialize(ref T value);
    static abstract T Deserialize(ReadOnlySpan<byte> bytes);
}

public interface IStrategy{
    static abstract bool UsesMemoryPackSerializer{get;}
}
