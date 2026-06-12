namespace Seamless.Benchmarks;

public class GroupJoin_inner:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).GroupJoin(src2(1),x=>x,x=>x,(outer,inner)=>outer).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().GroupJoin(src2(1),x=>x,x=>x,(outer,inner)=>outer).Consume(this.consumer);
    }
}
