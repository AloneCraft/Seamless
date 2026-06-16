namespace Benchmark.Intercept.System.Linq.Enumerable;

[BenchmarkTestIterations(7)]
public class Contains_value():Base(10_630_000){
    private const int iterationCount=3;

    [BenchFact,Benchmark(Baseline=true)]public void Linq(){
        const int value=500_000;
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.src(1).Contains(value));
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq(){
        const int value=500_000;
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.src(1).AsValueEnumerable().Contains(value));
        }
    }

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
