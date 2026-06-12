namespace Seamless.Benchmarks;

public class SelectManyPredicatePushdown:Base{
    private readonly int[] outer=Enumerable.Range(0,100_000).ToArray();
    private readonly int[] inner=Enumerable.Range(0,128).ToArray();
    private const int Threshold=120;

    [BenchFact]
    [Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.outer.SelectMany(_=>this.inner).Where(x=>x>Threshold).Consume(this.consumer);
    }

    void InterceptCore(){ this.outer.SelectMany(_=>this.inner).Where(x=>x>Threshold).Consume(this.consumer); }

    [BenchFact]
    [Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.outer.AsValueEnumerable().SelectMany(_=>this.inner.AsValueEnumerable()).Where(x=>x>Threshold).Consume(this.consumer);
    }
}
