using System.Linq;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class LinqChain1_Lambda {
    [Fact, Trait("Category","Unit")]
    public static void Where() {
        WhereImpl();
    }
    private static void WhereImpl() {
        var expected = Enumerable.Where(Source(), p=>Enumerable.Any(Enumerable.Where(Source2(), q=>q==p))).ToList();
        var actual = Source().Where(p=>Source2().Where(q=>q==p).Any()).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void Select() {
        SelectImpl();
    }
    private static void SelectImpl() {
        var expected = Enumerable.Select(Source(), p=>Enumerable.Count(Enumerable.Select(Source2(), q=>q+p))).ToList();
        var actual = Source().Select(p=>Source2().Select(q=>q+p).Count()).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void All() {
        AllImpl();
    }
    private static void AllImpl() {
        var expected = Enumerable.All(Source(), p=>Enumerable.All(Source2(), q=>q>0));
        var actual = Source().All(p=>Source2().All(q=>q>0));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void Any() {
        AnyImpl();
    }
    private static void AnyImpl() {
        var expected = Enumerable.Any(Source(), p=>Enumerable.Any(Source2(), q=>q==p));
        var actual = Source().Any(p=>Source2().Any(q=>q==p));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void Count() {
        CountImpl();
    }
    private static void CountImpl() {
        var expected = Enumerable.Count(Source(), p=>Enumerable.Count(Source2(), q=>q==p)>0);
        var actual = Source().Count(p=>Source2().Count(q=>q==p)>0);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void First() {
        FirstImpl();
    }
    private static void FirstImpl() {
        var expected = Enumerable.First(Source(), p=>Enumerable.First(Source2(), q=>q==p)==p);
        var actual = Source().First(p=>Source2().First(q=>q==p)==p);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void FirstOrDefault() {
        FirstOrDefaultImpl();
    }
    private static void FirstOrDefaultImpl() {
        var expected = Enumerable.FirstOrDefault(Source(), p=>Enumerable.FirstOrDefault(Source2(), q=>q==p)==p);
        var actual = Source().FirstOrDefault(p=>Source2().FirstOrDefault(q=>q==p)==p);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void Last() {
        LastImpl();
    }
    private static void LastImpl() {
        var expected = Enumerable.Last(Source(), p=>Enumerable.Last(Source2(), q=>q==p)==p);
        var actual = Source().Last(p=>Source2().Last(q=>q==p)==p);
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void SkipWhile() {
        SkipWhileImpl();
    }
    private static void SkipWhileImpl() {
        var expected = Enumerable.SkipWhile(Source(), p=>Enumerable.Any(Enumerable.SkipWhile(Source2(), q=>q<p))).ToList();
        var actual = Source().SkipWhile(p=>Source2().SkipWhile(q=>q<p).Any()).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void TakeWhile() {
        TakeWhileImpl();
    }
    private static void TakeWhileImpl() {
        var expected = Enumerable.TakeWhile(Source(), p=>Enumerable.Any(Enumerable.TakeWhile(Source2(), q=>q<=p))).ToList();
        var actual = Source().TakeWhile(p=>Source2().TakeWhile(q=>q<=p).Any()).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void Sum() {
        SumImpl();
    }
    private static void SumImpl() {
        var expected = Enumerable.Sum<int>(Source(), p=>Enumerable.Sum(Source2(), int (q)=>q+p));
        var actual = Source().Sum(p=>Source2().Sum(q=>q+p));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void Max() {
        MaxImpl();
    }
    private static void MaxImpl() {
        var expected = Enumerable.Max<int,int>(Source(), p=>Enumerable.Max(Source2(), int (q)=>q+p));
        var actual = Source().Max(p=>Source2().Max(q=>q+p));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void Average() {
        AverageImpl();
    }
    private static void AverageImpl() {
        var expected = Enumerable.Average<int>(Source(), p=>Enumerable.Average(Source2(), int (q)=>q+p));
        var actual = Source().Average(p=>Source2().Average(q=>q+p));
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void OrderBy() {
        OrderByImpl();
    }
    private static void OrderByImpl() {
        var expected = Enumerable.OrderBy(Source(), p=>Enumerable.First(Enumerable.OrderBy(Source2(), q=>q+p))).ToList();
        var actual = Source().OrderBy(p=>Source2().OrderBy(q=>q+p).First()).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void GroupBy() {
        GroupByImpl();
    }
    private static void GroupByImpl() {
        var expected = Enumerable.Select(Enumerable.GroupBy(Source(), p=>Enumerable.Count(Enumerable.GroupBy(Source2(), q=>q%p))), group=>group.Key).ToList();
        var actual = Source().GroupBy(p=>Source2().GroupBy(q=>q%p).Count()).Select(group=>group.Key).ToList();
        Assert.Equal(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public static void DistinctBy() {
        DistinctByImpl();
    }
    private static void DistinctByImpl() {
        var expected = Enumerable.DistinctBy(Source(), p=>Enumerable.Count(Enumerable.DistinctBy(Source2(), q=>q%p))).ToList();
        var actual = Source().DistinctBy(p=>Source2().DistinctBy(q=>q%p).Count()).ToList();
        Assert.Equal(expected,actual);
    }

    private static int[] Source() => [1,2,3,4];
    private static int[] Source2() => [1,2,3,4];
}
