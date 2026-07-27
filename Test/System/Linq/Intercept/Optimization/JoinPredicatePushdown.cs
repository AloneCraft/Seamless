using Seamless.Linq;

namespace Test.System.Linq.Intercept.Optimization;

// Join(...).Where(p) の葉移動。片側依存はそのまま葉へ、AND 複合述語は連言へ分解して
// 依存する側の葉へ個別に押し下げ、両側参照の連言だけ Join 後へ残す（部分押し下げ）。
public sealed class JoinPredicatePushdown:OptimizationTestBase {
    [Fact, Trait("Category","Unit")]
    public static void OuterOnlyConjuncts() {
        var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>1&&x.o.Name!="c")
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();
        AssertSequenceEqual(expected,OuterOnlyConjunctsOptimized());
        AssertSequenceEqual(expected,OuterOnlyConjunctsNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> OuterOnlyConjunctsOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>1&&x.o.Name!="c")
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> OuterOnlyConjunctsNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>1&&x.o.Name!="c")
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [Fact, Trait("Category","Unit")]
    public static void InnerOnlyConjuncts() {
        var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.i.Id>10&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();
        AssertSequenceEqual(expected,InnerOnlyConjunctsOptimized());
        AssertSequenceEqual(expected,InnerOnlyConjunctsNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> InnerOnlyConjunctsOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.i.Id>10&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> InnerOnlyConjunctsNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.i.Id>10&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [Fact, Trait("Category","Unit")]
    public static void BothSidesSplitToEachLeaf() {
        var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>2&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();
        AssertSequenceEqual(expected,BothSidesSplitToEachLeafOptimized());
        AssertSequenceEqual(expected,BothSidesSplitToEachLeafNoOptimized());
        var empty = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>1000&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();
        AssertSequenceEqual(empty,BothSidesSplitEmptyOptimized());
        AssertSequenceEqual(empty,BothSidesSplitEmptyNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> BothSidesSplitToEachLeafOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>2&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> BothSidesSplitToEachLeafNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>2&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> BothSidesSplitEmptyOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>1000&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> BothSidesSplitEmptyNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>1000&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    // 両側参照の連言 x.o.Id+8<x.i.Id だけが Join 後へ残り、残り 2 つがそれぞれの葉へ移る。
    [Fact, Trait("Category","Unit")]
    public static void BothSidesLeaveResidual() {
        var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>1&&x.o.Id+8<x.i.Id&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();
        AssertSequenceEqual(expected,BothSidesLeaveResidualOptimized());
        AssertSequenceEqual(expected,BothSidesLeaveResidualNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> BothSidesLeaveResidualOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>1&&x.o.Id+8<x.i.Id&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> BothSidesLeaveResidualNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>1&&x.o.Id+8<x.i.Id&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [Fact, Trait("Category","Unit")]
    public static void ConstantConjuncts() {
        var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => GetOffset()>0&&Limit()>10)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();
        AssertSequenceEqual(expected,ConstantConjunctsOptimized());
        AssertSequenceEqual(expected,ConstantConjunctsNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ConstantConjunctsOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => GetOffset()>0&&Limit()>10)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ConstantConjunctsNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => GetOffset()>0&&Limit()>10)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    // (a||b)&&c を平坦化で壊すと a||(b&&c) になり結果が変わる。優先順位の保持を検証する。
    [Fact, Trait("Category","Unit")]
    public static void DisjunctionKeepsPrecedence() {
        var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => (x.o.Id==3||x.i.Id==10)&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();
        AssertSequenceEqual(expected,DisjunctionKeepsPrecedenceOptimized());
        AssertSequenceEqual(expected,DisjunctionKeepsPrecedenceNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> DisjunctionKeepsPrecedenceOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => (x.o.Id==3||x.i.Id==10)&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> DisjunctionKeepsPrecedenceNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => (x.o.Id==3||x.i.Id==10)&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    // Join の resultSelector が作る carrier の3形（匿名型・タプル・逆解析できない直接値）を横断で検証する。
    // 直接値は carrier を持たないため押し下げず素通しする。
    [Fact, Trait("Category","Unit")]
    public static void CarrierShapes() {
        var anonymous = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>2&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();
        AssertSequenceEqual(anonymous,AnonymousCarrierOptimized());
        AssertSequenceEqual(anonymous,AnonymousCarrierNoOptimized());

        var tuple = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (o,i))
            .Where(static x => x.o.Id>2&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();
        AssertSequenceEqual(tuple,TupleCarrierOptimized());
        AssertSequenceEqual(tuple,TupleCarrierNoOptimized());

        var scalar = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => o.Id*100+i.Id)
            .Where(static v => v>210&&v<400)
            .ToList();
        AssertSequenceEqual(scalar,ScalarCarrierOptimized());
        AssertSequenceEqual(scalar,ScalarCarrierNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> AnonymousCarrierOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>2&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> AnonymousCarrierNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id>2&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> TupleCarrierOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (o,i))
            .Where(static x => x.o.Id>2&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> TupleCarrierNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => (o,i))
            .Where(static x => x.o.Id>2&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ScalarCarrierOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => o.Id*100+i.Id)
            .Where(static v => v>210&&v<400)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ScalarCarrierNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => o.Id*100+i.Id)
            .Where(static v => v>210&&v<400)
            .ToList();

    // Anchor は評価回数・評価順の変更を禁止するので連言分割しない。
    [Fact, Trait("Category","Unit")]
    public static void AnchorBlocksConjunctSplit() {
        var expected = Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id.Anchor>2&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();
        AssertSequenceEqual(expected,AnchorBlocksConjunctSplitOptimized());
        AssertSequenceEqual(expected,AnchorBlocksConjunctSplitNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> AnchorBlocksConjunctSplitOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id.Anchor>2&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> AnchorBlocksConjunctSplitNoOptimized() =>
        Outers().Join(Inners(),static o => o.Key,static i => i.Key,static (o,i) => new { o,i })
            .Where(static x => x.o.Id.Anchor>2&&x.i.Id<13)
            .Select(static x => x.o.Id*100+x.i.Id)
            .ToList();

    // LeftJoin で inner 側へ押し下げると、その key の inner が空になった outer 行が
    // default とペアで残り行数が変わる。inner 依存の連言は Join 後へ残さなければならない。
    [Fact, Trait("Category","Unit")]
    public static void LeftJoinKeepsInnerConjunct() {
        var expected = Numbers().LeftJoin(SecondNumbers(),static x => x%3,static y => y%3,static (x,y) => new { x,y })
            .Where(static p => p.x>2&&p.y!=0)
            .Select(static p => p.x*100+p.y)
            .ToList();
        AssertSequenceEqual(expected,LeftJoinKeepsInnerConjunctOptimized());
        AssertSequenceEqual(expected,LeftJoinKeepsInnerConjunctNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> LeftJoinKeepsInnerConjunctOptimized() =>
        Numbers().LeftJoin(SecondNumbers(),static x => x%3,static y => y%3,static (x,y) => new { x,y })
            .Where(static p => p.x>2&&p.y!=0)
            .Select(static p => p.x*100+p.y)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> LeftJoinKeepsInnerConjunctNoOptimized() =>
        Numbers().LeftJoin(SecondNumbers(),static x => x%3,static y => y%3,static (x,y) => new { x,y })
            .Where(static p => p.x>2&&p.y!=0)
            .Select(static p => p.x*100+p.y)
            .ToList();

    private static int[] SecondNumbers() => [0,2,5,8];
}
