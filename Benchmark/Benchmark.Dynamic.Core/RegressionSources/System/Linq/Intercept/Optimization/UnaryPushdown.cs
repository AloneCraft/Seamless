namespace Test.System.Linq.Intercept.Optimization;

// 単項演算(Distinct/Reverse/Order 系)の前へ Where を押し下げたときの意味保存を検証する。
//
// Distinct は要素そのものを比較キーにするため、同じキー(=値)の要素は必ず同じ述語結果を持つ。
// どれが代表として残っても結果が変わらないので σ_p(δ(A)) = δ(σ_p(A)) が成り立つ。
// DistinctBy は逆にこの前提を満たさない(SetOperationPushdown 側の退行テストを参照)。
//
// Reverse / Order 系は要素数も要素の集合も変えないので σ_p(f(A)) = f(σ_p(A)) は自明に成り立つ。
// Order はソースが span を出せても押し下げる。並べ替えの段は溜めた値が既に並んでいれば並べ替えを省くので、
// 比較で等しい要素(区別できる参照型)の元の順が保たれること(安定性)も確かめる。
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

    // ソースが配列でも押し下げる。押し下げの有無に関わらず結果は同じであることを固定する。
    [Fact, Trait("Category","Unit")]
    public static void OrderOnArrayIsPushed() {
        var expected = Values().Order().Where(static v => v%2==0).ToList();
        AssertSequenceEqual(expected,OrderOnArrayIsPushedOptimized());
        AssertSequenceEqual(expected,OrderOnArrayIsPushedNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> OrderOnArrayIsPushedOptimized() =>
        Values().Order().Where(static v => v%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> OrderOnArrayIsPushedNoOptimized() =>
        Values().Order().Where(static v => v%2==0).ToList();

    // Order のソースが LINQ 呼び出しでも押し下がる。Skip(0) は AlgebraicRewriter の
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

    // 比較では Value だけを見る、区別できる要素。等しい Value の要素は元の順で出なければならない(安定な並べ替え)。
    public sealed record Tagged(int Value,int Tag):IComparable<Tagged> {
        public int CompareTo(Tagged? other) => this.Value.CompareTo(other!.Value);
    }

    // 既に昇順(重複あり)・既に降順(重複あり)・並んでいない、の3通り。
    private static Tagged[] Ascending() => [new(1,0),new(2,1),new(2,2),new(4,3),new(4,4),new(6,5)];
    private static Tagged[] Descending() => [new(6,0),new(4,1),new(4,2),new(2,3),new(2,4),new(1,5)];
    private static Tagged[] Unsorted() => [new(4,0),new(2,1),new(6,2),new(2,3),new(4,4),new(1,5)];

    [Fact, Trait("Category","Unit")]
    public static void OrderOfTaggedKeepsEqualElementsInSourceOrder() {
        foreach(var source in new Func<Tagged[]>[] { Ascending,Descending,Unsorted }) {
            AssertSequenceEqual(source().Order().Where(static v => v.Value%2==0).ToList(),OrderTaggedOptimized(source()));
            AssertSequenceEqual(source().OrderDescending().Where(static v => v.Value%2==0).ToList(),OrderDescendingTaggedOptimized(source()));
            AssertSequenceEqual(source().OrderBy(static v => v).Where(static v => v.Value>1).ToList(),OrderByIdentityTaggedOptimized(source()));
        }
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Tagged> OrderTaggedOptimized(Tagged[] source) =>
        source.Order().Where(static v => v.Value%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Tagged> OrderDescendingTaggedOptimized(Tagged[] source) =>
        source.OrderDescending().Where(static v => v.Value%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Tagged> OrderByIdentityTaggedOptimized(Tagged[] source) =>
        source.OrderBy(static v => v).Where(static v => v.Value>1).ToList();
}
