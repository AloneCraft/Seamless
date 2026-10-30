namespace Seamless.Execution.Codecs;

public readonly struct Serializer<T,TSerializer>:IStrategy<Serializer<T,TSerializer>,T> where TSerializer:struct,global::Seamless.Collections.Serializers.IStrategy<T>{
    private readonly string format;
    public Serializer(string format){ArgumentException.ThrowIfNullOrWhiteSpace(format);this.format=format;}
    public static string FormatId(in Serializer<T,TSerializer> self)=>self.format;
    public static int GetEncodedLength(ref Serializer<T,TSerializer> self,T value)=>TSerializer.Serialize(ref value).Length;
    public static void Encode(ref Serializer<T,TSerializer> self,T value,Span<byte> destination){
        var source=TSerializer.Serialize(ref value);
        if(source.Length!=destination.Length){throw new InvalidDataException("The serializer length changed between measurement and encoding.");}
        source.CopyTo(destination);
    }
    public static T Decode(ref Serializer<T,TSerializer> self,ReadOnlySpan<byte> source)=>TSerializer.Deserialize(source);
}
