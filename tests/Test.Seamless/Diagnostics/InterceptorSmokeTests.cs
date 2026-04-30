using System.Linq;

namespace Test.Seamless.Diagnostics;

public class InterceptorSmokeTests {
    [Fact]
    public void ListWhere_Interceptor_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };
        foreach(var a in numbers.Where(n=>n>2)){

        }
        var filtered = numbers.Where(n => n>2).ToArray();

        Assert.Equal(new[] { 3,4,5 },filtered);
    }

    [Fact]
    public void ListWhere_Index_Interceptor_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };

        var filtered = numbers.Where((value,index) => index>=3).ToArray();

        Assert.Equal(new[] { 4,5 },filtered);
    }

    [Fact]
    public void QuerySyntax_Where_Helper_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };

        var filtered = from value in numbers
                       where value>2
                       select value;

        var filteredTypeName = filtered.GetType().FullName;
        Assert.NotNull(filteredTypeName);
        Assert.Contains("OptimizedListWhereEnumerable",filteredTypeName!);
        Assert.Equal(new[] { 3,4,5 },filtered.ToArray());
    }

    [Fact]
    public void QuerySyntax_Where_With_Projection_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };

        var filtered = from value in numbers
                       where value>2
                       select value*10;

        Assert.Equal(new[] { 30,40,50 },filtered.ToArray());
    }

    [Fact]
    public void ListWhere_Interceptor_String_Smoke() {
        var values = new List<string> { "a","bb","ccc" };

        var filtered = values.Where(value => value.Length>1).ToArray();

        Assert.Equal(new[] { "bb","ccc" },filtered);
    }

    [Fact]
    public void EnumerableWhere_StaticCall_Interceptor_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };

        var filtered = Enumerable.Where(numbers,n => (n%2)==0).ToArray();

        Assert.Equal(new[] { 2,4 },filtered);
    }

    [Fact]
    public void EnumerableWhere_StaticCall_Index_Interceptor_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };

        var filtered = Enumerable.Where(numbers,(value,index) => index>=3).ToArray();

        Assert.Equal(new[] { 4,5 },filtered);
    }

    [Fact]
    public void ListWhere_MethodGroup_Interceptor_Smoke() {
        var numbers = new List<int> { 1,2,3,4,5 };

        var filtered = numbers.Where(IsGreaterThanTwo).ToArray();

        Assert.Equal(new[] { 3,4,5 },filtered);

        static bool IsGreaterThanTwo(int value) => value>2;
    }
}
