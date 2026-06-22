namespace Seamless.Serialization;

public readonly struct RawByteArraySerializer:ISerializer<byte[]> {
    public static ReadOnlySpan<byte> Serialize(ref byte[] value) => value;
    public static byte[] Deserialize(ReadOnlySpan<byte> bytes) => bytes.ToArray();
}
