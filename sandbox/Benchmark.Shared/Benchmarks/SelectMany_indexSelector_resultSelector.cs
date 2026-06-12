namespace Seamless.Benchmarks;

public class SelectMany_indexSelector_resultSelector:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        srcNested(1).SelectMany((x,i)=>x,(outer,inner)=>inner+1).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        srcNested(1).AsValueEnumerable().SelectMany((x,i)=>x.AsValueEnumerable(),(outer,inner)=>inner+1).Consume(this.consumer);
    }
}
