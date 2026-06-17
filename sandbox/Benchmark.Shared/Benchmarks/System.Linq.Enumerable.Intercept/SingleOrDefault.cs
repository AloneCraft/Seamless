namespace Benchmark.System.Linq.Enumerable.Intercept;

[BenchmarkTestIterations(280)]
public class SingleOrDefault():Base(20_000_000){
    private const int iterationCount=4_500;

    [BenchFact,Benchmark(Baseline=true)]public void Linq(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.srcOne(1).SingleOrDefault());
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.srcOne(1).AsValueEnumerable().SingleOrDefault());
        }
    }

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
