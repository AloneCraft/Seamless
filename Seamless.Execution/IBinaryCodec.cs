namespace Seamless.Execution;
/// <summary>A deterministic, versioned encoding. Implementations must account for their own working buffers in the execution budget.</summary>
public interface IBinaryCodec<T>{
    string FormatId{get;}
    int GetEncodedLength(T value);
    void Encode(T value,Span<byte> destination);
    T Decode(ReadOnlySpan<byte> source);
}
public sealed class SerializerCodec<T,TSerializer>:IBinaryCodec<T> where TSerializer:global::Seamless.Collections.Serializers.IStrategy<T>{
    public SerializerCodec(string formatId){
        ArgumentException.ThrowIfNullOrWhiteSpace(formatId);
        this.FormatId=formatId;
    }
    public string FormatId{get;}
    public int GetEncodedLength(T value)=>TSerializer.Serialize(ref value).Length;
    public void Encode(T value,Span<byte> destination){
        var source=TSerializer.Serialize(ref value);
        if(source.Length!=destination.Length){throw new InvalidDataException("The serializer length changed between measurement and encoding.");}
        source.CopyTo(destination);
    }
    public T Decode(ReadOnlySpan<byte> source)=>TSerializer.Deserialize(source);
}
