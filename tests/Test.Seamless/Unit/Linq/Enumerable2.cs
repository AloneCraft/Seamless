#if TSAVORITE1_1_1
using Test.Seamless.Benchmarks.Simple;

namespace Test.Seamless.Unit.Linq;

public sealed class Enumerable2 {
    [Fact, Trait("Category","Unit")]
    public void QueryExtensionsMatchOriginalMethods() {
        var container = new Container();
        var outer = new[] { 1,2,3 };
        var inner = new[] { "a","b" };

        Assert.Equal(
            container.SelectManyQuery(outer,inner),
            container.SelectManyQueryAsExtension(outer,inner));
        Assert.Equal(
            container.WhereSelectQuery(outer,2),
            container.WhereSelectQueryAsExtension(outer,2));
    }

    [Fact, Trait("Category","Unit")]
    public void MethodChainExtensionsMatchOriginalMethods() {
        var container = new Container();
        var outer = new[] { 1,2,3,2 };
        var inner = new[] { 10,20 };

        Assert.Equal(
            container.SelectManyMethod(outer,inner),
            container.SelectManyMethodAsExtension(outer,inner));
        Assert.Equal(
            container.WhereSelectMethod(outer,2),
            container.WhereSelectMethodAsExtension(outer,2));
    }
}
#endif
