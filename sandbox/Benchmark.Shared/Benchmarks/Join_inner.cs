namespace Benchmark.Benchmarks;

public class Join_inner:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Join(this.src,this.src2,x=>x,x=>x,(a,b)=>a).Consume(this.consumer);
    }

    void JoinCore(){ this.src.Join(this.src2,x=>x,x=>x,(a,b)=>a).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.JoinCore(); }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.AsValueEnumerable(this.src).Join(this.src2,x=>x,x=>x,(a,b)=>a).Consume(this.consumer);
    }
}
