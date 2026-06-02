namespace Benchmark;

public class Zip_second:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Zip(src,src2,(a,b)=>a+b).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.Zip(src2,(a,b)=>a+b).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Zip(src2,(a,b)=>a+b).Consume(consumer);
    }
}
