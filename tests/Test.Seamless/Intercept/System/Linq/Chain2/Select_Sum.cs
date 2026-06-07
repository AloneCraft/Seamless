using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain2;

// 2 個目がスカラー終端演算子（Sum）のケース。
public sealed class Select_Sum:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() => StaticImpl();

    private static void StaticImpl() {
        var expected=SystemEnumerable.Sum(SystemEnumerable.Select(LinqChain2TestBase.Source(),static v => v*2));
        var actual=LinqChain2TestBase.Source().Select(static v => v*2).Sum();
        Assert.Equal(expected,actual);
    }
}
