using System.Linq;
using System.Reflection;
using SeamlessEnumerable = Seamless.Linq.Enumerable;
using SeamlessHashSet = Seamless.Linq.HashSet;

namespace Test.Seamless.Intersept.Seamless.Linq;

public sealed class HashSet2 {
    public int Smoke(global::Seamless.Collections.IHashSet<int> source) {
        var total = 0;
        total+=Where_Select(source).Sum();
        total+=Select_Where(source).Sum();
        return total;
    }

    public IEnumerable<int> Where_Select(global::Seamless.Collections.IHashSet<int> source) => SeamlessEnumerable.Select(SeamlessHashSet.Where(source,static value => value%2==0),static value => value*10);

    public IEnumerable<int> Select_Where(global::Seamless.Collections.IHashSet<int> source) => SeamlessEnumerable.Where(SeamlessHashSet.Select(source,static value => value*10),static value => value>20);

    [Fact, Trait("Category","Unit")]
    public void SmokeTest() {
        using var store = global::Seamless.Stores.HashStore.Create().CreateManagedHashSet<int>();
        using var source = store.Create();
        foreach(var value in new[] { 1,2,3,4 }) {
            source.Add(value);
        }
        var sample = new HashSet2();

        Assert.Equal(130,sample.Smoke(source));
        Assert.True(HasGeneratedInterceptor(nameof(Where_Select)));
        Assert.True(HasGeneratedInterceptor(nameof(Select_Where)));
    }

    private static bool HasGeneratedInterceptor(string methodName) {
        var interceptorType = typeof(HashSet2).Assembly.GetType("Seamless.SourceGenerator.Generated.ContainerMethodInterceptors");
        Assert.NotNull(interceptorType);
        return interceptorType.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
            .Any(method => method.Name.Contains($"HashSet2_{methodName}_",StringComparison.Ordinal));
    }
}
