namespace Test.System.Linq.Intercept.Optimization;

// 単項演算(Distinct/Reverse/Order 系)の前へ Where を押し下げたときの意味保存を検証する。
//
// Distinct は要素そのものを比較キーにするため、同じキー(=値)の要素は必ず同じ述語結果を持つ。
// どれが代表として残っても結果が変わらないので σ_p(δ(A)) = δ(σ_p(A)) が成り立つ。
// DistinctBy は逆にこの前提を満たさない(SetOperationPushdown 側の退行テストを参照)。
//
// Reverse / Order 系は要素数も要素の集合も変えないので σ_p(f(A)) = f(σ_p(A)) は自明に成り立つ。
// ただし Order は押し下げるとソート済みスパン高速パスを失うため、OptimizeVisitor の
// KeepsSortedSpanFastPath が「ソースがコンパイル時にスパンを出せるなら押し下げない」で止める。
// OrderKeepsSortedSpanFastPath / OrderPushedWhenSourceIsLinqCall はガードの両側を固定する。
public sealed class UnaryPushdown:OptimizationTestBase {
    // 重複と非ソートの両方を含める。Distinct の代表選択と Order の並べ替えが同時に効く。
    private static int[] Values() => [7,3,7,4,1,4,8,2,8,5];

    [Fact, Trait("Category","Unit")]
    public static void DistinctBeforeWhere() {
        var expected = Values().Distinct().Where(static v => v%2==0).ToList();
        AssertSequenceEqual(expected,DistinctBeforeWhereOptimized());
        AssertSequenceEqual(expected,DistinctBeforeWhereNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> DistinctBeforeWhereOptimized() =>
        Values().Distinct().Where(static v => v%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> DistinctBeforeWhereNoOptimized() =>
        Values().Distinct().Where(static v => v%2==0).ToList();

    [Fact, Trait("Category","Unit")]
    public static void ReverseBeforeWhere() {
        var expected = Values().Reverse().Where(static v => v%2==0).ToList();
        AssertSequenceEqual(expected,ReverseBeforeWhereOptimized());
        AssertSequenceEqual(expected,ReverseBeforeWhereNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ReverseBeforeWhereOptimized() =>
        Values().Reverse().Where(static v => v%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ReverseBeforeWhereNoOptimized() =>
        Values().Reverse().Where(static v => v%2==0).ToList();

    // ソースが配列なのでガードが効き、押し下げは起きない。押し下げの有無に関わらず結果は同じであることを固定する。
    [Fact, Trait("Category","Unit")]
    public static void OrderKeepsSortedSpanFastPath() {
        var expected = Values().Order().Where(static v => v%2==0).ToList();
        AssertSequenceEqual(expected,OrderKeepsSortedSpanFastPathOptimized());
        AssertSequenceEqual(expected,OrderKeepsSortedSpanFastPathNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> OrderKeepsSortedSpanFastPathOptimized() =>
        Values().Order().Where(static v => v%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> OrderKeepsSortedSpanFastPathNoOptimized() =>
        Values().Order().Where(static v => v%2==0).ToList();

    // Order のソースが LINQ 呼び出しなのでガードが外れて押し下がる。Skip(0) は AlgebraicRewriter の
    // 除去対象(恒等 Select / AsEnumerable)ではないので上流ステージとして残る。
    [Fact, Trait("Category","Unit")]
    public static void OrderPushedWhenSourceIsLinqCall() {
        var expected = Values().Skip(0).Order().Where(static v => v%2==0).ToList();
        AssertSequenceEqual(expected,OrderPushedWhenSourceIsLinqCallOptimized());
        AssertSequenceEqual(expected,OrderPushedWhenSourceIsLinqCallNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> OrderPushedWhenSourceIsLinqCallOptimized() =>
        Values().Skip(0).Order().Where(static v => v%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> OrderPushedWhenSourceIsLinqCallNoOptimized() =>
        Values().Skip(0).Order().Where(static v => v%2==0).ToList();

    // 降順でも同じ。押し下げ後に並び順が変わらないことを固定する。
    [Fact, Trait("Category","Unit")]
    public static void OrderDescendingPushedWhenSourceIsLinqCall() {
        var expected = Values().Skip(0).OrderDescending().Where(static v => v%2==0).ToList();
        AssertSequenceEqual(expected,OrderDescendingPushedWhenSourceIsLinqCallOptimized());
        AssertSequenceEqual(expected,OrderDescendingPushedWhenSourceIsLinqCallNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> OrderDescendingPushedWhenSourceIsLinqCallOptimized() =>
        Values().Skip(0).OrderDescending().Where(static v => v%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> OrderDescendingPushedWhenSourceIsLinqCallNoOptimized() =>
        Values().Skip(0).OrderDescending().Where(static v => v%2==0).ToList();
}
