using global::Tsavorite.Linq;

namespace Benchmark.Intercept.Tsavorite.Linq.Enumerable;

public class Any():TsavoriteBase() {
    #if BENCH_AS_TEST
    private const int iterationCount=1;
    #else
    private const int iterationCount=800_000;
    #endif
    // System.Tsavorite baseline: リフレクション直呼びのため傍受されず生のまま
    [BenchFact,Benchmark(Baseline=true)]public void Tsavorite(){
        for(var a=0;a<iterationCount;a++)
        this.consumer.Consume(this.src(1).Any());
    }

    // FLinq: this.Tsavorite() 呼び出しが whole-method interception で展開されめE

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();


    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
