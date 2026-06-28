using Seamless.FLinq;
using Test.System.Linq.Intercept;

namespace Test.Seamless.Tsavorite.Linq.Enumerable;

public class FLinqNativeScanTests {
    private static global::Seamless.Tsavorite.Collections.HashSet<int>? source;

    [Fact, Trait("Category","Unit")]
    public void TsavoriteHashSetRoot_UsesNativeScanTerminalForCount() {
        using var store = Store.Create().CreateHashSet<int>();
        using var set = store.Create();
        foreach(var value in new[] { 1,2,3,4 })
            set.Add(value);

        source=set;
        try {
            Assert.Equal(2,CountEvenImpl());

            var generated = GeneratedInterceptorAssert.SourceFor<FLinqNativeScanTests>(nameof(CountEvenImpl));
            Assert.Contains("TsavoriteHashSetScanState",generated);
            Assert.Contains("HashSetScanUnsafe",generated);
            Assert.DoesNotContain("MemoryExtensions.AsSpan",generated);
            Assert.DoesNotContain("CollectionsMarshal.AsSpan",generated);
            Assert.DoesNotContain(".GetSpan();",generated);
        }
        finally {
            source=null;
        }
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int CountEvenImpl() =>
        Source().Where(static value => value%2==0).Count();

    private static global::Seamless.Tsavorite.Collections.HashSet<int> Source() =>
        source ?? throw new InvalidOperationException("Source was not initialized.");
}
