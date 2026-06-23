namespace Benchmark.Shared;

#if BENCH_AS_TEST
#pragma warning disable xUnit1013
#endif

public class TsavoriteBase(int count = 80_000):Base(NormalizeCount(count)),IDisposable {
    private readonly Dictionary<int,SourceHandle<int>> srcCache = [];
    private readonly Dictionary<int,SourceHandle<int>> src2Cache = [];
    private readonly Dictionary<int,SourceHandle<int>> srcOneCache = [];

    protected new global::Seamless.Tsavorite.Collections.HashSet<int> src(int scale) =>
        GetOrCreate(scale,srcCache,static length => CreateRange(1,length));

    protected new global::Seamless.Tsavorite.Collections.HashSet<int> srcCount(int elementCount) =>
        GetOrCreateByLength(elementCount,srcCache,static length => CreateRange(1,length));

    protected new global::Seamless.Tsavorite.Collections.HashSet<int> src2(int scale) =>
        GetOrCreate(scale,src2Cache,static length => {
            var result = new int[length/2];
            var start = length/2+1;
            for(var index = 0;index<result.Length;index++) {
                result[index]=start+index;
            }
            return result;
        });

    protected new global::Seamless.Tsavorite.Collections.HashSet<int> src2Count(int elementCount) =>
        GetOrCreateByLength(elementCount,src2Cache,static length => {
            var result = new int[length/2];
            var start = length/2+1;
            for(var index = 0;index<result.Length;index++) {
                result[index]=start+index;
            }
            return result;
        });

    protected new global::Seamless.Tsavorite.Collections.HashSet<int> srcOne(int scale) =>
        GetOrCreate(scale,srcOneCache,static _ => [0]);

    [GlobalCleanup]
    public void Cleanup() {
        DisposeAll(srcCache);
        DisposeAll(src2Cache);
        DisposeAll(srcOneCache);
    }

    public void Dispose() => Cleanup();

    private static int NormalizeCount(int count) {
#if BENCH_AS_TEST
        return Math.Min(count,8_000);
#else
        return count;
#endif
    }

    private global::Seamless.Tsavorite.Collections.HashSet<int> GetOrCreate(
        int scale,
        Dictionary<int,SourceHandle<int>> cache,
        Func<int,int[]> factory) {
        var length=CountForComparison(CountForTestTiming(count*scale));
        if(cache.TryGetValue(length,out var handle)) {
            return handle.Set;
        }

        var values = factory(length);
        handle=CreateSet(values);
        cache.Add(length,handle);
        return handle.Set;
    }

    private global::Seamless.Tsavorite.Collections.HashSet<int> GetOrCreateByLength(
        int length,
        Dictionary<int,SourceHandle<int>> cache,
        Func<int,int[]> factory) {
        var effectiveLength=CountForComparison(CountForTestTiming(length));
        if(cache.TryGetValue(effectiveLength,out var handle)) {
            return handle.Set;
        }

        var values = factory(effectiveLength);
        handle=CreateSet(values);
        cache.Add(effectiveLength,handle);
        return handle.Set;
    }

    private static int[] CreateRange(int start,int length) {
        var result = new int[length];
        for(var index = 0;index<result.Length;index++) {
            result[index]=start+index;
        }
        return result;
    }

    private static SourceHandle<T> CreateSet<T>(IEnumerable<T> values)
        where T : unmanaged, IEquatable<T> {
        var count = values.TryGetNonEnumeratedCount(out var knownCount) ? knownCount : 80_000;
        var store = Seamless.Tsavorite.Collections.Store.Create(
            IndexSize: Seamless.Tsavorite.Collections.Store.GetIndexSizeForCapacity(count),
            MemorySize: GetMemorySize(count))
            .Create<T>();
        var set = store.Create();
        foreach(var value in values) {
            set.Add(value);
        }
        return new SourceHandle<T>(store,set);
    }

    private static long GetMemorySize(int count) {
        var required = Math.Max(8192L,count*128L);
        var result = 1L;
        while(result<required) {
            result<<=1;
        }
        return result;
    }

    private static void DisposeAll<T>(Dictionary<int,SourceHandle<T>> cache)
        where T : unmanaged, IEquatable<T> {
        foreach(var handle in cache.Values) {
            handle.Dispose();
        }
        cache.Clear();
    }

    public sealed class SourceHandle<T>(
        IDisposable store,
        global::Seamless.Tsavorite.Collections.HashSet<T> set):IDisposable
        where T : unmanaged, IEquatable<T> {
        public global::Seamless.Tsavorite.Collections.HashSet<T> Set { get; } = set;

        public void Dispose() {
            Set.Dispose();
            store.Dispose();
        }
    }
}

#if BENCH_AS_TEST
#pragma warning restore xUnit1013
#endif
