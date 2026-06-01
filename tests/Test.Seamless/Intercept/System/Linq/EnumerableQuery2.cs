using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableQuery2 {
    [Fact, Trait("Category","Unit")]
    public void Query_Where_Select() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Where(Source(),static v => v>2),static v => v*10).ToList();
        var actual=Query_Where_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Query_Where_Select_Actual() {
        return (from v in Source() select v).Where(v => v>2).Select(v => v*10).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public void Query_OrderBy_Select() {
        var expected=SystemEnumerable.Select(SystemEnumerable.OrderBy(Unordered(),static v => v),static v => v*10).ToList();
        var actual=Query_OrderBy_Select_Actual();
        Assert.Equal(expected,actual);
    }
    private static List<int> Query_OrderBy_Select_Actual() {
        return (from v in Unordered() select v).OrderBy(v => v).Select(v => v*10).ToList();
    }

    private static readonly int[] SourceValue=[1,2,3,4];
    private static int[] Source() => SourceValue;

    private static readonly int[] UnorderedValue=[3,1,4,2];
    private static int[] Unordered() => UnorderedValue;
}
