using SeamlessEnumerable = Seamless.Linq.Enumerable;
using System.Linq;
using System.Reflection;

namespace Test.Seamless.Intersept.Seamless.Linq;

public sealed class EnumerablePattern {
    public int Total { get; private set; }

    internal void Test0(int[] source) {
        Call(SeamlessEnumerable.Where(source,p => p%2==0));
    }

    internal void Call(IEnumerable<int> values) {
        this.Total=SeamlessEnumerable.Sum(values);
    }

    [Fact, Trait("Category","Unit")]
    public void SmokeTest() {
        var sample = new EnumerablePattern();

        sample.Test0([1,2,3,4]);

        Assert.Equal(6,sample.Total);
        Assert.True(HasGeneratedInterceptor(nameof(Test0)));
    }

    private static bool HasGeneratedInterceptor(string methodName) {
        var interceptorType = typeof(EnumerablePattern).Assembly.GetType("Seamless.SourceGenerator.Generated.ContainerMethodInterceptors");
        Assert.NotNull(interceptorType);
        return interceptorType.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
            .Any(method => method.Name.Contains($"EnumerablePattern_{methodName}_",StringComparison.Ordinal));
    }
}
