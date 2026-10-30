namespace Seamless.Execution.Codecs;

public sealed class Adapter<T,TStrategy>(TStrategy strategy):IAdapter<T> where TStrategy:struct,IStrategy<TStrategy,T>{
    private TStrategy strategy=strategy;
    public string FormatId=>TStrategy.FormatId(in this.strategy);
    public int GetEncodedLength(T value)=>TStrategy.GetEncodedLength(ref this.strategy,value);
    public void Encode(T value,Span<byte> destination)=>TStrategy.Encode(ref this.strategy,value,destination);
    public T Decode(ReadOnlySpan<byte> source)=>TStrategy.Decode(ref this.strategy,source);
}
