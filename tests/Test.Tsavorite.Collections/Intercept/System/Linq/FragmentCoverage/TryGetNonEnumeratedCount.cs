using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class TryGetNonEnumeratedCount {
    [Fact,Trait("Category","Unit")]
    public static void outVar() {
        GeneratedInterceptorAssert.ContainsMethod<TryGetNonEnumeratedCount>(nameof(outVarImpl));
        var expectedResult=new[]{1,2,3}.TryGetNonEnumeratedCount(out var expected);
        var (actualResult,actual)=outVarImpl();
        Assert.Equal(expectedResult,actualResult);
        Assert.Equal(expected,actual);
    }

    private static (bool Result,int Count) outVarImpl() {
        var result=new[]{1,2,3}.TryGetNonEnumeratedCount(out var count);
        return (result,count);
    }
}
