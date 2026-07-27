using Shared.Linq;

namespace Test.Tsavorite.Linq.Enumerable.IEnumerable.FLinq;

public sealed class IEnumerableRootTests {
    private static global::Seamless.Collections.Tsavorite.HashSet<int>? source;

    [Fact,Trait("Category","Unit")]
    public void TsavoriteHashSetRoute_UsesIEnumerableRootWithoutNativeIterateState() {
        using var store=global::Seamless.Collections.Tsavorite.Store.Create().CreateHashSet<int>();
        using var set=store.Create();
        foreach(var value in new[]{1,2,3,4})
            set.Add(value);
        source=set;
        try {
            Assert.Equal(2,CountEvenImpl());
            var generated=GeneratedFor(nameof(CountEvenImpl));
            Assert.DoesNotContain("TsavoriteHashSetIterateState",generated);
            Assert.DoesNotContain("IHashSetIterateStrategy",generated);
            Assert.DoesNotContain(".Iterate(ref __TsavoriteHashSetIterateState",generated);
        } finally {
            source=null;
        }
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int CountEvenImpl()=>Source().AsX().Count(static value=>value%2==0);

    private static global::Seamless.Collections.Tsavorite.HashSet<int> Source()=>source??throw new InvalidOperationException("Source was not initialized.");
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
