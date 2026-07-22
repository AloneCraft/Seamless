using Seamless.Linq.Shared;

namespace Test.Persisted.Linq.Enumerable;

public sealed class IEnumerableRootTests {
    private static global::Seamless.Persisted.Collections.Set<int>? source;

    [Fact,Trait("Category","Unit")]
    public void PersistedSetRoute_UsesIEnumerableRootWithoutNativeIterateState() {
        var path=Path.Combine(Path.GetTempPath(),"Seamless-IEnumerableRoot-"+Guid.NewGuid().ToString("N"),"items.dat");
        try {
            using var set=global::Seamless.Persisted.Collections.Store.CreateSet<int>(path,InitialCapacity:1,InitialDataCapacity:128);
            foreach(var value in new[]{1,2,3,4})
                set.Add(value);
            set.Compact();
            source=set;

            Assert.Equal(2,CountEvenImpl());
            var generated=GeneratedFor(nameof(CountEvenImpl));
            Assert.DoesNotContain("PersistedSetIterateState",generated);
            Assert.DoesNotContain("ISetIterateStrategy",generated);
            Assert.DoesNotContain(".Iterate(ref __PersistedSetIterateState",generated);
        } finally {
            source=null;
            var directory=Path.GetDirectoryName(path);
            if(!string.IsNullOrWhiteSpace(directory)&&Directory.Exists(directory))
                Directory.Delete(directory,recursive:true);
        }
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int CountEvenImpl()=>Source().AsX().Count(static value=>value%2==0);

    private static global::Seamless.Persisted.Collections.Set<int> Source()=>source??throw new InvalidOperationException("Source was not initialized.");
    private static string GeneratedFor(string methodName) {
        var project=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..",".."));
        var root=Path.Combine(project,"obj","Release","net10.0","generated");
        foreach(var file in Directory.EnumerateFiles(root,"*.g.cs",SearchOption.AllDirectories)) {
            var source=File.ReadAllText(file);
            if(source.Contains("class IEnumerableRootTests",StringComparison.Ordinal)
                &&source.Contains("<c>"+methodName+"()</c>",StringComparison.Ordinal)) {
                return source;
            }
        }
        throw new InvalidOperationException("Generated source was not found.");
    }
}
