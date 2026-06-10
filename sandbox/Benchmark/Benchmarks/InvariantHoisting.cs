namespace Benchmark.Benchmarks;

public class InvariantHoisting:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Select(this.src,x=>x+HoistInvariant.Value()).Consume(this.consumer);
    }

    void InterceptCore(){ this.src.Select(x=>x+HoistInvariant.Value()).Consume(this.consumer); }

    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.AsValueEnumerable(this.src).Select(x=>x+HoistInvariant.Value()).Consume(this.consumer);
    }
}
