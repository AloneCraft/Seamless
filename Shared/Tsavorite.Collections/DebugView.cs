#if BENCHMARK_PROJECT
namespace Benchmark.Tsavorite.Collections;
#else
namespace Tsavorite.Collections;
#endif


public class DebugView {
    [BenchFact,Benchmark]public void UnmanagedDictionary() {
        using var store = Store.Create().Create<int,int>();
        using var dict = store.Create();
        for(var a=0;a<10;a++){
            dict.Add(a,a);
        }
    }
    [BenchFact,Benchmark]public void UnmanagedHashSet() {
        using var store = Store.Create().Create<int>();
        using var dict = store.Create();
        for(var a=0;a<10;a++){
            dict.Add(a);
        }
    }
    [BenchFact,Benchmark]public void Dictionary() {
        using var store = Store.Create().Create<string,string>();
        using var dict = store.Create();
        for(var a=0;a<10;a++){
            dict.Add(a.ToString(),a.ToString());
        }
    }
    [BenchFact,Benchmark]public void HashSet() {
        using var store = Store.Create().Create<string>();
        using var dict = store.Create();
        for(var a=0;a<10;a++){
            dict.Add(a.ToString());
        }
    }
}
