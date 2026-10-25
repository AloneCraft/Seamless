namespace Seamless.Collections.Serializers;

public readonly struct RawByteArray:IStrategy<byte[]> {
    public static ReadOnlySpan<byte> Serialize(ref byte[] value) => value;
    public static byte[] Deserialize(ReadOnlySpan<byte> bytes) => bytes.ToArray();
}
