using System.Linq;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Chain2;

public sealed record Chain2Row(int Key,int Value,string Name);

// 2 つの LINQ メソッドを連鎖させるテストの共通基盤。シーケンス連鎖は `.Consume()` 終端、
// 2 個目が終端演算子（ToArray/ToList 等）の場合はそのまま materialize して比較する。
public abstract class LinqChain2TestBase {
    public static int[] Source() => [1,2,3,4,5,6];
    public static int[] Second() => [3,4,5];
    public static Chain2Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma"),new(1,40,"delta")];

    public static void AssertSequenceEqual<T>(IEnumerable<T> expected,IEnumerable<T> actual) {
        Assert.Equal(expected,actual);
    }
}
