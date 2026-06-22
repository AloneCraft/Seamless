using Seamless.FLinq;
using Test.System.Linq.Intercept;

namespace Test.Seamless.Persisted.Linq.Enumerable;

public class FLinqNativeScanTests {
    private static global::Seamless.Persisted.Collections.Set<int>? source;

    [Fact, Trait("Category","Unit")]
    public void PersistedSetRoot_UsesNativeScanTerminalForAny() {
        var path = CreatePath();
        try {
            using var set = new global::Seamless.Persisted.Collections.Set<int>(path,initialCapacity: 1,initialDataCapacity: 128);
            foreach(var value in new[] { 1,2,3,4 })
                set.Add(value);
            set.Compact();

            source=set;
            Assert.True(AnyGreaterThanThreeImpl());

            var generated = GeneratedInterceptorAssert.SourceFor<FLinqNativeScanTests>(nameof(AnyGreaterThanThreeImpl));
            Assert.Contains("PersistedSetScanState",generated);
            Assert.Contains("PersistedSetScanUnsafe",generated);
        }
        finally {
            source=null;
            TryDeleteDirectory(path);
        }
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static bool AnyGreaterThanThreeImpl() =>
        Source().Where(static value => value>3).Any();

    private static global::Seamless.Persisted.Collections.Set<int> Source() =>
        source ?? throw new InvalidOperationException("Source was not initialized.");

    private static string CreatePath() {
        var directoryPath = TestPath.Directory<FLinqNativeScanTests>("native-scan");
        return Path.Combine(directoryPath,"items.dat");
    }

    private static void TryDeleteDirectory(string path) {
        var directoryPath = Path.GetDirectoryName(path);
        if(string.IsNullOrEmpty(directoryPath))
            return;
        try {
            Directory.Delete(directoryPath,recursive: true);
        }
        catch(IOException) {
        }
        catch(UnauthorizedAccessException) {
        }
    }
}
