using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class First {
    [Fact,Trait("Category","Unit")]
    public static void First_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<First>(nameof(First_noArgImpl));
        var expected=SystemEnumerable.First(new[]{1,2,3,4});
        var actual=First_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int First_noArgImpl(){
        return new[]{1,2,3,4}.First();
    }
}

public sealed class FirstOrDefault {
    [Fact,Trait("Category","Unit")]
    public static void FirstOrDefault_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<FirstOrDefault>(nameof(FirstOrDefault_noArgImpl));
        var expected=SystemEnumerable.FirstOrDefault(new[]{1,2,3,4});
        var actual=FirstOrDefault_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int FirstOrDefault_noArgImpl(){
        return new[]{1,2,3,4}.FirstOrDefault();
    }
}

public sealed class Last {
    [Fact,Trait("Category","Unit")]
    public static void Last_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Last>(nameof(Last_noArgImpl));
        var expected=SystemEnumerable.Last(new[]{1,2,3,4});
        var actual=Last_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int Last_noArgImpl(){
        return new[]{1,2,3,4}.Last();
    }
}

public sealed class LastOrDefault {
    [Fact,Trait("Category","Unit")]
    public static void LastOrDefault_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<LastOrDefault>(nameof(LastOrDefault_noArgImpl));
        var expected=SystemEnumerable.LastOrDefault(new[]{1,2,3,4});
        var actual=LastOrDefault_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int LastOrDefault_noArgImpl(){
        return new[]{1,2,3,4}.LastOrDefault();
    }
}

public sealed class Single {
    [Fact,Trait("Category","Unit")]
    public static void Single_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Single>(nameof(Single_noArgImpl));
        var expected=SystemEnumerable.Single(new[]{42});
        var actual=Single_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int Single_noArgImpl(){
        return new[]{42}.Single();
    }
}

public sealed class SingleOrDefault {
    [Fact,Trait("Category","Unit")]
    public static void SingleOrDefault_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<SingleOrDefault>(nameof(SingleOrDefault_noArgImpl));
        var expected=SystemEnumerable.SingleOrDefault(new[]{42});
        var actual=SingleOrDefault_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int SingleOrDefault_noArgImpl(){
        return new[]{42}.SingleOrDefault();
    }
}

public sealed class ElementAt {
    [Fact,Trait("Category","Unit")]
    public static void ElementAt_index(){
        GeneratedInterceptorAssert.ContainsMethod<ElementAt>(nameof(ElementAt_indexImpl));
        var expected=SystemEnumerable.ElementAt(new[]{1,2,3,4},1);
        var actual=ElementAt_indexImpl();
        Assert.Equal(expected,actual);
    }
    private static int ElementAt_indexImpl(){
        return new[]{1,2,3,4}.ElementAt(1);
    }
}

public sealed class ElementAtOrDefault {
    [Fact,Trait("Category","Unit")]
    public static void ElementAtOrDefault_index(){
        GeneratedInterceptorAssert.ContainsMethod<ElementAtOrDefault>(nameof(ElementAtOrDefault_indexImpl));
        var expected=SystemEnumerable.ElementAtOrDefault(new[]{1,2,3,4},1);
        var actual=ElementAtOrDefault_indexImpl();
        Assert.Equal(expected,actual);
    }
    private static int ElementAtOrDefault_indexImpl(){
        return new[]{1,2,3,4}.ElementAtOrDefault(1);
    }
}
