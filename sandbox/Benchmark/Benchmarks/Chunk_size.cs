namespace Benchmark.Benchmarks;

public class Chunk_size:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Chunk(src,1000).Consume(consumer);
    }

    void InterceptCore(){ this.src.Chunk(1000).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Chunk(1000).Consume(consumer);
    }
}
