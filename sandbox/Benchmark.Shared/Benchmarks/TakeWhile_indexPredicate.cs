namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class TakeWhile_indexPredicate:Base{
    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        src(1).TakeWhile((x,i)=>i<750000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        src(1).TakeWhile((x,i)=>i<750000).TakeWhile((x,i)=>i<750000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        src(1).TakeWhile((x,i)=>i<750000).TakeWhile((x,i)=>i<750000).TakeWhile((x,i)=>i<750000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        src(1).TakeWhile((x,i)=>i<750000).TakeWhile((x,i)=>i<750000).TakeWhile((x,i)=>i<750000).TakeWhile((x,i)=>i<750000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.FLinq)]
    public void FLinq1(){ this.LINQ1(); }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.FLinq)]
    public void FLinq2(){ this.LINQ2(); }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.FLinq)]
    public void FLinq3(){ this.LINQ3(); }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.FLinq)]
    public void FLinq4(){ this.LINQ4(); }

    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq1(){
        src(1).AsValueEnumerable().TakeWhile((x,i)=>i<750000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        src(1).AsValueEnumerable().TakeWhile((x,i)=>i<750000).TakeWhile((x,i)=>i<750000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        src(1).AsValueEnumerable().TakeWhile((x,i)=>i<750000).TakeWhile((x,i)=>i<750000).TakeWhile((x,i)=>i<750000).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        src(1).AsValueEnumerable().TakeWhile((x,i)=>i<750000).TakeWhile((x,i)=>i<750000).TakeWhile((x,i)=>i<750000).TakeWhile((x,i)=>i<750000).Consume(this.consumer);
    }
}
