using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Chain2;

// 2 個目が終端演算子（ToArray）のケース。materialize して直接比較する。
public sealed class Where_ToArray:LinqChain2TestBase {
    [Fact, Trait("Category","Unit")]
    public static void Static() => StaticImpl();

    private static void StaticImpl() {
        var expected=SystemEnumerable.ToArray(SystemEnumerable.Where(LinqChain2TestBase.Source(),static v => v>2));
        var actual=LinqChain2TestBase.Source().Where(static v => v>2).ToArray();
        LinqChain2TestBase.AssertSequenceEqual(expected,actual);
    }
}
