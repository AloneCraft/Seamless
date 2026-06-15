using System.Linq;

using Test.Tsavorite.Collections.Intercept.System.Linq;


namespace Test.Tsavorite.Collections.Intercept.System.Linq.FragmentCoverage;

public sealed class Shuffle {
    [Fact,Trait("Category","Unit")]
    public static void noArg() {
        GeneratedInterceptorAssert.ContainsMethod<Shuffle>(nameof(noArgImpl));
        var expected=new[]{1,2,2,3,4}.Shuffle().Order().ToList();
        var actual=noArgImpl().Order().ToList();
        Assert.Equal(expected,actual);
    }

    private static List<int> noArgImpl() {
        return new[]{1,2,2,3,4}.Shuffle().ToList();
    }
}
