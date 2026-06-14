namespace Seamless.Benchmarks;

public class Any():Base() {
    private const int count=1000;
    // System.Linq baseline: リフレクション直呼びのため傍受されず生のまま
    [BenchFact,Benchmark(Baseline=true)]
    public void LINQ(){
        for(var a=0;a<count;a++)
        this.consumer.Consume(src(1).Any());
    }

    // FLinq: this.LINQ() 呼び出しが whole-method interception で展開される

    [BenchFact,Benchmark]
    public void FLinq(){ this.LINQ(); }

    // ZLinq: 同一連鎖 + .AsValueEnumerable() の頭差分のみ
    [BenchFact,Benchmark]
    public void ZLinq(){
        for(var a=0;a<count;a++)
        this.consumer.Consume(src(1).AsValueEnumerable().Any());
    }
}
