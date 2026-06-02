namespace Benchmark;

public class Chunk_size:Base{
    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Chunk(src,1000).Consume(consumer);
    }

    [Benchmark]
    public void Intercept(){
        src.Chunk(1000).Consume(consumer);
    }

    [Benchmark]
    public void ZLinq(){
        src.AsValueEnumerable().Chunk(1000).Consume(consumer);
    }
}
