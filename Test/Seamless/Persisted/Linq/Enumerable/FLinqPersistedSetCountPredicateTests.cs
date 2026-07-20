using Seamless.FLinq;

using Test.System.Linq.Intercept;

namespace Test.Seamless.Persisted.Linq.Enumerable;

// predicate 付き Count 専用（FLinqPersistedSetCountTests.cs と同じ理由でファイルを分離）。
// 今回の bare Count 向け修正（ProviderIterateTerminalBase.CanCreateFor）が predicate 付きの
// native iterate 最適化を退行させていないことを確認する。
public class FLinqPersistedSetCountPredicateTests {
    private static global::Seamless.Persisted.Collections.Set<int>? source;

    [Fact, Trait("Category","Unit")]
    public void PersistedSetRoot_Count_predicate_StillUsesNativeIterateTerminal() {
        var path = CreatePath();
        try {
            using var set = global::Seamless.Persisted.Collections.Store.CreateSet<int>(path,InitialCapacity: 1,InitialDataCapacity: 128);
            foreach(var value in new[] { 1,2,3,4 })
                set.Add(value);
            set.Compact();

            source=set;
            Assert.Equal(2,CountEvenImpl());

            var generated = GeneratedInterceptorAssert.SourceFor<FLinqPersistedSetCountPredicateTests>(nameof(CountEvenImpl));
            Assert.Contains("PersistedSetIterateState",generated);
            Assert.Contains("ISetIterateStrategy",generated);
            Assert.Contains(".Iterate(ref __PersistedSetIterateState",generated);
        } finally {
            source=null;
            TryDeleteDirectory(path);
        }
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int CountEvenImpl() =>
        Source().Count(static value => value%2==0);

    private static global::Seamless.Persisted.Collections.Set<int> Source() =>
        source??throw new InvalidOperationException("Source was not initialized.");

    private static string CreatePath() {
        var directoryPath = TestPath.Directory<FLinqPersistedSetCountPredicateTests>("persisted-set-count-predicate");
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
