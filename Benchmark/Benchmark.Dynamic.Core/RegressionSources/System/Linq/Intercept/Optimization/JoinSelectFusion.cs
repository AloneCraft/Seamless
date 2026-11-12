namespace Test.System.Linq.Intercept.Optimization;

// result selector が作る carrier と後続 Select の融合を検証する。
// Select は 1 行 → 1 行なので行数も順序も変わらない。述語プッシュダウンで必要だった
// 「LeftJoin の inner 側へ押すと行数が変わる」種類のガードは射影融合には要らないため、
// Join / LeftJoin / RightJoin / GroupJoin / SelectMany のすべてで融合できることを固定する。
public sealed class JoinSelectFusion:OptimizationTestBase {
    [Fact, Trait("Category","Unit")]
    public static void JoinResultSelector() {
        var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Select(static x => x.Outer.Id*100+x.Inner.Id)
            .ToList();
        AssertSequenceEqual(expected,JoinResultSelectorOptimized());
        AssertSequenceEqual(expected,JoinResultSelectorNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> JoinResultSelectorOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Select(static x => x.Outer.Id*100+x.Inner.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> JoinResultSelectorNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Select(static x => x.Outer.Id*100+x.Inner.Id)
            .ToList();

    // 片側しか使わない Select。使われない側は Tier0(パラメータ参照)なので捨ててよい。
    [Fact, Trait("Category","Unit")]
    public static void JoinOuterSideOnly() {
        var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Select(static x => x.Outer.Id)
            .ToList();
        AssertSequenceEqual(expected,JoinOuterSideOnlyOptimized());
        AssertSequenceEqual(expected,JoinOuterSideOnlyNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> JoinOuterSideOnlyOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Select(static x => x.Outer.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> JoinOuterSideOnlyNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Select(static x => x.Outer.Id)
            .ToList();

    // 述語プッシュダウンとの合わせ技。Where が葉へ退いてから Join と Select が隣接し、carrier が完全に消える。
    [Fact, Trait("Category","Unit")]
    public static void PushdownThenFusionRemovesCarrier() {
        var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Where(static x => x.Outer.Id>1&&x.Inner.Id<13)
            .Select(static x => x.Outer.Id*100+x.Inner.Id)
            .ToList();
        AssertSequenceEqual(expected,PushdownThenFusionRemovesCarrierOptimized());
        AssertSequenceEqual(expected,PushdownThenFusionRemovesCarrierNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> PushdownThenFusionRemovesCarrierOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Where(static x => x.Outer.Id>1&&x.Inner.Id<13)
            .Select(static x => x.Outer.Id*100+x.Inner.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> PushdownThenFusionRemovesCarrierNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Where(static x => x.Outer.Id>1&&x.Inner.Id<13)
            .Select(static x => x.Outer.Id*100+x.Inner.Id)
            .ToList();

    // LeftJoin は一致なしの outer 行が残る。融合しても行数が変わらないことを空一致込みで確認する。
    [Fact, Trait("Category","Unit")]
    public static void LeftJoinResultSelector() {
        var expected = Outers().LeftJoin(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Select(static x => x.Outer.Id*100+(x.Inner is null ? 0 : x.Inner.Id))
            .ToList();
        AssertSequenceEqual(expected,LeftJoinResultSelectorOptimized());
        AssertSequenceEqual(expected,LeftJoinResultSelectorNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> LeftJoinResultSelectorOptimized() =>
        Outers().LeftJoin(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Select(static x => x.Outer.Id*100+(x.Inner is null ? 0 : x.Inner.Id))
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> LeftJoinResultSelectorNoOptimized() =>
        Outers().LeftJoin(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Select(static x => x.Outer.Id*100+(x.Inner is null ? 0 : x.Inner.Id))
            .ToList();

    // RightJoin は一致なしの inner 行が残る。
    [Fact, Trait("Category","Unit")]
    public static void RightJoinResultSelector() {
        var expected = Outers().RightJoin(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Select(static x => (x.Outer is null ? 0 : x.Outer.Id)*100+x.Inner.Id)
            .ToList();
        AssertSequenceEqual(expected,RightJoinResultSelectorOptimized());
        AssertSequenceEqual(expected,RightJoinResultSelectorNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> RightJoinResultSelectorOptimized() =>
        Outers().RightJoin(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Select(static x => (x.Outer is null ? 0 : x.Outer.Id)*100+x.Inner.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> RightJoinResultSelectorNoOptimized() =>
        Outers().RightJoin(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (Outer: o,Inner: i))
            .Select(static x => (x.Outer is null ? 0 : x.Outer.Id)*100+x.Inner.Id)
            .ToList();

    // GroupJoin は第2パラメータが IEnumerable<TInner>。carrier メンバとしては単なる識別子(Tier0)。
    [Fact, Trait("Category","Unit")]
    public static void GroupJoinResultSelector() {
        var expected = Outers().GroupJoin(Inners(),static o => o.Key,static i => i.Key,static (o,g) => (Outer: o,Group: g))
            .Select(static x => x.Outer.Id*100+CountGroup(x.Group))
            .ToList();
        AssertSequenceEqual(expected,GroupJoinResultSelectorOptimized());
        AssertSequenceEqual(expected,GroupJoinResultSelectorNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> GroupJoinResultSelectorOptimized() =>
        Outers().GroupJoin(Inners(),static o => o.Key,static i => i.Key,static (o,g) => (Outer: o,Group: g))
            .Select(static x => x.Outer.Id*100+CountGroup(x.Group))
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> GroupJoinResultSelectorNoOptimized() =>
        Outers().GroupJoin(Inners(),static o => o.Key,static i => i.Key,static (o,g) => (Outer: o,Group: g))
            .Select(static x => x.Outer.Id*100+CountGroup(x.Group))
            .ToList();

    // SelectMany の result selector も同型。
    [Fact, Trait("Category","Unit")]
    public static void SelectManyResultSelector() {
        var expected = Outers().SelectMany(static o => Inners(),static (o,i) => (Outer: o,Inner: i))
            .Select(static x => x.Outer.Id*100+x.Inner.Id)
            .ToList();
        AssertSequenceEqual(expected,SelectManyResultSelectorOptimized());
        AssertSequenceEqual(expected,SelectManyResultSelectorNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> SelectManyResultSelectorOptimized() =>
        Outers().SelectMany(static o => Inners(),static (o,i) => (Outer: o,Inner: i))
            .Select(static x => x.Outer.Id*100+x.Inner.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> SelectManyResultSelectorNoOptimized() =>
        Outers().SelectMany(static o => Inners(),static (o,i) => (Outer: o,Inner: i))
            .Select(static x => x.Outer.Id*100+x.Inner.Id)
            .ToList();

    // クエリ式経路。join 句は透過 ValueTuple carrier を作り、select 句が末尾 Select を足すので、
    // 述語プッシュダウン → 射影融合の順で透過 carrier が丸ごと消える(本作業の最大の利得経路)。
    [Fact, Trait("Category","Unit")]
    public static void QuerySyntaxJoinLoweringFuses() {
        var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (o,i))
            .Where(static x => x.o.Id>1)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();
        AssertSequenceEqual(expected,QuerySyntaxJoinLoweringFusesOptimized());
        AssertSequenceEqual(expected,QuerySyntaxJoinLoweringFusesNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> QuerySyntaxJoinLoweringFusesOptimized() =>
        (from o in Outers()
         join i in Inners() on o.Key equals i.Key
         where o.Id>1
         select o.Id*100+i.Id).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> QuerySyntaxJoinLoweringFusesNoOptimized() =>
        (from o in Outers()
         join i in Inners() on o.Key equals i.Key
         where o.Id>1
         select o.Id*100+i.Id).ToList();

    // carrier 形の横断検証。匿名型 / タプル式 / Tuple / record struct / record /
    // 直接値(逆解析できないので素通し)の 6 形すべてで意味が保たれること。
    // 明示的な new ValueTuple<A,B>(..) 形は CarrierAnalysis 側では解決できるが、生成器の型名描画が
    // ValueTuple<A,B> を (A,B) と書いてしまい new (A,B)(..) という不正な C# になる既存の別バグがあるため
    // ここでは扱わない(ProjectionMerge を無効にすると更にエラーが増えるので本最適化とは無関係)。
    // 通常の書き方であるタプル式 (o,i) は同じ ObjectCarrierShape を作るので実質的な穴は無い。
    [Fact, Trait("Category","Unit")]
    public static void CarrierShapes() {
        {
            // 匿名型
            var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
                .Select(static x => x.o.Id*100+x.i.Id)
                .ToList();
            AssertSequenceEqual(expected,AnonymousCarrierOptimized());
            AssertSequenceEqual(expected,AnonymousCarrierNoOptimized());
        }
        {
            // タプル式
            var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (o,i))
                .Select(static x => x.o.Id*100+x.i.Id)
                .ToList();
            AssertSequenceEqual(expected,TupleCarrierOptimized());
            AssertSequenceEqual(expected,TupleCarrierNoOptimized());
        }
        {
            // Tuple(参照型)
            var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new Tuple<Item,Item>(o,i))
                .Select(static x => x.Item1.Id*100+x.Item2.Id)
                .ToList();
            AssertSequenceEqual(expected,TupleClassCarrierOptimized());
            AssertSequenceEqual(expected,TupleClassCarrierNoOptimized());
        }
        {
            // record struct の位置引数コンストラクタ
            var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new PairStruct(o,i))
                .Select(static x => x.Left.Id*100+x.Right.Id)
                .ToList();
            AssertSequenceEqual(expected,RecordStructCarrierOptimized());
            AssertSequenceEqual(expected,RecordStructCarrierNoOptimized());
        }
        {
            // record class の位置引数コンストラクタ
            var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new PairClass(o,i))
                .Select(static x => x.Left.Id*100+x.Right.Id)
                .ToList();
            AssertSequenceEqual(expected,RecordClassCarrierOptimized());
            AssertSequenceEqual(expected,RecordClassCarrierNoOptimized());
        }
        {
            // 直接値。carrier を持たないので融合せず素通しする。
            var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => o.Id*100+i.Id)
                .Select(static v => v+1)
                .ToList();
            AssertSequenceEqual(expected,ScalarCarrierOptimized());
            AssertSequenceEqual(expected,ScalarCarrierNoOptimized());
        }
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> AnonymousCarrierOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> AnonymousCarrierNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> TupleCarrierOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (o,i))
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> TupleCarrierNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (o,i))
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> TupleClassCarrierOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new Tuple<Item,Item>(o,i))
            .Select(static x => x.Item1.Id*100+x.Item2.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> TupleClassCarrierNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new Tuple<Item,Item>(o,i))
            .Select(static x => x.Item1.Id*100+x.Item2.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> RecordStructCarrierOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new PairStruct(o,i))
            .Select(static x => x.Left.Id*100+x.Right.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> RecordStructCarrierNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new PairStruct(o,i))
            .Select(static x => x.Left.Id*100+x.Right.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> RecordClassCarrierOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new PairClass(o,i))
            .Select(static x => x.Left.Id*100+x.Right.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> RecordClassCarrierNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new PairClass(o,i))
            .Select(static x => x.Left.Id*100+x.Right.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ScalarCarrierOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => o.Id*100+i.Id)
            .Select(static v => v+1)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ScalarCarrierNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => o.Id*100+i.Id)
            .Select(static v => v+1)
            .ToList();

    // GroupJoin のグループ件数。テストプロジェクトには SystemEnumerable エイリアスが無いので自前で数える。
    public static int CountGroup(IEnumerable<Item> group) {
        var count = 0;
        foreach(var _ in group) {
            count++;
        }
        return count;
    }

    public readonly record struct PairStruct(Item Left,Item Right);
    public sealed record PairClass(Item Left,Item Right);
}
