namespace Benchmark.Benchmarks;

public class SelectMany_Hoisting_PredicatePushdown:Base{
    private readonly int[] outer=Enumerable.Range(0,100_000).ToArray();
    private readonly int[] inner=Enumerable.Range(0,128).ToArray();
    private const int Threshold=120;

    [Benchmark(Baseline=true)]
    public void LINQ(){
        Enumerable.Where(Enumerable.SelectMany(this.outer,_=>this.inner),x=>x>Threshold&&HoistInvariant.Value()==0).Consume(this.consumer);
    }

    void InterceptCore(){ this.outer.SelectMany(_=>this.inner).Where(x=>x>Threshold&&HoistInvariant.Value()==0).Consume(this.consumer); }

    [Benchmark]
    public void Intercept() {
        this.InterceptCore();
    }

    [Benchmark]
    public void ZLinq(){
        ValueEnumerable.AsValueEnumerable(this.outer).SelectMany(_=>ValueEnumerable.AsValueEnumerable(this.inner)).Where(x=>x>Threshold&&HoistInvariant.Value()==0).Consume(this.consumer);
    }
}
