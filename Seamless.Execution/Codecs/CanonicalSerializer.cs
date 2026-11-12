namespace Seamless.Execution.Codecs;

/// <summary>型ごとの loader allocator に閉じた codec。共有の型登録表へ生成型を登録しない。</summary>
public readonly struct CanonicalSerializer<T>:global::Seamless.Collections.Serializers.IStrategy<T>{
    private static readonly IAdapter<T> Codec=new CodecRegistry().Get<T>();
    public static ReadOnlySpan<byte> Serialize(ref T value){var bytes=new byte[Codec.GetEncodedLength(value)];Codec.Encode(value,bytes);return bytes;}
    public static T Deserialize(ReadOnlySpan<byte> bytes)=>Codec.Decode(bytes);
}
