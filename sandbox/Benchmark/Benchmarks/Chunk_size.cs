namespace Benchmark.Benchmarks;

public class Chunk_size:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Chunk(this.src,1000).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.Chunk(1000).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(this.src).Chunk(1000).Consume(this.consumer);
    }
}
