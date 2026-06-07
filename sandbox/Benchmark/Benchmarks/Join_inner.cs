namespace Benchmark.Benchmarks;

public class Join_inner:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Join(src,src2,x=>x,x=>x,(a,b)=>a).Consume(consumer);
    }

    void JoinCore(){ this.src.Join(this.src2,x=>x,x=>x,(a,b)=>a).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ JoinCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Join(src2,x=>x,x=>x,(a,b)=>a).Consume(consumer);
    }
}
