namespace Benchmark;

public class Zip_tuple:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Zip(src,src2).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.Zip(src2).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Zip(src2).Consume(consumer);
    }
}
