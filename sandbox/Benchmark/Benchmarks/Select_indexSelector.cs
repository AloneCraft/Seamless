namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Select_indexSelector:Base{
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        Enumerable.Select(src,(x,i)=>x+i).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        Enumerable.Select(Enumerable.Select(src,(x,i)=>x+i),(x,i)=>x+i).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        Enumerable.Select(Enumerable.Select(Enumerable.Select(src,(x,i)=>x+i),(x,i)=>x+i),(x,i)=>x+i).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        Enumerable.Select(Enumerable.Select(Enumerable.Select(Enumerable.Select(src,(x,i)=>x+i),(x,i)=>x+i),(x,i)=>x+i),(x,i)=>x+i).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept1(){
        src.Select((x,i)=>x+i).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept2(){
        src.Select((x,i)=>x+i).Select((x,i)=>x+i).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept3(){
        src.Select((x,i)=>x+i).Select((x,i)=>x+i).Select((x,i)=>x+i).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.Intercept)]
    public void Intercept4(){
        src.Select((x,i)=>x+i).Select((x,i)=>x+i).Select((x,i)=>x+i).Select((x,i)=>x+i).Consume(consumer);
    }

    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src.AsValueEnumerable().Select((x,i)=>x+i).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src.AsValueEnumerable().Select((x,i)=>x+i).Select((x,i)=>x+i).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src.AsValueEnumerable().Select((x,i)=>x+i).Select((x,i)=>x+i).Select((x,i)=>x+i).Consume(consumer);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src.AsValueEnumerable().Select((x,i)=>x+i).Select((x,i)=>x+i).Select((x,i)=>x+i).Select((x,i)=>x+i).Consume(consumer);
    }
}
