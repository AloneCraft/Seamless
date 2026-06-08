namespace Benchmark.Benchmarks;

public class GroupJoin_inner:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.GroupJoin(this.src,this.src2,x=>x,x=>x,(outer,inner)=>outer).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.GroupJoin(this.src2,x=>x,x=>x,(outer,inner)=>outer).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.AsValueEnumerable(this.src).GroupJoin(this.src2,x=>x,x=>x,(outer,inner)=>outer).Consume(this.consumer);
    }
}
