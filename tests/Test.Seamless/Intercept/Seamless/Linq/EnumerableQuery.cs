//using System.Linq;
//using NonGenericEnumerable = System.Collections.IEnumerable;
using Seamless.Linq;
//using e = System.Linq.Enumerable;
using a = Seamless.Linq.Enumerable;

namespace Test.Seamless.Intercept.Seamless.Linq;

static class DatabaseGenerator;

public sealed class EnumerableQuery{
    private static void GroupBy_keySelector() {
        Assert.Equal(
            from row in Rows() group row.Value by row.Key,
            Rows().GroupBy(static row => row.Key,static row => row.Value)
        );
    }
    private static void GroupJoin_keySelectors_resultSelector() =>Assert.Equal(
        from outerValue in Source() join innerValue in Source() on outerValue equals innerValue into innerValues select (outerValue, innerValues),
        Source().GroupJoin(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValues) => (outerValue,innerValues))
    );

    private static void Join_keySelectors_resultSelector() =>Assert.Equal(
        from outerValue in Source() join innerValue in Second() on outerValue equals innerValue select outerValue+innerValue,
        Source().Join(Second(),static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue)
    );
    private static void Select_selector()=>Assert.Equal(
        from value in Source() select value*2,
        Source().Select(static value=>value*2)
    );

    private static void SelectMany_collectionSelector_resultSelector()=>Assert.Equal(
        from outerValue in Source() from innerValue in Second() select outerValue+innerValue,
        a.SelectMany(Source(),static value => new[] { value,value+10 },static (outerValue,innerValue) => outerValue+innerValue)

//        Source().SelectMany(static value=>new[]{value,value+10},static (outerValue,innerValue)=>outerValue+innerValue)
    );
    private static void Where_predicate()=>Assert.Equal(
        from value in Source() where value>2 select value,
        Source().Where(static value=>value>2)
    );

    private static int[] Source() => [1,2,3,4];
    private static int[] Second() => [3,4,5];
    private static int[] Third() => [10,20,30];
    private static int[] Duplicates() => [1,2,2,3,3,4];
    private static int[] SingleSource() => [2];
    private static IEnumerable CastObjects() => new object[] { 1,2,3 };
    private static object[] MixedObjects() => [1,"skip",2,3.0,4];
    private static Row[] Rows() => [new(1,10,"beta"),new(2,20,"alpha"),new(3,30,"gamma")];
    private static Row[] OtherRows() => [new(3,300,"gamma-2"),new(4,40,"delta")];

    private sealed record Row(int Key,int Value,string Name);

    [Fact, Trait("Category","Unit")]
    public void SmokeTest() {
        //Assert.True(new Enumerable().Smoke()>0);
    }
}
