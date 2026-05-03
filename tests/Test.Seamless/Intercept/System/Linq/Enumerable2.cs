using System.Linq;
using System.Reflection;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class Enumerable2 {
    public int Smoke() {
        var total = 0;
        total+=Where_Select(Source()).Sum();
        total+=Select_Where(Source()).Sum();
        return total;
    }

    public IEnumerable<int> Where_Select(int[] source) => SystemEnumerable.Select(SystemEnumerable.Where(source,static value => value%2==0),static value => value*10);

    public IEnumerable<int> Select_Where(int[] source) => SystemEnumerable.Where(SystemEnumerable.Select(source,static value => value*10),static value => value>20);

    public static int[] Source() => [1,2,3,4];

    [Fact, Trait("Category","Unit")]
    public void SmokeTest() {
        var sample = new Enumerable2();

        Assert.Equal(130,sample.Smoke());
        Assert.True(HasGeneratedInterceptor(nameof(Where_Select)));
        Assert.True(HasGeneratedInterceptor(nameof(Select_Where)));
    }

    private static bool HasGeneratedInterceptor(string methodName) {
        var interceptorType = typeof(Enumerable2).Assembly.GetType("Seamless.SourceGenerator.Generated.LinqInvocationInterceptors");
        Assert.NotNull(interceptorType);
        return interceptorType.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
            .Any(method => method.Name.Contains($"Enumerable2_{methodName}_",StringComparison.Ordinal));
    }
}
