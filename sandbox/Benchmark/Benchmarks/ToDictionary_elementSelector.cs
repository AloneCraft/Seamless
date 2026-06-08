namespace Benchmark.Benchmarks;

public class ToDictionary_elementSelector:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.ToDictionary(this.src,x=>x,x=>x*2).Consume(this.consumer);
    }

    [Benchmark]
    public void Intercept(){
        this.src.ToDictionary(x=>x,x=>x*2).Consume(this.consumer);
    }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.AsValueEnumerable(this.src).ToDictionary(x=>x,x=>x*2).Consume(this.consumer);
    }
}
