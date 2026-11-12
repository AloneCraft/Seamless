namespace Test.System.Linq.Intercept.Optimization;

// 集合演算(Union/Concat/Except/Intersect)への Where 分配の意味保存を検証する。
//
// Union/Concat/Except/Intersect は要素そのものを比較キーにするため、選択の分配則
//   σ_p(A∪B) = σ_p(A)∪σ_p(B) 、 σ_p(A−B) = σ_p(A)−B 、 σ_p(A∩B) = σ_p(A)∩B
// がそのまま成り立つ。同じキー(=値)を持つ要素は必ず同じ p の結果を持つため、
// どちらが「代表」として残るかに依存しない。
//
// UnionBy/ExceptBy/IntersectBy は逆にこの前提を満たさない。これらはキーで代表を1つ選ぶ
// (FIRST_VALUE() OVER (PARTITION BY key) 相当)操作を内包しており、同じキーでも値・述語の
// 結果が異なる要素が存在しうる。フィルタを先に通すと「どの値が代表になるか」自体が変わり、
// 押し下げ方向(片側・両側いずれも)に関わらず結果が変わりうる。したがって By系への
// Where 押し下げは実装しない。ExceptByDuplicateKeyRegression / IntersectByDuplicateKeyRegression /
// UnionByNotPushed はこの安全側の挙動(=素通しで一致すること)を固定する退行テスト。
public sealed class SetOperationPushdown:OptimizationTestBase {
    private static int[] SetA() => [1,2,3,4,5,6,7,8];
    private static int[] SetB() => [4,5,6,9,10];

    [Fact, Trait("Category","Unit")]
    public static void UnionBothSides() {
        var expected = SetA().Union(SetB()).Where(static v => v%2==0).ToList();
        AssertSequenceEqual(expected,UnionBothSidesOptimized());
        AssertSequenceEqual(expected,UnionBothSidesNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> UnionBothSidesOptimized() =>
        SetA().Union(SetB()).Where(static v => v%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> UnionBothSidesNoOptimized() =>
        SetA().Union(SetB()).Where(static v => v%2==0).ToList();

    [Fact, Trait("Category","Unit")]
    public static void ConcatBothSides() {
        var expected = SetA().Concat(SetB()).Where(static v => v%2==0).ToList();
        AssertSequenceEqual(expected,ConcatBothSidesOptimized());
        AssertSequenceEqual(expected,ConcatBothSidesNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ConcatBothSidesOptimized() =>
        SetA().Concat(SetB()).Where(static v => v%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ConcatBothSidesNoOptimized() =>
        SetA().Concat(SetB()).Where(static v => v%2==0).ToList();

    [Fact, Trait("Category","Unit")]
    public static void ExceptFirstSourceOnly() {
        var expected = SetA().Except(SetB()).Where(static v => v%2==0).ToList();
        AssertSequenceEqual(expected,ExceptFirstSourceOnlyOptimized());
        AssertSequenceEqual(expected,ExceptFirstSourceOnlyNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ExceptFirstSourceOnlyOptimized() =>
        SetA().Except(SetB()).Where(static v => v%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ExceptFirstSourceOnlyNoOptimized() =>
        SetA().Except(SetB()).Where(static v => v%2==0).ToList();

    [Fact, Trait("Category","Unit")]
    public static void IntersectFirstSourceOnly() {
        var expected = SetA().Intersect(SetB()).Where(static v => v%2==0).ToList();
        AssertSequenceEqual(expected,IntersectFirstSourceOnlyOptimized());
        AssertSequenceEqual(expected,IntersectFirstSourceOnlyNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> IntersectFirstSourceOnlyOptimized() =>
        SetA().Intersect(SetB()).Where(static v => v%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> IntersectFirstSourceOnlyNoOptimized() =>
        SetA().Intersect(SetB()).Where(static v => v%2==0).ToList();

    // 同じキー(Key=1)を持つ2要素が Name で割れる。代表選択に述語が影響してはいけない。
    private static Item[] DuplicateKeyItems() => [new(1,1,"a"),new(2,1,"b")];
    // Except: キー1がsecondに含まれない(=first側の代表がそのまま残る)ケースで検証する。
    private static int[] DisjointKeys() => [999];
    // Intersect: キー1がsecondに含まれる(=first側の代表が残る)ケースで検証する。
    private static int[] SharedKeys() => [1];

    // ExceptBy は Where を押し下げない(素通し)。押し下げていたら、代表選択が変わり
    // Optimized の結果が NoOptimized/expected と食い違う(修正前はこのテストが red になる)。
    [Fact, Trait("Category","Unit")]
    public static void ExceptByDuplicateKeyRegression() {
        var expected = DuplicateKeyItems().ExceptBy(DisjointKeys(),static x => x.Key)
            .Where(static x => x.Name=="b")
            .ToList();
        AssertSequenceEqual(expected,ExceptByDuplicateKeyRegressionOptimized());
        AssertSequenceEqual(expected,ExceptByDuplicateKeyRegressionNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Item> ExceptByDuplicateKeyRegressionOptimized() =>
        DuplicateKeyItems().ExceptBy(DisjointKeys(),static x => x.Key)
            .Where(static x => x.Name=="b")
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Item> ExceptByDuplicateKeyRegressionNoOptimized() =>
        DuplicateKeyItems().ExceptBy(DisjointKeys(),static x => x.Key)
            .Where(static x => x.Name=="b")
            .ToList();

    // IntersectBy も同様。押し下げていたら代表選択が変わり結果が食い違う。
    [Fact, Trait("Category","Unit")]
    public static void IntersectByDuplicateKeyRegression() {
        var expected = DuplicateKeyItems().IntersectBy(SharedKeys(),static x => x.Key)
            .Where(static x => x.Name=="b")
            .ToList();
        AssertSequenceEqual(expected,IntersectByDuplicateKeyRegressionOptimized());
        AssertSequenceEqual(expected,IntersectByDuplicateKeyRegressionNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Item> IntersectByDuplicateKeyRegressionOptimized() =>
        DuplicateKeyItems().IntersectBy(SharedKeys(),static x => x.Key)
            .Where(static x => x.Name=="b")
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Item> IntersectByDuplicateKeyRegressionNoOptimized() =>
        DuplicateKeyItems().IntersectBy(SharedKeys(),static x => x.Key)
            .Where(static x => x.Name=="b")
            .ToList();

    // UnionBy は本実装のスコープ外(押し下げ不健全)。素通しのまま一致することだけを固定する。
    private static Item[] SecondDuplicateKeyItems() => [new(3,1,"c")];

    [Fact, Trait("Category","Unit")]
    public static void UnionByNotPushed() {
        var expected = DuplicateKeyItems().UnionBy(SecondDuplicateKeyItems(),static x => x.Key)
            .Where(static x => x.Name=="b")
            .ToList();
        AssertSequenceEqual(expected,UnionByNotPushedOptimized());
        AssertSequenceEqual(expected,UnionByNotPushedNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Item> UnionByNotPushedOptimized() =>
        DuplicateKeyItems().UnionBy(SecondDuplicateKeyItems(),static x => x.Key)
            .Where(static x => x.Name=="b")
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Item> UnionByNotPushedNoOptimized() =>
        DuplicateKeyItems().UnionBy(SecondDuplicateKeyItems(),static x => x.Key)
            .Where(static x => x.Name=="b")
            .ToList();

    // DistinctBy も UnionBy/ExceptBy/IntersectBy と同じ「キーごとに代表を1つ選ぶ」操作。
    // Key=1 の代表は素通しなら先頭の (1,1,"a") だが、Where を先に通すと (2,1,"b") が代表になる。
    // 押し下げていたら Optimized だけ [(2,1,"b")] を返し expected([]) と食い違う。
    [Fact, Trait("Category","Unit")]
    public static void DistinctByDuplicateKeyRegression() {
        var expected = DuplicateKeyItems().DistinctBy(static x => x.Key)
            .Where(static x => x.Name=="b")
            .ToList();
        AssertSequenceEqual(expected,DistinctByDuplicateKeyRegressionOptimized());
        AssertSequenceEqual(expected,DistinctByDuplicateKeyRegressionNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Item> DistinctByDuplicateKeyRegressionOptimized() =>
        DuplicateKeyItems().DistinctBy(static x => x.Key)
            .Where(static x => x.Name=="b")
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Item> DistinctByDuplicateKeyRegressionNoOptimized() =>
        DuplicateKeyItems().DistinctBy(static x => x.Key)
            .Where(static x => x.Name=="b")
            .ToList();
}
