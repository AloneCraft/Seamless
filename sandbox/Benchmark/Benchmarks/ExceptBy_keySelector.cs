namespace Benchmark;

public class ExceptBy_keySelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ExceptBy(src,src2,x=>x).Consume(consumer);
    }

    void InterceptCore(){ this.src.ExceptBy(this.src2,x=>x).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().ExceptBy(src2,x=>x).Consume(consumer);
    }
}
