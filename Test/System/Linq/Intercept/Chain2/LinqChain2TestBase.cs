using System.Linq;

using Tsavorite;

namespace Test.System.Linq.Intercept.Chain2;

public sealed record Chain2Row(int Key,int Value,string Name);

// 2 つの LINQ メソッドを連鎖させるテストの共通基盤。シーケンス連鎖は `.Consume()` 終端、
// 2 個目が終端演算子（ToArray/ToList 等）の場合はそのまま materialize して比較する。
public abstract class LinqChain2TestBase(ITestOutputHelper output) {
    public static int[] Source() => [1,2,3,4,5,6];
    public static int[] Second() => [3,4,5];
    public static Chain2Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma"),new(1,40,"delta")];

    public static void AssertSequenceEqual<T>(IEnumerable<T> expected,IEnumerable<T> actual) {
        Assert.Equal(expected,actual);
    }

    public void AssertOptimized<T>(string label,T expected,Func<T> actualFactory) {
        using var scope=LinqCounterScope.Start();
        var actual=actualFactory();
        LinqCounterDiagnostics.EmitActual(label,scope.Counter);
        this.WriteCounterMessage(label,scope.Counter);
        Assert.Equal(expected,actual);
    }

    public void MeasureOptimized(string label,Action action) {
        using var scope=LinqCounterScope.Start();
        action();
        LinqCounterDiagnostics.EmitActual(label,scope.Counter);
        this.WriteCounterMessage(label,scope.Counter);
    }

    private void WriteCounterMessage(string label,LinqCounter counter) {
        var message=$"{label}: delegate(log/phys)={counter.LogicalDelegate}/{counter.PhysicalDelegate}; logical ops={counter.LogicalOperations}, moveNext={counter.PhysicalMoveNext}";
        output.WriteLine(message);
        global::System.Console.WriteLine(message);
        global::System.Diagnostics.Trace.WriteLine(message);
    }
}
