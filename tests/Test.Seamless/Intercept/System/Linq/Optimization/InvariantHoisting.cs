using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Optimization;

// ルール §1: 多階層ループ不変式ホイスト + static ラムダのキャプチャ除去。
// どの階層へ持ち上げられても結果は System.Linq と一致しなければならない。
public sealed class InvariantHoisting:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void GlobalInvariant() => GlobalInvariantImpl();

    private static void GlobalInvariantImpl(){
        {
            // 非空: a に依存しない StaticCheck() は完全に巻き上げ可能。
            var expected=SystemEnumerable.Select(Outer(),static a => a+StaticCheck()).ToList();
            var actual=new Consumer<int>();
            Outer().Select(static a => a+StaticCheck()).Consume(actual);
            AssertSequenceEqual(expected,actual.Items);
        }
        {
            // 空: 巻き上げても 1 件も出ないこと。
            var expected=SystemEnumerable.Select(EmptyOuter(),static a => a+StaticCheck()).ToList();
            var actual=new Consumer<int>();
            EmptyOuter().Select(static a => a+StaticCheck()).Consume(actual);
            AssertSequenceEqual(expected,actual.Items);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void IntermediateScopeInvariant() => IntermediateScopeInvariantImpl();

    private static void IntermediateScopeInvariantImpl(){
        {
            // a+StaticCheck() は外側 a のみ依存（中間スコープへ）、b*StackCheck() は global。
            var expected=SystemEnumerable.SelectMany(Outer(),
                static a => SystemEnumerable.Select(Inner(),b => (a+StaticCheck())-(b*StackCheck()))).ToList();
            var actual=new Consumer<int>();
            Outer().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).Consume(actual);
            AssertSequenceEqual(expected,actual.Items);
        }
        {
            // 外側が空なら内側シーケンスは一度も生成されない。
            var expected=SystemEnumerable.SelectMany(EmptyOuter(),
                static a => SystemEnumerable.Select(Inner(),b => (a+StaticCheck())-(b*StackCheck()))).ToList();
            var actual=new Consumer<int>();
            EmptyOuter().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).Consume(actual);
            AssertSequenceEqual(expected,actual.Items);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void CapturedLocalEliminatedToStructField() => CapturedLocalEliminatedToStructFieldImpl();

    private static void CapturedLocalEliminatedToStructFieldImpl(){
        // キャプチャされたローカルは display class でなく struct フィールドへ。結果不変を確認。
        var threshold=3;
        var expected=SystemEnumerable.Where(Outer(),v => v<threshold).ToList();
        var actual=new Consumer<int>();
        Outer().Where(v => v<threshold).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void InvariantNotHoistedPastLazyGuard() => InvariantNotHoistedPastLazyGuardImpl();

    private static void InvariantNotHoistedPastLazyGuardImpl(){
        // ルール §4 null/例外安全: 不変式 100/divisor は本来ループ内で初めて評価される。
        // ソースが空なら一度も評価されず例外も出ない。先行（ループ外）評価へ巻き上げると
        // DivideByZeroException になり意味が変わる。両者とも空で例外なしを確認する。
        var divisor=0;
        var expected=SystemEnumerable.Where(EmptyOuter(),v => v>100/divisor).ToList();
        var actual=new Consumer<int>();
        EmptyOuter().Where(v => v>100/divisor).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
