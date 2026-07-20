using Seamless.FLinq;

using Test.System.Linq.Intercept;

namespace Test.Seamless.Persisted.Linq.Enumerable;

// bare Count（predicate なし）専用。GeneratedInterceptorAssert.SourceFor は生成元 .cs ファイル単位の
// 生成コードをまとめて返すため、native iterate を使う predicate 付き Count（FLinqPersistedSetCountPredicateTests、
// 別ファイル）と同じソースファイルに置くと DoesNotContain が誤検知する。ファイルごと分離する。
public class FLinqPersistedSetCountTests {
    private static global::Seamless.Persisted.Collections.Set<int>? source;

    [Fact, Trait("Category","Unit")]
    public void PersistedSetRoot_Count_UsesNonEnumeratedCountExpressionInsteadOfNativeIterate() {
        var path = CreatePath();
        try {
            using var set = global::Seamless.Persisted.Collections.Store.CreateSet<int>(path,InitialCapacity: 1,InitialDataCapacity: 128);
            foreach(var value in new[] { 1,2,3,4 })
                set.Add(value);
            set.Compact();

            source=set;
            Assert.Equal(4,CountImpl());

            // native iterate（要素を1つずつ materialize する ISetIterateStrategy 経由）ではなく、
            // Set<T>.Count を直接読む O(1) inline path が選ばれていることをソース形状で確認する。
            var generated = GeneratedInterceptorAssert.SourceFor<FLinqPersistedSetCountTests>(nameof(CountImpl));
            Assert.Contains(".Count)",generated);
            Assert.DoesNotContain("PersistedSetIterateState",generated);
            Assert.DoesNotContain("ISetIterateStrategy",generated);
            Assert.DoesNotContain(".Iterate(ref",generated);
        } finally {
            source=null;
            TryDeleteDirectory(path);
        }
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int CountImpl() =>
        Source().Count();

    private static global::Seamless.Persisted.Collections.Set<int> Source() =>
        source??throw new InvalidOperationException("Source was not initialized.");

    private static string CreatePath() {
        var directoryPath = TestPath.Directory<FLinqPersistedSetCountTests>("persisted-set-count");
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
