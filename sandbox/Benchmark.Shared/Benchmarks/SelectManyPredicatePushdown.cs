namespace Seamless.Benchmarks;

public class SelectManyPredicatePushdown:Base{
    private readonly int[] outer=Enumerable.Range(0,30_000).ToArray();
    private readonly int[] inner=Enumerable.Range(0,128).ToArray();
    private const int Threshold=120;

    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.outer.SelectMany(_=>this.inner).Where(x=>x>Threshold).Consume(this.consumer);

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.outer.AsValueEnumerable().SelectMany(_=>this.inner.AsValueEnumerable()).Where(x=>x>Threshold).Consume(this.consumer);
}
