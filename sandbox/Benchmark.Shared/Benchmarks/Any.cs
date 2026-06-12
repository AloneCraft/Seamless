namespace Seamless.Benchmarks;

public class Any:Base{
    // System.Linq baseline: インスタンス記法のまま [NoLinqRewrite] で傍受抑止
    [BenchFact,Benchmark(Baseline=true)]
    [NoLinqRewrite]
    public void LINQ(){
        this.consumer.Consume(src(1).Any());
    }

    // FLinq: LINQ と同一連鎖。Core 経由で whole-method interception
    void InterceptCore(){ this.consumer.Consume(src(1).Any()); }

    [BenchFact,Benchmark]
    public void Intercept(){ this.InterceptCore(); }

    // ZLinq: 同一連鎖 + .AsValueEnumerable() の頭差分のみ
    [BenchFact,Benchmark]
    public void ZLinq(){
        this.consumer.Consume(src(1).AsValueEnumerable().Any());
    }
}
