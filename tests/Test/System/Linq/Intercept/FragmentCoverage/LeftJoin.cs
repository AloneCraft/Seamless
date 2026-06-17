using System.Linq;

using Test.System.Linq.Intercept;


namespace Test.System.Linq.Intercept.FragmentCoverage;

public sealed class LeftJoin {
    public sealed record Row(int Key,string Name);

    [Fact,Trait("Category","Unit")]
    public static void keySelectors_resultSelector() {
        GeneratedInterceptorAssert.ContainsMethod<LeftJoin>(nameof(keySelectors_resultSelectorImpl));
        var outer=new[]{new Row(1,"a"),new Row(2,"b"),new Row(3,"c")};
        var inner=new[]{new Row(1,"x"),new Row(1,"y"),new Row(4,"z")};
        var expected=outer.LeftJoin(inner,static row => row.Key,static row => row.Key,static (outerRow,innerRow) => $"{outerRow.Name}:{innerRow?.Name??"none"}").ToList();
        var actual=keySelectors_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }

    private static List<string> keySelectors_resultSelectorImpl() {
        var outer=new[]{new Row(1,"a"),new Row(2,"b"),new Row(3,"c")};
        var inner=new[]{new Row(1,"x"),new Row(1,"y"),new Row(4,"z")};
        return outer.LeftJoin(inner,static row => row.Key,static row => row.Key,static (outerRow,innerRow) => $"{outerRow.Name}:{innerRow?.Name??"none"}").ToList();
    }
}
