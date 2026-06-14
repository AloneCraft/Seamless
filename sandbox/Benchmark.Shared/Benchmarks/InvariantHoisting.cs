namespace Seamless.Benchmarks;

public class InvariantHoisting():Base(5_720_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).Select(x=>x+HoistInvariant.Value()).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().Select(x=>x+HoistInvariant.Value()).Consume(this.consumer);
    }
}
