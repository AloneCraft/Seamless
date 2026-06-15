using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class RightJoin {
    public sealed record Row(int Key,string Name);

    [Fact,Trait("Category","Unit")]
    public static void keySelectors_resultSelector() {
        GeneratedInterceptorAssert.ContainsMethod<RightJoin>(nameof(keySelectors_resultSelectorImpl));
        var outer=new[]{new Row(1,"a"),new Row(2,"b")};
        var inner=new[]{new Row(1,"x"),new Row(1,"y"),new Row(4,"z")};
        var expected=outer.RightJoin(inner,static row => row.Key,static row => row.Key,static (outerRow,innerRow) => $"{outerRow?.Name??"none"}:{innerRow.Name}").ToList();
        var actual=keySelectors_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }

    private static List<string> keySelectors_resultSelectorImpl() {
        var outer=new[]{new Row(1,"a"),new Row(2,"b")};
        var inner=new[]{new Row(1,"x"),new Row(1,"y"),new Row(4,"z")};
        return outer.RightJoin(inner,static row => row.Key,static row => row.Key,static (outerRow,innerRow) => $"{outerRow?.Name??"none"}:{innerRow.Name}").ToList();
    }
}
