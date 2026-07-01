using Seamless.FLinq;
using Test.System.Linq.Intercept;

namespace Test.Seamless.Tsavorite.Linq.Enumerable;

public class FLinqNativeIterateTests {
    private static global::Seamless.Tsavorite.Collections.HashSet<int>? source;

    [Fact, Trait("Category","Unit")]
    public void TsavoriteHashSetRoot_UsesNativeIterateTerminalForCount() {
        using var store = Store.Create().CreateHashSet<int>();
        using var set = store.Create();
        foreach(var value in new[] { 1,2,3,4 })
            set.Add(value);

        source=set;
        try {
            Assert.Equal(2,CountEvenImpl());

            var generated = GeneratedInterceptorAssert.SourceFor<FLinqNativeIterateTests>(nameof(CountEvenImpl));
            Assert.Contains("TsavoriteHashSetIterateState",generated);
            Assert.Contains("IHashSetIterateStrategy",generated);
            Assert.Contains(".Iterate(ref __TsavoriteHashSetIterateState",generated);
            Assert.DoesNotContain("ScanCursor",generated);
            Assert.DoesNotContain("HashSetScanUnsafe",generated);
            Assert.DoesNotContain("ScanUnsafeDelegate",generated);
            Assert.DoesNotContain("Seamless.Linq.Enumerable",generated);
            Assert.DoesNotContain("MemoryExtensions.AsSpan",generated);
            Assert.DoesNotContain("CollectionsMarshal.AsSpan",generated);
            Assert.DoesNotContain(".GetSpan();",generated);
        }
        finally {
            source=null;
        }
    }

    [Fact, Trait("Category","Unit")]
    public void TsavoriteHashSetRoot_IterateDoesNotSeeRemovedOrStaleKeys() {
        using var store = Store.Create().CreateHashSet<int>();
        using var set = store.Create();
        foreach(var value in new[] { 1,2,3 })
            set.Add(value);
        Assert.True(set.Remove(2));

        source=set;
        try {
            Assert.Equal(0,CountEvenImpl());
            Assert.False(AnyEvenImpl());

            set.Add(2);
            Assert.Equal(1,CountEvenImpl());
            Assert.True(AnyEvenImpl());

            AssertNativeIterateGenerated(nameof(CountEvenImpl));
            AssertNativeIterateGenerated(nameof(AnyEvenImpl));
        }
        finally {
            source=null;
        }
    }

    [Fact, Trait("Category","Unit")]
    public void TsavoriteHashSetRoot_UsesNativeIterateTerminalForAllAndFirstOrDefault() {
        using var store = Store.Create().CreateHashSet<int>();
        using var set = store.Create();
        foreach(var value in new[] { 1,2,3,4 })
            set.Add(value);

        source=set;
        try {
            Assert.True(AllLessThanFiveImpl());
            Assert.Equal(3,FirstOrDefaultGreaterThanTwoImpl());

            AssertNativeIterateGenerated(nameof(AllLessThanFiveImpl));
            AssertNativeIterateGenerated(nameof(FirstOrDefaultGreaterThanTwoImpl));
        }
        finally {
            source=null;
        }
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int CountEvenImpl() =>
        Source().Where(static value => value%2==0).Count();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool AnyEvenImpl() =>
        Source().Where(static value => value%2==0).Any();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool AllLessThanFiveImpl() =>
        Source().All(static value => value<5);

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int FirstOrDefaultGreaterThanTwoImpl() =>
        Source().Where(static value => value>2).FirstOrDefault();

    private static global::Seamless.Tsavorite.Collections.HashSet<int> Source() =>
        source ?? throw new InvalidOperationException("Source was not initialized.");

    private static void AssertNativeIterateGenerated(string methodName) {
        var generated = GeneratedInterceptorAssert.SourceFor<FLinqNativeIterateTests>(methodName);
        Assert.Contains("TsavoriteHashSetIterateState",generated);
        Assert.Contains("IHashSetIterateStrategy",generated);
        Assert.Contains(".Iterate(ref __TsavoriteHashSetIterateState",generated);
        Assert.DoesNotContain("ScanCursor",generated);
        Assert.DoesNotContain("HashSetScanUnsafe",generated);
        Assert.DoesNotContain("ScanUnsafeDelegate",generated);
        Assert.DoesNotContain("Seamless.Linq.Enumerable",generated);
    }
}
