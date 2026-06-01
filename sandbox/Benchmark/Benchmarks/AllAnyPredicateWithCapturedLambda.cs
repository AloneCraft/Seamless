using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using ZLinq;

namespace Benchmark;

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class AnyPredicateWithCapturedLambda
{
    [Params(10, 100, 1000, 10000)]
    public int N;

    int[] _nums = default!;
    int _target;

    [GlobalSetup]
    public void Setup()
    {
        _nums = Enumerable.Range(1, N).ToArray();
        _target = N / 2;
    }

    [Benchmark]
    [BenchmarkCategory("Array")]
    public bool ArrayExists()
    {
        return Array.Exists(_nums, i => i > _target);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public bool ZLinq()
    {
        return _nums.AsValueEnumerable().Any(i => i > _target);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public bool Linq()
    {
        return _nums.Any(i => i > _target);
    }
}

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class AllPredicateWithCapturedLambda
{
    [Params(10, 100, 1000, 10000)]
    public int N;

    int[] _nums = default!;
    int _target;

    [GlobalSetup]
    public void Setup()
    {
        _nums = Enumerable.Range(1, N).ToArray();
        _target = N;
    }

    [Benchmark]
    [BenchmarkCategory("Array")]
    public bool TrueForAll()
    {
        return Array.TrueForAll(_nums, i => i <= _target);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public bool ZLinq()
    {
        return _nums.AsValueEnumerable().All(i => i <= _target);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public bool Linq()
    {
        return _nums.All(i => i <= _target);
    }
}

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CountPredicateWithCapturedLambda
{
    [Params(10, 100, 1000, 10000)]
    public int N;

    int[] _nums = default!;
    int _target;

    [GlobalSetup]
    public void Setup()
    {
        _nums = Enumerable.Range(1, N).ToArray();
        _target = N;
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int ZLinq()
    {
        return _nums.AsValueEnumerable().Count(i => i <= _target);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public int Linq()
    {
        return _nums.Count(i => i <= _target);
    }
}

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LongCountPredicateWithCapturedLambda
{
    [Params(10, 100, 1000, 10000)]
    public int N;

    int[] _nums = default!;
    int _target;

    [GlobalSetup]
    public void Setup()
    {
        _nums = Enumerable.Range(1, N).ToArray();
        _target = N;
    }

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public long ZLinq()
    {
        return _nums.AsValueEnumerable().LongCount(i => i <= _target);
    }

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public long Linq()
    {
        return _nums.LongCount(i => i <= _target);
    }
}
