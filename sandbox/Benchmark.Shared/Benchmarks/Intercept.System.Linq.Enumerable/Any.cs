namespace Benchmark.Intercept.System.Linq.Enumerable;

public class Any():Base() {
    private const int iterationCount=800_000;
    // System.Linq baseline: リフレクション直呼びのため傍受されず生のまま
    [BenchFact,Benchmark(Baseline=true)]public void Linq(){
        for(var a=0;a<iterationCount;a++)
        this.consumer.Consume(this.src(1).Any());
    }

    // FLinq: this.Linq() 呼び出しが whole-method interception で展開される

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    // ZLinq: 同一連鎖 + .AsValueEnumerable() の頭差分のみ
    [BenchFact,Benchmark]public void ZLinq(){
        for(var a=0;a<iterationCount;a++)
        this.consumer.Consume(this.src(1).AsValueEnumerable().Any());
    }

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
