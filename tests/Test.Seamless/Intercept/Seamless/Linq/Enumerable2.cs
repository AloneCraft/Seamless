using System.Linq;
using System.Reflection;
using SeamlessEnumerable = Seamless.Linq.Enumerable;

namespace Test.Seamless.Intersept.Seamless.Linq;

public sealed class Enumerable2 {
    public int Smoke() {
        var total = 0;
        total+=Where_Select(Source()).Sum();
        total+=Select_Where(Source()).Sum();
        return total;
    }

    private static IEnumerable<int> Where_Select(int[] source) => SeamlessEnumerable.Select(SeamlessEnumerable.Where(source,static value => value%2==0),static value => value*10);

    private static IEnumerable<int> Select_Where(int[] source) => SeamlessEnumerable.Where(SeamlessEnumerable.Select(source,static value => value*10),static value => value>20);

    public static int[] Source() => [1,2,3,4];

    [Fact, Trait("Category","Unit")]
    public void SmokeTest() {
        var sample = new Enumerable2();

        Assert.Equal(130,sample.Smoke());
        Assert.True(HasGeneratedInterceptor(nameof(Where_Select)));
        Assert.True(HasGeneratedInterceptor(nameof(Select_Where)));
    }

    private static bool HasGeneratedInterceptor(string methodName) {
        var interceptorType = typeof(Enumerable2).Assembly.GetType("Seamless.SourceGenerator.Generated.ContainerMethodInterceptors");
        Assert.NotNull(interceptorType);
        return interceptorType.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
            .Any(method => method.Name.Contains($"Enumerable2_{methodName}_",StringComparison.Ordinal));
    }
}
