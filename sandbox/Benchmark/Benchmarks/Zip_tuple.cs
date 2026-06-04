namespace Benchmark;

public class Zip_tuple:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Zip(src,src2).Consume(consumer);
    }

    void InterceptCore(){ this.src.Zip(this.src2).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Zip(src2).Consume(consumer);
    }
}
