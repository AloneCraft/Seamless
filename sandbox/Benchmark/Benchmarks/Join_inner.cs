namespace Benchmark;

public class Join_inner:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Join(src,src2,x=>x,x=>x,(a,b)=>a).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.Join(src2,x=>x,x=>x,(a,b)=>a).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Join(src2,x=>x,x=>x,(a,b)=>a).Consume(consumer);
    }
}
