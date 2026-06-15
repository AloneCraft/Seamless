namespace Seamless.Benchmarks;

[BenchmarkTestIterations(350)]
public class First():Base(20_000_000){
    private const int iterationCount=5_000;

    [BenchFact,Benchmark(Baseline=true)]public void Linq(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.src(1).First());
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq(){
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.src(1).AsValueEnumerable().First());
        }
    }

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
