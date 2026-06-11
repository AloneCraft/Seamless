using System;
using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Optimization;

// §1: OrderBy(k).First() -> MinBy(k)（降順は MaxBy）。全ソート O(n log n) を O(n) スキャンへ。
// First は全要素型で書換（空列は First も生成器 MinBy/MaxBy も throw）。FirstOrDefault は非書換
// （生成器 MinBy/MaxBy は空列で throw、FirstOrDefault は default のため）だが通常経路で結果は正しい。
// tie は OrderBy 安定先頭＝MinBy/MaxBy 先頭一致で検証。
public sealed class OrderByFirstToMinMax:OptimizationTestBase{
    private static Item[] EmptyItems() => [];
    private static int OrderByFirstEmpty() => EmptyNumbers().OrderBy(static x => x).First();

    [Fact,Trait("Category","Unit")]
    public static void OrderBy_First() => OrderBy_FirstImpl();

    private static void OrderBy_FirstImpl(){
        {
            // First→MinBy へ書換。最小値。
            var expected=SystemEnumerable.First(SystemEnumerable.OrderBy(Numbers(),static x => x));
            var actual=Numbers().OrderBy(static x => x).First();
            Assert.Equal(expected,actual);
        }
        {
            // 値型(struct)で最小キー重複。OrderBy 安定先頭 == MinBy 先頭一致。
            var expected=SystemEnumerable.First(SystemEnumerable.OrderBy(Points(),static p => p.X));
            var actual=Points().OrderBy(static p => p.X).First();
            Assert.Equal(expected,actual);
        }
        {
            // 空列(値型)。双方 InvalidOperationException。
            Assert.Throws<InvalidOperationException>(static () => SystemEnumerable.First(SystemEnumerable.OrderBy(EmptyNumbers(),static x => x)));
            Assert.Throws<InvalidOperationException>(static () => OrderByFirstEmpty());
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderByDescending_First() => OrderByDescending_FirstImpl();

    private static void OrderByDescending_FirstImpl(){
        {
            // 値型(int)。First→MaxBy。最大値。
            var expected=SystemEnumerable.First(SystemEnumerable.OrderByDescending(Numbers(),static x => x));
            var actual=Numbers().OrderByDescending(static x => x).First();
            Assert.Equal(expected,actual);
        }
        {
            var expected=SystemEnumerable.First(SystemEnumerable.OrderByDescending(Points(),static p => p.X));
            var actual=Points().OrderByDescending(static p => p.X).First();
            Assert.Equal(expected,actual);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderBy_FirstOrDefault() => OrderBy_FirstOrDefaultImpl();

    private static void OrderBy_FirstOrDefaultImpl(){
        {
            // FirstOrDefault は非書換（OrderBy+FirstOrDefault のまま）。最小キーの先頭一致（重複キーあり）。
            var expected=SystemEnumerable.FirstOrDefault(SystemEnumerable.OrderBy(Outers(),static i => i.Key));
            var actual=Outers().OrderBy(static i => i.Key).FirstOrDefault();
            Assert.Equal(expected,actual);
        }
        {
            // 空列(参照型)。双方 null（FirstOrDefault なので throw しない）。
            var expected=SystemEnumerable.FirstOrDefault(SystemEnumerable.OrderBy(EmptyItems(),static i => i.Key));
            var actual=EmptyItems().OrderBy(static i => i.Key).FirstOrDefault();
            Assert.Equal(expected,actual);
        }
        {
            // 値型(int)。空列 -> default(0)。
            var expected=SystemEnumerable.FirstOrDefault(SystemEnumerable.OrderBy(EmptyNumbers(),static x => x));
            var actual=EmptyNumbers().OrderBy(static x => x).FirstOrDefault();
            Assert.Equal(expected,actual);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void OrderByDescending_FirstOrDefault() => OrderByDescending_FirstOrDefaultImpl();

    private static void OrderByDescending_FirstOrDefaultImpl(){
        {
            // 参照型(Item)。FirstOrDefault→MaxBy。最大キーの先頭一致。
            var expected=SystemEnumerable.FirstOrDefault(SystemEnumerable.OrderByDescending(Outers(),static i => i.Key));
            var actual=Outers().OrderByDescending(static i => i.Key).FirstOrDefault();
            Assert.Equal(expected,actual);
        }
        {
            var expected=SystemEnumerable.FirstOrDefault(SystemEnumerable.OrderByDescending(EmptyItems(),static i => i.Key));
            var actual=EmptyItems().OrderByDescending(static i => i.Key).FirstOrDefault();
            Assert.Equal(expected,actual);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void ReferenceFirst() => ReferenceFirstImpl();

    private static void ReferenceFirstImpl(){
        // 参照型(string)の First も MinBy へ書換（空列なら双方 throw）。非空での最小長先頭一致。
        var expected=SystemEnumerable.First(SystemEnumerable.OrderBy(Words(),static w => w.Length));
        var actual=Words().OrderBy(static w => w.Length).First();
        Assert.Equal(expected,actual);
    }
}
