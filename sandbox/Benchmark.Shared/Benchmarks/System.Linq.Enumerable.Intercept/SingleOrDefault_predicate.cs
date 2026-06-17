namespace Benchmark.System.Linq.Enumerable.Intercept;

public class SingleOrDefault_predicate():Base(4_690_000){
    [BenchFact,Benchmark(Baseline=true)]public void Linq()=>/* 要素数に依存しなぁE��数(先頭要素=1)で一意一致させる。count は instance なのでラムダ冁E��は使えなぁE��E */ this.consumer.Consume(this.src(1).SingleOrDefault(x=>x==1));

    [BenchFact,Benchmark]public void FLinq()=>this.Linq();

    [BenchFact,Benchmark]public void ZLinq()=>this.consumer.Consume(this.src(1).AsValueEnumerable().SingleOrDefault(x=>x==1));

    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Linq,this.FLinq,this.FLinqNoOptimized,this.ZLinq);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Linq();
}
