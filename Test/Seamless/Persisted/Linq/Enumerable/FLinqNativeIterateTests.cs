using Seamless.FLinq;

using Test.System.Linq.Intercept;

namespace Test.Seamless.Persisted.Linq.Enumerable;

public class FLinqNativeIterateTests {
    private static global::Seamless.Persisted.Collections.Set<int>? source;

    [Fact, Trait("Category","Unit")]
    public void PersistedSetRoot_UsesNativeIterateTerminalForAny() {
        var path = CreatePath();
        try {
            using var set = global::Seamless.Persisted.Collections.Store.CreateSet<int>(path,InitialCapacity: 1,InitialDataCapacity: 128);
            foreach(var value in new[] { 1,2,3,4 })
                set.Add(value);
            set.Compact();

            source=set;
            Assert.True(AnyGreaterThanThreeImpl());

            var generated = GeneratedInterceptorAssert.SourceFor<FLinqNativeIterateTests>(nameof(AnyGreaterThanThreeImpl));
            Assert.Contains("PersistedSetIterateState",generated);
            Assert.Contains("ISetIterateStrategy",generated);
            Assert.Contains(".Iterate(ref __PersistedSetIterateState",generated);
            Assert.DoesNotContain("PersistedSetScanUnsafe",generated);
            Assert.DoesNotContain("Seamless.Linq.Enumerable",generated);
            Assert.DoesNotContain("MemoryExtensions.AsSpan",generated);
            Assert.DoesNotContain("CollectionsMarshal.AsSpan",generated);
            Assert.DoesNotContain(".GetSpan();",generated);
        } finally {
            source=null;
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void PersistedSetRoot_UsesNativeIterateTerminalForAllAndExplicitFirstOrDefault() {
        var path = CreatePath();
        try {
            using var set = global::Seamless.Persisted.Collections.Store.CreateSet<int>(path,InitialCapacity: 1,InitialDataCapacity: 128);
            foreach(var value in new[] { 1,2,3,4 })
                set.Add(value);
            set.Compact();

            source=set;
            Assert.True(AllLessThanFiveImpl());
            // 順序なしSetへの直接的な順序依存演算は、以前はFLinq側のFLINQ003/004でランタイム例外を投げていたが、
            // その判定はSeamless.Linq.Analyzers側のコンパイル時診断(SEAMLINQ001/002)へ移管された。
            // FLinqのnative iterate自体は AsEnumerable() の有無に関わらず同じ結果を返す。
            Assert.Equal(3,DirectFirstOrDefaultGreaterThanTwoImpl());
            Assert.Equal(3,ExplicitFirstOrDefaultGreaterThanTwoImpl());

            AssertNativeIterateGenerated(nameof(AllLessThanFiveImpl));
            AssertNativeIterateGenerated(nameof(ExplicitFirstOrDefaultGreaterThanTwoImpl));
        } finally {
            source=null;
            TryDeleteDirectory(path);
        }
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool AnyGreaterThanThreeImpl() =>
        Source().Where(static value => value>3).Any();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool AllLessThanFiveImpl() =>
        Source().All(static value => value<5);

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int DirectFirstOrDefaultGreaterThanTwoImpl() =>
        Source().Where(static value => value>2).FirstOrDefault();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int ExplicitFirstOrDefaultGreaterThanTwoImpl() =>
        Source().AsEnumerable().Where(static value => value>2).FirstOrDefault();

    private static global::Seamless.Persisted.Collections.Set<int> Source() =>
        source??throw new InvalidOperationException("Source was not initialized.");

    private static void AssertNativeIterateGenerated(string methodName) {
        var generated = GeneratedInterceptorAssert.SourceFor<FLinqNativeIterateTests>(methodName);
        Assert.Contains("PersistedSetIterateState",generated);
        Assert.Contains("ISetIterateStrategy",generated);
        Assert.Contains(".Iterate(ref __PersistedSetIterateState",generated);
        Assert.DoesNotContain("PersistedSetScanUnsafe",generated);
        Assert.DoesNotContain("Seamless.Linq.Enumerable",generated);
    }

    private static string CreatePath() {
        var directoryPath = TestPath.Directory<FLinqNativeIterateTests>("native-iterate");
        return Path.Combine(directoryPath,"items.dat");
    }

    private static void TryDeleteDirectory(string path) {
        var directoryPath = Path.GetDirectoryName(path);
        if(string.IsNullOrEmpty(directoryPath))
            return;
        try {
            Directory.Delete(directoryPath,recursive: true);
        } catch(IOException) {
        } catch(UnauthorizedAccessException) {
        }
    }
}
