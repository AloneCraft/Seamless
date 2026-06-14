namespace Seamless.Benchmarks;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[BenchmarkTestIterations(6)]
public class Take_count():Base(5_950_000){
    [BenchFact]
    [Benchmark(Baseline=true)]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ1(){
        const int takeCount=750_000;
        src(1).Take(takeCount).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ2(){
        const int takeCount=750_000;
        src(1).Take(takeCount).Take(takeCount).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ3(){
        const int takeCount=750_000;
        src(1).Take(takeCount).Take(takeCount).Take(takeCount).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public void LINQ4(){
        const int takeCount=750_000;
        src(1).Take(takeCount).Take(takeCount).Take(takeCount).Take(takeCount).Consume(this.consumer);
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
        const int takeCount=750_000;
        src(1).AsValueEnumerable().Take(takeCount).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq2(){
        const int takeCount=750_000;
        src(1).AsValueEnumerable().Take(takeCount).Take(takeCount).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq3(){
        const int takeCount=750_000;
        src(1).AsValueEnumerable().Take(takeCount).Take(takeCount).Take(takeCount).Consume(this.consumer);
    }

    [BenchFact]
    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public void ZLinq4(){
        const int takeCount=750_000;
        src(1).AsValueEnumerable().Take(takeCount).Take(takeCount).Take(takeCount).Take(takeCount).Consume(this.consumer);
    }
}
