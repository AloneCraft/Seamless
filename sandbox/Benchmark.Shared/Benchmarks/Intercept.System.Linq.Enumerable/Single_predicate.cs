namespace Benchmark.Intercept.System.Linq.Enumerable;

public class Single_predicate():Base(4_680_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq() {
        var y= this.srcOne(1).ToArray();
        var x=y.Single(x => x==0);
        // 要素数に依存しなぁE��数(先頭要素=1)で一意一致させる。count は instance なのでラムダ冁E��は使えなぁE��E
        this.consumer.Consume(this.src(1).Single(x=>x==1));
    }

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().Single(x=>x==1));

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
