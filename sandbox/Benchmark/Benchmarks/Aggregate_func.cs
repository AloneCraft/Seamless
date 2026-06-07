namespace Benchmark.Benchmarks;

public class Aggregate_func:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        consumer.Consume(Enumerable.Aggregate(src,(acc,x)=>acc+x));
    }

    public void InterceptCore(){ this.consumer.Consume(this.src.Aggregate((acc,x)=>acc+x)); }

    [Benchmark]
    public void Intercept(){ InterceptCore(); }

    [Benchmark]
    public void ZLinq(){
        consumer.Consume(global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Aggregate((acc,x)=>acc+x));
    }

    /// <summary>Intercepts <see cref="Benchmark.Benchmarks.Aggregate_func.Intercept()" /> (InterceptCore).</summary>
    void ff() {

    }
    /// <summary>Intercepts <see cref="Benchmark.Benchmarks.Aggregate_func.InterceptCore()" /> (InterceptCore).</summary>
    void fgf() {

    }

}
