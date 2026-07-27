namespace Seamless.Collections;

public interface ISerializer<T> {
    static abstract ReadOnlySpan<byte> Serialize(ref T value);
    static abstract T Deserialize(ReadOnlySpan<byte> bytes);
}
