namespace Seamless.Benchmarks;

public class Chunk_size:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        src(1).Chunk(1000).Consume(this.consumer);
    }

    void InterceptCore(){ src(1).Chunk(1000).Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().Chunk(1000).Consume(this.consumer);
    }
}
