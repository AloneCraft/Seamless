namespace Benchmark.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class TakeWhile_predicate:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.TakeWhile(src,x=>x<750001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.TakeWhile(Enumerable.TakeWhile(src,x=>x<750001),x=>x<750001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.TakeWhile(Enumerable.TakeWhile(Enumerable.TakeWhile(src,x=>x<750001),x=>x<750001),x=>x<750001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.TakeWhile(Enumerable.TakeWhile(Enumerable.TakeWhile(Enumerable.TakeWhile(src,x=>x<750001),x=>x<750001),x=>x<750001),x=>x<750001).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){ Intercept1Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){ Intercept2Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){ Intercept3Core(); }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){ Intercept4Core(); }

    void Intercept1Core(){ this.src.AsEnumerable().TakeWhile(x=>x<750001).Consume(this.consumer); }
    void Intercept2Core(){ this.src.AsEnumerable().TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).Consume(this.consumer); }
    void Intercept3Core(){ this.src.AsEnumerable().TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).Consume(this.consumer); }
    void Intercept4Core(){ this.src.AsEnumerable().TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).Consume(this.consumer); }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).TakeWhile(x=>x<750001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        global::ZLinq.ValueEnumerable.AsValueEnumerable(src).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).Consume(consumer);
    }
}
