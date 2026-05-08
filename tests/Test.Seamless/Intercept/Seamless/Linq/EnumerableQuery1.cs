using Seamless.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq;

static class DatabaseGenerator;

public static class EnumerableQuery1 {
    [Fact, Trait("Category","Intercept")]
    public static void GroupBy() {
        Assert.Equal(
            FormatGroups(SystemEnumerable.GroupBy(Rows(),static row => row.Key,static row => row.Value)),
            FormatGroups(Rows().GroupBy(static row => row.Key,static row => row.Value))
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void GroupJoin() {
        Assert.Equal(
            FormatGroupJoin(SystemEnumerable.GroupJoin(Source(),Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValues) => (outerValue,innerValues))),
            FormatGroupJoin(Source().GroupJoin(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValues) => (outerValue,innerValues)))
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Join() {
        Assert.Equal(
            SystemEnumerable.Join(Source(),Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue),
            Source().Join(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void OrderBy() {
        Assert.Equal(
            SystemEnumerable.OrderBy(Rows(),static row => row.Key),
            Rows().OrderBy(static row => row.Key)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void OrderByDescending() {
        Assert.Equal(
            SystemEnumerable.OrderByDescending(Rows(),static row => row.Key),
            Rows().OrderByDescending(static row => row.Key)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Select() {
        {
            Assert.Equal(
                SystemEnumerable.Select(Source(),static value => value*2),
                Source().Select(static value => value*2)
            );
        }
        {
            Assert.Equal(
                SystemEnumerable.Select(Source(),static value => value*2+1),
                Source().Select(static value => value*2+1)
            );
        }
        {
            Assert.Equal(
                SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value*2),static selected => selected+1),
                Source().Select(static value => value*2).Select(static selected => selected+1)
            );
        }
    }

    [Fact, Trait("Category","Intercept")]
    public static void SelectMany() {
        Assert.Equal(
            SystemEnumerable.SelectMany(Source(),static _ => Second(),static (outerValue,innerValue) => outerValue+innerValue),
            Source().SelectMany(static _ => Second(),static (outerValue,innerValue) => outerValue+innerValue)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void ThenBy() {
        Assert.Equal(
            SystemEnumerable.ThenBy(SystemEnumerable.OrderBy(OrderedRows(),static row => row.Key),static row => row.Name),
            OrderedRows().OrderBy(static row => row.Key).ThenBy(static row => row.Name)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void ThenByDescending() {
        Assert.Equal(
            SystemEnumerable.ThenByDescending(SystemEnumerable.OrderBy(OrderedRows(),static row => row.Key),static row => row.Name),
            OrderedRows().OrderBy(static row => row.Key).ThenByDescending(static row => row.Name)
        );
    }

    [Fact, Trait("Category","Intercept")]
    public static void Where() {
        {
            Assert.Equal(
                SystemEnumerable.Where(Source(),static value => value>2),
                Source().Where(static value => value>2)
            );
        }
        {
            Assert.Equal(
                SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*2),static selected => selected>4),
                Source().Select(static value => value*2).Where(static selected => selected>4)
            );
        }
    }

    private static IEnumerable<string> FormatGroups(IEnumerable<global::System.Linq.IGrouping<int,int>> groups) =>
        SystemEnumerable.Select(groups,static group => group.Key+":"+string.Join(",",group));

    private static IEnumerable<string> FormatGroupJoin(IEnumerable<(int outerValue,IEnumerable<int> innerValues)> rows) =>
        SystemEnumerable.Select(rows,static row => row.outerValue+":"+string.Join(",",row.innerValues));

    private static int[] Source() => [1,2,3,4];
    private static int[] Second() => [3,4,5];
    private static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];
    private static Row[] OrderedRows() => [new(1,30,"gamma"),new(1,10,"alpha"),new(2,20,"beta")];

    private sealed record Row(int Key,int Value,string Name);
}
