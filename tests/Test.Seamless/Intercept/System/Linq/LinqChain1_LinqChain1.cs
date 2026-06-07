using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class LinqChain1_LinqChain1 {
    [Fact, Trait("Category","Unit")]
    public static void WhereSelect() {
        WhereSelectImpl();
    }
    private static void WhereSelectImpl() {
        var expected = Enumerable.Select(Enumerable.Where(Source(),p=>p>1),p=>p*10).ToList();
        var actual = Source().Where(p=>p>1).Select(p=>p*10).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void WhereWhere() {
        WhereWhereImpl();
    }

    [Fact, Trait("Category","Unit")]
    public static void WhereWhereImpl() {
        var actual = Source().Where(p=>p>1).Where(p=>p<4).ToArray();
        Assert.Equal([2,3],actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void SelectWhere() {
        SelectWhereImpl();
    }

    [Fact, Trait("Category","Unit")]
    public static void SelectWhereImpl() {
        var actual = Source().Select(p=>p*10).Where(p=>p>20).ToArray();
        Assert.Equal([30,40],actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void SelectSelect() {
        SelectSelectImpl();
    }

    [Fact, Trait("Category","Unit")]
    public static void SelectSelectImpl() {
        var actual = Source().Select(p=>p+1).Select(p=>p*10).ToArray();
        Assert.Equal([20,30,40,50],actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void TakeLastTakeLast() {
        TakeLastTakeLastImpl();
    }

    private static void TakeLastTakeLastImpl() {
        {
            //span経路(配列)・引数違いでmin融合になること
            var expected = Enumerable.TakeLast(Enumerable.TakeLast(Source(),3),2).ToList();
            var actual = Source().TakeLast(3).TakeLast(2).ToList();
            Assert.Equal(expected,actual);
        }
        {
            //span経路・3段連鎖
            var expected = Enumerable.TakeLast(Enumerable.TakeLast(Enumerable.TakeLast(Source(),2),3),4).ToList();
            var actual = Source().TakeLast(2).TakeLast(3).TakeLast(4).ToList();
            Assert.Equal(expected,actual);
        }
        {
            //非span経路(Where挟みでQueue経路)
            var expected = Enumerable.TakeLast(Enumerable.TakeLast(Enumerable.Where(Source(),p=>p>0),3),2).ToList();
            var actual = Source().Where(p=>p>0).TakeLast(3).TakeLast(2).ToList();
            Assert.Equal(expected,actual);
        }
    }

    [Fact, Trait("Category","Unit")]
    public static void SkipLastSkipLast() {
        SkipLastSkipLastImpl();
    }

    private static void SkipLastSkipLastImpl() {
        {
            //span経路(配列)・引数違いで加算融合になること
            var expected = Enumerable.SkipLast(Enumerable.SkipLast(Source(),1),2).ToList();
            var actual = Source().SkipLast(1).SkipLast(2).ToList();
            Assert.Equal(expected,actual);
        }
        {
            //span経路・3段連鎖(合計が要素数を超えるケース)
            var expected = Enumerable.SkipLast(Enumerable.SkipLast(Enumerable.SkipLast(Source(),2),2),2).ToList();
            var actual = Source().SkipLast(2).SkipLast(2).SkipLast(2).ToList();
            Assert.Equal(expected,actual);
        }
        {
            //非span経路(Where挟みでQueue経路)
            var expected = Enumerable.SkipLast(Enumerable.SkipLast(Enumerable.Where(Source(),p=>p>0),1),2).ToList();
            var actual = Source().Where(p=>p>0).SkipLast(1).SkipLast(2).ToList();
            Assert.Equal(expected,actual);
        }
    }



    private static int[] Source() => [1,2,3,4];
    private static int[] Second() => [3,4,5];
    private static int[] Duplicates() => [1,2,2,3,3,4];
    private static NonGenericEnumerable CastObjects() => new object[] { 1,2,3 };
    private static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];

    internal sealed record Row(int Key,int Value,string Name);
}
