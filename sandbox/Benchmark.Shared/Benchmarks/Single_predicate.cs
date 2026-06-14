namespace Seamless.Benchmarks;

public class Single_predicate():Base(4_680_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ() {
        var y=srcOne(1).ToArray();
        var x=y.Single(x => x==0);
        // 要素数に依存しなぁE��数(先頭要素=1)で一意一致させる。count は instance なのでラムダ冁E��は使えなぁE��E
        this.consumer.Consume(src(1).Single(x=>x==1));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Single(x=>x==1));
    }
}
