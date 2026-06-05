namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Reverse:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Reverse(src).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Reverse(Enumerable.Reverse(src)).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Reverse(Enumerable.Reverse(Enumerable.Reverse(src))).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Reverse(Enumerable.Reverse(Enumerable.Reverse(Enumerable.Reverse(src)))).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        Intercept1Core();
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        Intercept2Core();
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        Intercept3Core();
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        Intercept4Core();
    }

    void Intercept1Core(){ this.src.AsEnumerable().Reverse().Consume(this.consumer); }
    void Intercept2Core(){ this.src.AsEnumerable().Reverse().Reverse().Consume(this.consumer); }
    void Intercept3Core(){ this.src.AsEnumerable().Reverse().Reverse().Reverse().Consume(this.consumer); }
    void Intercept4Core(){ this.src.AsEnumerable().Reverse().Reverse().Reverse().Reverse().Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Reverse().Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Reverse().Reverse().Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Reverse().Reverse().Reverse().Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).Reverse().Reverse().Reverse().Reverse().Consume(consumer);
    }
}
