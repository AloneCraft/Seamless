using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Optimization;

// §1: 連続 Where の選択率並べ替え。純粋・全域な述語のみコスト＋selectivity で順入替（結果集合は不変）。
// 安全ゲートを満たさない（参照型 deref 等）述語が混じる場合は並べ替えず原順を維持し、NRE を起こさない。
public sealed class WhereReorder:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void CostAndSelectivity() => CostAndSelectivityImpl();

    private static void CostAndSelectivityImpl(){
        {
            // 純粋・全域。安価な範囲述語の後ろに高コストの等値述語 -> 並べ替えても結果不変。
            var expected=SystemEnumerable.Where(SystemEnumerable.Where(Numbers(),static x => x>3),static x => x*x%2==0).ToList();
            var actual=new Consumer<int>();
            Numbers().Where(static x => x>3).Where(static x => x*x%2==0).Consume(actual);
            AssertSequenceEqual(expected,actual.Items);
        }
        {
            // selectivity ヒューリスティック: 等値 == を前へ -> 結果不変。
            var expected=SystemEnumerable.Where(SystemEnumerable.Where(Numbers(),static x => x>0),static x => x==3).ToList();
            var actual=new Consumer<int>();
            Numbers().Where(static x => x>0).Where(static x => x==3).Consume(actual);
            AssertSequenceEqual(expected,actual.Items);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void StructMemberAccess() => StructMemberAccessImpl();

    private static void StructMemberAccessImpl(){
        // 非null値型(struct)のメンバアクセスは NRE 不能なので並べ替え対象 -> 結果不変。
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(Points(),static p => p.X>0),static p => p.Y==1).ToList();
        var actual=new Consumer<Point>();
        Points().Where(static p => p.X>0).Where(static p => p.Y==1).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void GuardSuppressed() => GuardSuppressedImpl();

    private static void GuardSuppressedImpl(){
        // 参照型 deref を含む第2述語は安全ゲート外 -> 並べ替え抑止。null ガードが先頭のまま維持され NRE しない。
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(WithNulls(),static s => s!=null),static s => s!.Length>3).ToList();
        var actual=new Consumer<string?>();
        WithNulls().Where(static s => s!=null).Where(static s => s!.Length>3).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
