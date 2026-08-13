using Seamless.FLinq;

using Test.System.Linq.Intercept;

namespace Test.Seamless.Persisted.Linq.Enumerable;

public class FLinqNativeIterateTests {
    private static global::Seamless.Collections.Persisted.Set<int>? source;

    [Fact, Trait("Category","Unit")]
    public void PersistedSetRoot_UsesNativeIterateTerminalForAny() {
        var path = CreatePath();
        try {
            using var set = global::Seamless.Collections.Persisted.Store.CreateSet<int>(path,InitialCapacity: 1,InitialDataCapacity: 128);
            foreach(var value in new[] { 1,2,3,4 })
                set.Add(value);
            set.Compact();

            source=set;
            Assert.True(AnyGreaterThanThreeImpl());

            AssertNativeIterateGenerated(nameof(AnyGreaterThanThreeImpl));
        } finally {
            source=null;
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void PersistedSetRoot_UsesNativeIterateTerminalForAllAndExplicitFirstOrDefault() {
        var path = CreatePath();
        try {
            using var set = global::Seamless.Collections.Persisted.Store.CreateSet<int>(path,InitialCapacity: 1,InitialDataCapacity: 128);
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

    [Fact, Trait("Category","Unit")]
    public void PersistedSetRoot_UsesNativeLookupForContains() {
        var path = CreatePath();
        try {
            using var set = global::Seamless.Collections.Persisted.Store.CreateSet<int>(path,InitialCapacity: 1,InitialDataCapacity: 128);
            foreach(var value in new[] { 1,2,3,4 })
                set.Add(value);
            set.Compact();

            source=set;
            {
                //要素を変えない段しか無く comparer も無い Contains は .hindex による O(1) 直接検索になる
                Assert.True(ContainsThreeImpl());
                Assert.False(ContainsNineImpl());

                var body = InterceptorBody(nameof(ContainsThreeImpl));
                Assert.Contains(".Contains(3)",body);
                Assert.DoesNotContain("PersistedSetIterateState",body);
                Assert.DoesNotContain(".Iterate(ref",body);
            }
            {
                //段が挟まると集合自身の等価では答えられないので従来の native iterate へ落ちる
                Assert.True(WhereThenContainsImpl());

                var body = InterceptorBody(nameof(WhereThenContainsImpl));
                Assert.Contains("PersistedSetIterateState",body);
                Assert.Contains(".Iterate(ref __PersistedSetIterateState",body);
            }
        } finally {
            source=null;
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void PersistedSetRoot_UsesNativeIterateTerminalForCastAndOfType() {
        var path = CreatePath();
        try {
            using var set = global::Seamless.Collections.Persisted.Store.CreateSet<int>(path,InitialCapacity: 1,InitialDataCapacity: 128);
            foreach(var value in new[] { 1,2,3,4 })
                set.Add(value);
            set.Compact();

            source=set;
            {
                //Cast は順序に依存しないので unordered な provider scan へ載る
                Assert.Equal(4,CastToObjectCountImpl());

                var body = InterceptorBody(nameof(CastToObjectCountImpl));
                Assert.Contains("PersistedSetIterateState",body);
                Assert.Contains(".Iterate(ref __PersistedSetIterateState",body);
            }
            {
                //OfType も同様。スキップは per-item コールバックの return true で表現される
                Assert.Equal(4,OfTypeIntCountImpl());

                var body = InterceptorBody(nameof(OfTypeIntCountImpl));
                Assert.Contains("PersistedSetIterateState",body);
                Assert.Contains(".Iterate(ref __PersistedSetIterateState",body);
            }
        } finally {
            source=null;
            TryDeleteDirectory(path);
        }
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int CastToObjectCountImpl() =>
        Source().AsEnumerable().Cast<object>().Count();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int OfTypeIntCountImpl() =>
        Source().AsEnumerable().OfType<int>().Count();

    // Source().Contains(x) は Set<T> 自身のインスタンスメソッドに束縛されるため LINQ 介入自体が起きない。
    // FLinq が介入するのは AsEnumerable() を挟んで IEnumerable<T> として見える形。
    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool ContainsThreeImpl() =>
        Source().AsEnumerable().Contains(3);

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool ContainsNineImpl() =>
        Source().AsEnumerable().Contains(9);

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool WhereThenContainsImpl() =>
        Source().AsEnumerable().Where(static value => value>2).Contains(3);

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

    private static global::Seamless.Collections.Persisted.Set<int> Source() =>
        source??throw new InvalidOperationException("Source was not initialized.");

    // SourceFor はファイル全体を返すため、他メソッド分の生成コードが混ざる。
    // 「この経路では出ないこと」を検証するには対象インターセプターの本体だけを切り出す必要がある。
    private static string InterceptorBody(string methodName) {
        var generated = GeneratedInterceptorAssert.SourceFor<FLinqNativeIterateTests>(methodName);
        var marker = $"<c>{methodName}()</c>";
        var start = generated.IndexOf(marker,StringComparison.Ordinal);
        Assert.True(start>=0,$"Interceptor for {methodName} was not found in the generated source.");
        var end = generated.IndexOf("/// <summary>",start,StringComparison.Ordinal);
        if(end<0) {
            end=generated.Length;
        }
        return generated[start..end];
    }

    private static void AssertNativeIterateGenerated(string methodName) {
        var body = InterceptorBody(methodName);
        Assert.Contains("PersistedSetIterateState",body);
        Assert.Contains(".Iterate(ref __PersistedSetIterateState",body);
        Assert.DoesNotContain("PersistedSetScanUnsafe",body);
        Assert.DoesNotContain("Seamless.Linq.Enumerable",body);
        Assert.DoesNotContain("MemoryExtensions.AsSpan",body);
        Assert.DoesNotContain("CollectionsMarshal.AsSpan",body);
        Assert.DoesNotContain(".GetSpan();",body);
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
