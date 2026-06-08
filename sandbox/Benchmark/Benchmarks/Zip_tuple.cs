namespace Benchmark.Benchmarks;

public class Zip_tuple:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Zip(this.src,this.src2).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.Zip(this.src2).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.AsValueEnumerable(this.src).Zip(this.src2).Consume(this.consumer);
    }
}
