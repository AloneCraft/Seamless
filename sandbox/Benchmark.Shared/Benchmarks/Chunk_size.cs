namespace Seamless.Benchmarks;

public class Chunk_size():Base(3_950_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=> this.src(1).Chunk(1000).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=> this.src(1).AsValueEnumerable().Chunk(1000).Consume(this.consumer);
}
