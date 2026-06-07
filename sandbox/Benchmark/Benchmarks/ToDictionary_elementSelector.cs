namespace Benchmark.Benchmarks;

public class ToDictionary_elementSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToDictionary(src,x=>x,x=>x*2).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.ToDictionary(x=>x,x=>x*2).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).ToDictionary(x=>x,x=>x*2).Consume(consumer);
    }
}
