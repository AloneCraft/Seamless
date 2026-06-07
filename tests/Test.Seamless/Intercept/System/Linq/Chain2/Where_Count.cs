using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain2;

// 2 個目がスカラー終端演算子（Count）のケース。
public sealed class Where_Count:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() => StaticImpl();

    private static void StaticImpl() {
        var expected=SystemEnumerable.Count(SystemEnumerable.Where(LinqChain2TestBase.Source(),static v => v>2));
        var actual=LinqChain2TestBase.Source().Where(static v => v>2).Count();
        Assert.Equal(expected,actual);
    }
}
