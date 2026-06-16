using global::Tsavorite.Linq;

namespace Benchmark.Intercept.Tsavorite.Linq.Enumerable;

public class Single_predicate():TsavoriteBase(4_680_000){
    [BenchFact,Benchmark(Baseline=true)]public void Tsavorite() {
        var y= this.srcOne(1).ToArray();
        var x=y.Single(x => x==0);
        // 要素数に依存しなぁE�E��E�数(先頭要素=1)で一意一致させる。count は instance なのでラムダ冁E�E��E�は使えなぁE�E��E�E
        this.consumer.Consume(this.src(1).Single(x=>x==1));
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();


    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
