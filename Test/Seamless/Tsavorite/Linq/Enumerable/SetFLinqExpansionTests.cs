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
        }
        finally {
            source=null;
        }
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int WhereSelectSumImpl() =>
        Source()
            .Where(static row => row.Key>=2)
            .Select(static row => row.Key+row.Value)
            .Sum();

    private static global::Seamless.Tsavorite.Collections.Set<int,Row> Source() =>
        source ?? throw new InvalidOperationException("Source was not initialized.");

    public readonly record struct Row(int Key,int Value):global::Seamless.Collections.IRecord<int>;
}
