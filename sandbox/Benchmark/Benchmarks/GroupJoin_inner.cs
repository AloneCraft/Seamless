namespace Benchmark.Benchmarks;

public class GroupJoin_inner:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.GroupJoin(src,src2,x=>x,x=>x,(outer,inner)=>outer).Consume(consumer);
    }

    void InterceptCore(){ this.src.GroupJoin(this.src2,x=>x,x=>x,(outer,inner)=>outer).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).GroupJoin(src2,x=>x,x=>x,(outer,inner)=>outer).Consume(consumer);
    }
}
