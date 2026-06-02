namespace Benchmark;

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
    public void Intercept1(){
        src.TakeWhile(x=>x<750001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        src.TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        src.TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        src.TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src.AsValueEnumerable().TakeWhile(x=>x<750001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src.AsValueEnumerable().TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src.AsValueEnumerable().TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src.AsValueEnumerable().TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).TakeWhile(x=>x<750001).Consume(consumer);
    }
}
