using Seamless.Linq;

namespace Test.System.Linq.Intercept.Optimization;

// Select(f).Select(g) → Select(g∘f) の意味保存を検証する。
// carrier メンバの引き戻し可否は CarrierAnalysis の Tier 規則が決める。
//   Tier0 純粋葉(識別子/リテラル等)        : 参照回数は任意(0 回で捨ててよく、複数回で複製してよい)
//   Tier1 可搬葉(メンバ/要素アクセス/キャスト): 1 回以上必須(評価の消滅を禁止)
//   Tier2 任意式(メソッド呼び出し等)        : ちょうど 1 回、かつ carrier 内に高々 1 個
public sealed class ProjectionMerge:OptimizationTestBase {
    // 単純なメンバ取り出し。carrier メンバは両方 Tier0 なので必ず融合する。
    [Fact, Trait("Category","Unit")]
    public static void SelectCarrierThenMember() {
        var expected = Outers().Select(static o => (Id: o.Id,Key: o.Key))
            .Select(static p => p.Id*100+p.Key)
            .ToList();
        AssertSequenceEqual(expected,SelectCarrierThenMemberOptimized());
        AssertSequenceEqual(expected,SelectCarrierThenMemberNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> SelectCarrierThenMemberOptimized() =>
        Outers().Select(static o => (Id: o.Id,Key: o.Key))
            .Select(static p => p.Id*100+p.Key)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> SelectCarrierThenMemberNoOptimized() =>
        Outers().Select(static o => (Id: o.Id,Key: o.Key))
            .Select(static p => p.Id*100+p.Key)
            .ToList();

    // let 句相当。Tier2(メソッド呼び出し)が 1 個で、後続 Select がちょうど 1 回だけ読むので融合できる。
    [Fact, Trait("Category","Unit")]
    public static void SelectComputedCarrierThenMember() {
        var expected = Outers().Select(static o => (Source: o,Scaled: Scale(o.Id)))
            .Select(static p => p.Scaled+p.Source.Key)
            .ToList();
        AssertSequenceEqual(expected,SelectComputedCarrierThenMemberOptimized());
        AssertSequenceEqual(expected,SelectComputedCarrierThenMemberNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> SelectComputedCarrierThenMemberOptimized() =>
        Outers().Select(static o => (Source: o,Scaled: Scale(o.Id)))
            .Select(static p => p.Scaled+p.Source.Key)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> SelectComputedCarrierThenMemberNoOptimized() =>
        Outers().Select(static o => (Source: o,Scaled: Scale(o.Id)))
            .Select(static p => p.Scaled+p.Source.Key)
            .ToList();

    // Tier2 が 2 個ある carrier は融合しない(読み出し順で評価順が入れ替わるため)。結果は当然同じ。
    [Fact, Trait("Category","Unit")]
    public static void TwoArbitraryMembersStayUnfused() {
        var expected = Outers().Select(static o => (First: Scale(o.Id),Second: Scale(o.Key)))
            .Select(static p => p.Second-p.First)
            .ToList();
        AssertSequenceEqual(expected,TwoArbitraryMembersStayUnfusedOptimized());
        AssertSequenceEqual(expected,TwoArbitraryMembersStayUnfusedNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> TwoArbitraryMembersStayUnfusedOptimized() =>
        Outers().Select(static o => (First: Scale(o.Id),Second: Scale(o.Key)))
            .Select(static p => p.Second-p.First)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> TwoArbitraryMembersStayUnfusedNoOptimized() =>
        Outers().Select(static o => (First: Scale(o.Id),Second: Scale(o.Key)))
            .Select(static p => p.Second-p.First)
            .ToList();

    // 3 段の Select が 1 段へ畳まれる。再束縛ループが連鎖融合を収束させることの確認。
    [Fact, Trait("Category","Unit")]
    public static void ThreeSelectsCollapse() {
        var expected = Numbers().Select(static v => v+1)
            .Select(static v => v*2)
            .Select(static v => v-1)
            .ToList();
        AssertSequenceEqual(expected,ThreeSelectsCollapseOptimized());
        AssertSequenceEqual(expected,ThreeSelectsCollapseNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ThreeSelectsCollapseOptimized() =>
        Numbers().Select(static v => v+1)
            .Select(static v => v*2)
            .Select(static v => v-1)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ThreeSelectsCollapseNoOptimized() =>
        Numbers().Select(static v => v+1)
            .Select(static v => v*2)
            .Select(static v => v-1)
            .ToList();

    // 入れ子 carrier(carrier の中に carrier)もパス解決で引き戻せる。
    [Fact, Trait("Category","Unit")]
    public static void NestedCarrierResolves() {
        var expected = Outers().Select(static o => (Outer: (Id: o.Id,Key: o.Key),Name: o.Name))
            .Select(static p => p.Outer.Id*10+p.Outer.Key)
            .ToList();
        AssertSequenceEqual(expected,NestedCarrierResolvesOptimized());
        AssertSequenceEqual(expected,NestedCarrierResolvesNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> NestedCarrierResolvesOptimized() =>
        Outers().Select(static o => (Outer: (Id: o.Id,Key: o.Key),Name: o.Name))
            .Select(static p => p.Outer.Id*10+p.Outer.Key)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> NestedCarrierResolvesNoOptimized() =>
        Outers().Select(static o => (Outer: (Id: o.Id,Key: o.Key),Name: o.Name))
            .Select(static p => p.Outer.Id*10+p.Outer.Key)
            .ToList();

    // Anchor を含む射影は評価回数・評価順の変更を禁止するので融合しない。
    [Fact, Trait("Category","Unit")]
    public static void AnchorBlocksProjectionMerge() {
        var expected = Numbers().Select(static v => v+1)
            .Select(static v => v*2)
            .ToList();
        AssertSequenceEqual(expected,AnchorBlocksProjectionMergeOptimized());
        AssertSequenceEqual(expected,AnchorBlocksProjectionMergeNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> AnchorBlocksProjectionMergeOptimized() =>
        Numbers().Select(static v => (v+1).Anchor)
            .Select(static v => v*2)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> AnchorBlocksProjectionMergeNoOptimized() =>
        Numbers().Select(static v => (v+1).Anchor)
            .Select(static v => v*2)
            .ToList();

    // Where を挟むと Select 同士は隣接しないので融合しない。パイプライン段としてはそのまま通る。
    [Fact, Trait("Category","Unit")]
    public static void WhereBetweenSelectsBlocksMerge() {
        var expected = Numbers().Select(static v => v+1)
            .Where(static v => v>3)
            .Select(static v => v*2)
            .ToList();
        AssertSequenceEqual(expected,WhereBetweenSelectsBlocksMergeOptimized());
        AssertSequenceEqual(expected,WhereBetweenSelectsBlocksMergeNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> WhereBetweenSelectsBlocksMergeOptimized() =>
        Numbers().Select(static v => v+1)
            .Where(static v => v>3)
            .Select(static v => v*2)
            .ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> WhereBetweenSelectsBlocksMergeNoOptimized() =>
        Numbers().Select(static v => v+1)
            .Where(static v => v>3)
            .Select(static v => v*2)
            .ToList();

    // 引き戻し対象になる不変式。ホイスト側と混ざらないよう決定的にする。
    public static int Scale(int value) => value*7;
}
