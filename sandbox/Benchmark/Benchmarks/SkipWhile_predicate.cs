namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SkipWhile_predicate:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.SkipWhile(src,x=>x<250001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.SkipWhile(Enumerable.SkipWhile(src,x=>x<250001),x=>x<250001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.SkipWhile(Enumerable.SkipWhile(Enumerable.SkipWhile(src,x=>x<250001),x=>x<250001),x=>x<250001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.SkipWhile(Enumerable.SkipWhile(Enumerable.SkipWhile(Enumerable.SkipWhile(src,x=>x<250001),x=>x<250001),x=>x<250001),x=>x<250001).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        src.SkipWhile(x=>x<250001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        src.SkipWhile(x=>x<250001).SkipWhile(x=>x<250001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        src.SkipWhile(x=>x<250001).SkipWhile(x=>x<250001).SkipWhile(x=>x<250001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        src.SkipWhile(x=>x<250001).SkipWhile(x=>x<250001).SkipWhile(x=>x<250001).SkipWhile(x=>x<250001).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src.AsValueEnumerable().SkipWhile(x=>x<250001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src.AsValueEnumerable().SkipWhile(x=>x<250001).SkipWhile(x=>x<250001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src.AsValueEnumerable().SkipWhile(x=>x<250001).SkipWhile(x=>x<250001).SkipWhile(x=>x<250001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src.AsValueEnumerable().SkipWhile(x=>x<250001).SkipWhile(x=>x<250001).SkipWhile(x=>x<250001).SkipWhile(x=>x<250001).Consume(consumer);
    }
}
