namespace Seamless.Benchmarks;

public class SingleOrDefault_predicate():Base(4_690_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    public void LINQ(){
        // 要素数に依存しなぁE��数(先頭要素=1)で一意一致させる。count は instance なのでラムダ冁E��は使えなぁE��E
        this.consumer.Consume(src(1).SingleOrDefault(x=>x==1));
    }

    [BenchFact]
    [Benchmark]
    public void FLinq(){ this.LINQ(); }

    [BenchFact]
    [Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().SingleOrDefault(x=>x==1));
    }
}
