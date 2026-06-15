namespace Seamless.Benchmarks;

[BenchmarkTestIterations(8)]
public class ElementAt_index():Base(15_430_000){
    private const int iterationCount=3;

    [BenchFact,Benchmark(Baseline=true)]public void Linq(){
        const int index=500_000;
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.src(1).ElementAt(index));
        }
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq(){
        const int index=500_000;
        for(var i=0;i<iterationCount;i++){
            this.consumer.Consume(this.src(1).AsValueEnumerable().ElementAt(index));
        }
    }

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
