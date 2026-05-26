using System.Linq;
using NonGenericEnumerable = System.Collections.IEnumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class LinqChain1_LinqChain1 {
    [Fact, Trait("Category","Unit")]
    public static void WhereSelect() {
        WhereSelectImpl();
    }
    private static void WhereSelectImpl() {
        var expected = Enumerable.Select(Enumerable.Where(Source(),p=>p>1),p=>p*10);
        var actual = Source().Where(p=>p>1).Select(p=>p*10);
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



    private static int[] Source() => [1,2,3,4];
    private static int[] Second() => [3,4,5];
    private static int[] Duplicates() => [1,2,2,3,3,4];
    private static NonGenericEnumerable CastObjects() => new object[] { 1,2,3 };
    private static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];

    internal sealed record Row(int Key,int Value,string Name);
}
