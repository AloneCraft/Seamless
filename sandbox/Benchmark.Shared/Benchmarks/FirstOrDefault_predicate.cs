namespace Seamless.Benchmarks;

public class FirstOrDefault_predicate:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        this.consumer.Consume(src(1).FirstOrDefault(x=>x>500000));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().FirstOrDefault(x=>x>500000));
    }
}
