namespace Benchmark.Shared;

#if BENCH_AS_TEST
#pragma warning disable xUnit1013
#endif

public class TsavoriteBase(int count = 80_000):Base(NormalizeCount(count)),IDisposable {
    private readonly Dictionary<int,SourceHandle<int>> srcCache = [];
    private readonly Dictionary<int,SourceHandle<int>> src2Cache = [];
    private readonly Dictionary<int,SourceHandle<int>> srcOneCache = [];

    protected new global::Tsavorite.Collections.HashSet<int> src(int scale) =>
        this.GetOrCreate(scale,this.srcCache,static length => CreateRange(1,length));

    protected new global::Tsavorite.Collections.HashSet<int> src2(int scale) =>
        this.GetOrCreate(scale,this.src2Cache,static length => {
            var result = new int[length/2];
            var start = length/2+1;
            for(var index = 0;index<result.Length;index++) {
                result[index]=start+index;
            }
            return result;
        });

    protected new global::Tsavorite.Collections.HashSet<int> srcOne(int scale) =>
        this.GetOrCreate(scale,this.srcOneCache,static _ => [0]);

    [GlobalCleanup]
    public void Cleanup() {
        DisposeAll(this.srcCache);
        DisposeAll(this.src2Cache);
        DisposeAll(this.srcOneCache);
    }

    public void Dispose() => this.Cleanup();

    private static int NormalizeCount(int count) {
#if BENCH_AS_TEST
        return Math.Min(count,8_000);
#else
        return count;
#endif
    }

    private global::Tsavorite.Collections.HashSet<int> GetOrCreate(
        int scale,
        Dictionary<int,SourceHandle<int>> cache,
        Func<int,int[]> factory) {
        if(cache.TryGetValue(scale,out var handle)) {
            return handle.Set;
        }

        var values = factory(this.CountForTestTiming(this.count*scale));
        handle=CreateSet(values);
        cache.Add(scale,handle);
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
        var store = global::Tsavorite.Stores.HashStore.Create(
            IndexSize: global::Tsavorite.Stores.HashStore.GetIndexSizeForCapacity(count),
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
        global::Tsavorite.Collections.HashSet<T> set):IDisposable
        where T : unmanaged, IEquatable<T> {
        public global::Tsavorite.Collections.HashSet<T> Set { get; } = set;

        public void Dispose() {
            this.Set.Dispose();
            store.Dispose();
        }
    }
}

#if BENCH_AS_TEST
#pragma warning restore xUnit1013
#endif
