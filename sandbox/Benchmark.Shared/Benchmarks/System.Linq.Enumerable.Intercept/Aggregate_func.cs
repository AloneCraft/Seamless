namespace Benchmark.System.Linq.Enumerable.Intercept;

public class Aggregate_func():Base(3_000_000){
    [BenchFact,Benchmark(Baseline=true)]
    public void Linq(){
        var result=this.src(1).Aggregate((acc,x)=>acc+x);
        this.Result=result;
        this.consumer.Consume(result);
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]
    public void ZLinq(){
        var result=this.src(1).AsValueEnumerable().Aggregate((acc,x)=>acc+x);
        this.Result=result;
        this.consumer.Consume(result);
    }

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
