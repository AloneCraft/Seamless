using Seamless.FLinq;

using Test.System.Linq.Intercept;

namespace Test.Seamless.Tsavorite.Linq.Enumerable;

public sealed class SetFLinqExpansionTests {
    private static global::Seamless.Tsavorite.Collections.Set<int,Row>? source;

    [Fact, Trait("Category","Unit")]
    public void TsavoriteSetRoot_ExpandsWithoutSeamlessEnumerableFallback() {
        using var store = Store.Create().CreateSet<int,Row>();
        using var set = store.Create();
        set.Add(new(1,10));
        set.Add(new(2,20));
        set.Add(new(3,30));

        source=set;
        try {
            Assert.Equal(55,WhereSelectSumImpl());

            var generated = GeneratedInterceptorAssert.SourceFor<SetFLinqExpansionTests>(nameof(WhereSelectSumImpl));
            Assert.DoesNotContain("Seamless.Linq.Enumerable",generated);
            Assert.DoesNotContain("System.Linq.Enumerable.Where",generated);
            Assert.DoesNotContain("System.Linq.Enumerable.Select",generated);
            Assert.DoesNotContain("System.Linq.Enumerable.Sum",generated);
            Assert.DoesNotContain("ScanCursor",generated);
            Assert.Contains("ISetIterateStrategy",generated);
            Assert.Contains(".Iterate(ref __TsavoriteSetIterateState",generated);
        } finally {
            source=null;
        }
    }

    [Fact, Trait("Category","Unit")]
    public void TsavoriteSetIterate_ExposesOnlyCurrentRecordsAndReturnsStrategyState() {
        using var store = Store.Create().CreateSet<int,Row>();
        using var set = store.Create();
        set.Add(new(1,10));
        set.Add(new(2,20));
        Assert.True(set.Remove(1,out var removed));
        Assert.Equal(10,removed.Value);
        Assert.True(set.Add(new(1,99)));

        var collect = new CollectStrategy { Items= [] };
        set.Iterate(ref collect);
        Assert.Equal(2,collect.Items!.Count);
        Assert.Contains(new Row(1,99),collect.Items);
        Assert.Contains(new Row(2,20),collect.Items);
        Assert.DoesNotContain(new Row(1,10),collect.Items);

        var stop = new StopAfterOneStrategy();
        set.Iterate(ref stop);
        Assert.Equal(1,stop.Count);

        var throwing = new ThrowingStrategy();
        Assert.Throws<StrategyException>(() => set.Iterate(ref throwing));
        Assert.True(set.ContainsKey(2));
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int WhereSelectSumImpl() =>
        Source()
            .Where(static row => row.Key>=2)
            .Select(static row => row.Key+row.Value)
            .Sum();

    private static global::Seamless.Tsavorite.Collections.Set<int,Row> Source() =>
        source??throw new InvalidOperationException("Source was not initialized.");

    public readonly record struct Row(int Key,int Value):global::Seamless.Collections.IRecord<int>;

    private struct CollectStrategy:global::Seamless.Tsavorite.Collections.ISetIterateStrategy<CollectStrategy,Row> {
        public List<Row>? Items;
        public static bool Invoke(ref CollectStrategy self,Row item) {
            self.Items!.Add(item);
            return true;
        }
    }

    private struct StopAfterOneStrategy:global::Seamless.Tsavorite.Collections.ISetIterateStrategy<StopAfterOneStrategy,Row> {
        public int Count;
        public static bool Invoke(ref StopAfterOneStrategy self,Row item) {
            self.Count++;
            return false;
        }
    }

    private struct ThrowingStrategy:global::Seamless.Tsavorite.Collections.ISetIterateStrategy<ThrowingStrategy,Row> {
        public static bool Invoke(ref ThrowingStrategy self,Row item) => throw new StrategyException();
    }

    private sealed class StrategyException:Exception;
}
