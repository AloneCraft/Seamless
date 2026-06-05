namespace Benchmark;

public class Zip_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Zip(src,src2,(a,b)=>a+b).Consume(consumer);
    }

    void InterceptCore(){ this.src.Zip(this.src2,(a,b)=>a+b).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Zip(src2,(a,b)=>a+b).Consume(consumer);
    }
}
