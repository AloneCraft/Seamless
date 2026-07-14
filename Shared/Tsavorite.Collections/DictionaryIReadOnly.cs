#if BENCHMARK_PROJECT
namespace Benchmark.Tsavorite.Collections;
#else
namespace Tsavorite.Collections;
#endif

public class DictionaryIReadOnly {
    [BenchFact, Benchmark]
    public void Keys_IReadOnlyDictionary() {
        using var store = Store.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        IReadOnlyDictionary<int,string> readOnly = dict;
        var keys = readOnly.Keys;
        Assert.Equal(2,keys.Count());
        Assert.Contains(1,keys);
        Assert.Contains(2,keys);
    }
    [BenchFact, Benchmark]
    public void Values_IReadOnlyDictionary() {
        using var store = Store.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        IReadOnlyDictionary<int,string> readOnly = dict;
        var values = readOnly.Values;
        Assert.Equal(2,values.Count());
        Assert.Contains("a",values);
        Assert.Contains("b",values);
    }
}
