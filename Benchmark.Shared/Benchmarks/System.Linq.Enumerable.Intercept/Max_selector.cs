namespace Benchmark.System.Linq.Enumerable.Intercept;

public class Max_selector():Base(5_850_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>this.consumer.Consume(this.src(1).Max(x=>x*2));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().Max(x=>x*2));

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
