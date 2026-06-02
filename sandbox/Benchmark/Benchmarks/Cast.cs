namespace Benchmark;

public class Cast:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Cast<int>(src).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.Cast<int>().Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Cast<int>().Consume(consumer);
    }
}
