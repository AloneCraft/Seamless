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
        } finally {
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
        } finally {
            source=null;
        }
    }

    [Fact, Trait("Category","Unit")]
    public void TsavoriteHashSetRoot_UsesNativeIterateTerminalForAllAndExplicitFirstOrDefault() {
        using var store = Store.Create().CreateHashSet<int>();
        using var set = store.Create();
        foreach(var value in new[] { 1,2,3,4 })
            set.Add(value);

        source=set;
        try {
            Assert.True(AllLessThanFiveImpl());
            Assert.Throws<NotSupportedException>(() => { _=DirectFirstOrDefaultGreaterThanTwoImpl(); });
            Assert.Equal(3,ExplicitFirstOrDefaultGreaterThanTwoImpl());

            AssertNativeIterateGenerated(nameof(AllLessThanFiveImpl));
            AssertNativeIterateGenerated(nameof(ExplicitFirstOrDefaultGreaterThanTwoImpl));
        } finally {
            source=null;
        }
    }

    [Fact, Trait("Category","Unit")]
    public void TsavoriteHashSetRoot_UsesNativeIterateForMaterializeAndHashTerminals() {
        using var store = Store.Create().CreateHashSet<int>();
        using var set = store.Create();
        foreach(var value in new[] { 1,2,3,4 })
            set.Add(value);

        source=set;
        try {
            Assert.Equal([20,40],ToArrayImpl().Order());
            Assert.Equal([1,3],ToListImpl().Order());
            Assert.Equal(10,SumImpl());
            Assert.Equal(4,ToDictionaryImpl().Count);

            AssertNativeMaterializeGenerated(nameof(ToArrayImpl),"Array=global::System.GC.AllocateUninitializedArray");
            AssertNativeMaterializeGenerated(nameof(ToListImpl),"List=new global::System.Collections.Generic.List");
            AssertNativeMaterializeGenerated(nameof(SumImpl),"state.Sum+=");
            AssertNativeMaterializeGenerated(nameof(ToDictionaryImpl),"Dictionary=new");
        } finally {
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
    private static int DirectFirstOrDefaultGreaterThanTwoImpl() =>
        Source().Where(static value => value>2).FirstOrDefault();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int ExplicitFirstOrDefaultGreaterThanTwoImpl() =>
        Source().AsEnumerable().Where(static value => value>2).FirstOrDefault();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int[] ToArrayImpl() =>
        Source().Where(static value => value%2==0).Select(static value => value*10).ToArray();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static List<int> ToListImpl() =>
        Source().Where(static value => value%2!=0).ToList();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int SumImpl() => Source().Sum();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static Dictionary<int,int> ToDictionaryImpl() =>
        Source().ToDictionary(static value => value,static value => value*2);

    private static global::Seamless.Tsavorite.Collections.HashSet<int> Source() =>
        source??throw new InvalidOperationException("Source was not initialized.");

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

    private static void AssertNativeMaterializeGenerated(string methodName,string shape) {
        var generated = GeneratedInterceptorAssert.SourceFor<FLinqNativeIterateTests>(methodName);
        Assert.Contains("IHashSetIterateStrategy",generated);
        Assert.Contains(".Iterate(ref __TsavoriteHashSetIterateState",generated);
        Assert.Contains(shape,generated);
        Assert.DoesNotContain("ScanCursor",generated);
        Assert.DoesNotContain("Seamless.Linq.Enumerable",generated);
        Assert.DoesNotContain("__chunk",generated,StringComparison.OrdinalIgnoreCase);
    }
}
