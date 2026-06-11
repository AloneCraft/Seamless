using System;
using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Optimization;

// §1: Where(p).Op() -> Op(p)（述語をスカラー終端へ融合）。
// expected=非 intercept の SystemEnumerable（原チェーン）、actual=intercept される拡張メソッド形。
// 例外を投げるケースは式本体ヘルパ経由で intercept させる（ラムダ内部の LINQ は intercept されないため）。
public sealed class WherePredicateFusion:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void Count() => CountImpl();

    private static void CountImpl(){
        {
            // 一致あり。
            var expected=SystemEnumerable.Count(SystemEnumerable.Where(Numbers(),static x => x>4));
            var actual=Numbers().Where(static x => x>4).Count();
            Assert.Equal(expected,actual);
        }
        {
            // 一致なし。
            var expected=SystemEnumerable.Count(SystemEnumerable.Where(Numbers(),static x => x>100));
            var actual=Numbers().Where(static x => x>100).Count();
            Assert.Equal(expected,actual);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void LongCount() => LongCountImpl();

    private static void LongCountImpl(){
        var expected=SystemEnumerable.LongCount(SystemEnumerable.Where(Numbers(),static x => x>4));
        var actual=Numbers().Where(static x => x>4).LongCount();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public static void Any() => AnyImpl();

    private static void AnyImpl(){
        {
            // 一致あり -> true。
            var expected=SystemEnumerable.Any(SystemEnumerable.Where(Numbers(),static x => x>4));
            var actual=Numbers().Where(static x => x>4).Any();
            Assert.Equal(expected,actual);
        }
        {
            // 一致なし -> false。
            var expected=SystemEnumerable.Any(SystemEnumerable.Where(Numbers(),static x => x>100));
            var actual=Numbers().Where(static x => x>100).Any();
            Assert.Equal(expected,actual);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void First() => FirstImpl();
    private static int FirstNoMatch() => Numbers().Where(static x => x>100).First();

    private static void FirstImpl(){
        {
            // 一致あり。
            var expected=SystemEnumerable.First(SystemEnumerable.Where(Numbers(),static x => x>4));
            var actual=Numbers().Where(static x => x>4).First();
            Assert.Equal(expected,actual);
        }
        {
            // 一致なし -> 双方 InvalidOperationException。
            Assert.Throws<InvalidOperationException>(static () => SystemEnumerable.First(SystemEnumerable.Where(Numbers(),static x => x>100)));
            Assert.Throws<InvalidOperationException>(static () => FirstNoMatch());
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void FirstOrDefault() => FirstOrDefaultImpl();

    private static void FirstOrDefaultImpl(){
        {
            var expected=SystemEnumerable.FirstOrDefault(SystemEnumerable.Where(Numbers(),static x => x>4));
            var actual=Numbers().Where(static x => x>4).FirstOrDefault();
            Assert.Equal(expected,actual);
        }
        {
            // 一致なし -> default(0)。
            var expected=SystemEnumerable.FirstOrDefault(SystemEnumerable.Where(Numbers(),static x => x>100));
            var actual=Numbers().Where(static x => x>100).FirstOrDefault();
            Assert.Equal(expected,actual);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void Last() => LastImpl();

    private static void LastImpl(){
        var expected=SystemEnumerable.Last(SystemEnumerable.Where(Numbers(),static x => x>4));
        var actual=Numbers().Where(static x => x>4).Last();
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public static void LastOrDefault() => LastOrDefaultImpl();

    private static void LastOrDefaultImpl(){
        {
            var expected=SystemEnumerable.LastOrDefault(SystemEnumerable.Where(Numbers(),static x => x>4));
            var actual=Numbers().Where(static x => x>4).LastOrDefault();
            Assert.Equal(expected,actual);
        }
        {
            var expected=SystemEnumerable.LastOrDefault(SystemEnumerable.Where(Numbers(),static x => x>100));
            var actual=Numbers().Where(static x => x>100).LastOrDefault();
            Assert.Equal(expected,actual);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void Single() => SingleImpl();
    private static int SingleMultiple() => Numbers().Where(static x => x>4).Single();

    private static void SingleImpl(){
        {
            // ちょうど 1 件。
            var expected=SystemEnumerable.Single(SystemEnumerable.Where(Numbers(),static x => x==8));
            var actual=Numbers().Where(static x => x==8).Single();
            Assert.Equal(expected,actual);
        }
        {
            // 複数一致 -> 双方 InvalidOperationException。
            Assert.Throws<InvalidOperationException>(static () => SystemEnumerable.Single(SystemEnumerable.Where(Numbers(),static x => x>4)));
            Assert.Throws<InvalidOperationException>(static () => SingleMultiple());
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void SingleOrDefault() => SingleOrDefaultImpl();

    private static void SingleOrDefaultImpl(){
        {
            // ちょうど 1 件。
            var expected=SystemEnumerable.SingleOrDefault(SystemEnumerable.Where(Numbers(),static x => x==8));
            var actual=Numbers().Where(static x => x==8).SingleOrDefault();
            Assert.Equal(expected,actual);
        }
        {
            // 0 件 -> default(0)。
            var expected=SystemEnumerable.SingleOrDefault(SystemEnumerable.Where(Numbers(),static x => x>100));
            var actual=Numbers().Where(static x => x>100).SingleOrDefault();
            Assert.Equal(expected,actual);
        }
    }
}
