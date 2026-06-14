namespace Seamless.Benchmarks;

public class Join_inner():Base(580_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        src(1).Join(src2(1),x=>x,x=>x,(a,b)=>a).Consume(this.consumer);
    }

    void JoinCore(){ src(1).Join(src2(1),x=>x,x=>x,(a,b)=>a).Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        src(1).AsValueEnumerable().Join(src2(1),x=>x,x=>x,(a,b)=>a).Consume(this.consumer);
    }
}
