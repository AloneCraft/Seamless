using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Append {
    [Fact,Trait("Category","Unit")]
    public static void Append_element(){
        GeneratedInterceptorAssert.ContainsMethod<Append>(nameof(Append_elementImpl));
        var expected=SystemEnumerable.Append(new[]{1,2,3,4},99).ToList();
        var actual=Append_elementImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Append_elementImpl(){
        return new[]{1,2,3,4}.Append(99).ToList();
    }
}

public sealed class Prepend {
    [Fact,Trait("Category","Unit")]
    public static void Prepend_element(){
        GeneratedInterceptorAssert.ContainsMethod<Prepend>(nameof(Prepend_elementImpl));
        var expected=SystemEnumerable.Prepend(new[]{1,2,3,4},0).ToList();
        var actual=Prepend_elementImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Prepend_elementImpl(){
        return new[]{1,2,3,4}.Prepend(0).ToList();
    }
}

public sealed class Concat {
    [Fact,Trait("Category","Unit")]
    public static void Concat_second(){
        GeneratedInterceptorAssert.ContainsMethod<Concat>(nameof(Concat_secondImpl));
        var expected=SystemEnumerable.Concat(new[]{1,2,3},new[]{4,5,6}).ToList();
        var actual=Concat_secondImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Concat_secondImpl(){
        return new[]{1,2,3}.Concat(new[]{4,5,6}).ToList();
    }
}

public sealed class Reverse {
    [Fact,Trait("Category","Unit")]
    public static void Reverse_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Reverse>(nameof(Reverse_noArgImpl));
        var expected=SystemEnumerable.Reverse(new[]{1,2,3,4}).ToList();
        var actual=Reverse_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Reverse_noArgImpl(){
        return new[]{1,2,3,4}.Reverse().ToList();
    }
}

public sealed class Zip {
    [Fact,Trait("Category","Unit")]
    public static void Zip_second(){
        GeneratedInterceptorAssert.ContainsMethod<Zip>(nameof(Zip_secondImpl));
        var expected=SystemEnumerable.Zip(new[]{1,2,3},new[]{4,5,6}).ToList();
        var actual=Zip_secondImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int First,int Second)> Zip_secondImpl(){
        return new[]{1,2,3}.Zip(new[]{4,5,6}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Zip3_third(){
        GeneratedInterceptorAssert.ContainsMethod<Zip>(nameof(Zip3_thirdImpl));
        var expected=SystemEnumerable.Zip(new[]{1,2,3},new[]{4,5,6},new[]{7,8,9}).ToList();
        var actual=Zip3_thirdImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int First,int Second,int Third)> Zip3_thirdImpl(){
        return new[]{1,2,3}.Zip(new[]{4,5,6},new[]{7,8,9}).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void Zip_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<Zip>(nameof(Zip_resultSelectorImpl));
        var expected=SystemEnumerable.Zip(new[]{1,2,3},new[]{4,5,6},static (a,b) => a+b).ToList();
        var actual=Zip_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> Zip_resultSelectorImpl(){
        return new[]{1,2,3}.Zip(new[]{4,5,6},static (a,b) => a+b).ToList();
    }
}

public sealed class Index {
    [Fact,Trait("Category","Unit")]
    public static void Index_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Index>(nameof(Index_noArgImpl));
        var expected=SystemEnumerable.Index(new[]{1,2,3,4}).ToList();
        var actual=Index_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<(int Index,int Item)> Index_noArgImpl(){
        return new[]{1,2,3,4}.Index().ToList();
    }
}

public sealed class AsEnumerable {
    [Fact,Trait("Category","Unit")]
    public static void AsEnumerable_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<AsEnumerable>(nameof(AsEnumerable_noArgImpl));
        var expected=SystemEnumerable.AsEnumerable(new[]{1,2,3,4}).ToList();
        var actual=AsEnumerable_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> AsEnumerable_noArgImpl(){
        return new[]{1,2,3,4}.AsEnumerable().ToList();
    }
}

public sealed class DefaultIfEmpty {
    [Fact,Trait("Category","Unit")]
    public static void DefaultIfEmpty_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<DefaultIfEmpty>(nameof(DefaultIfEmpty_noArgImpl));
        var expected=SystemEnumerable.DefaultIfEmpty(new[]{1,2,3,4}).ToList();
        var actual=DefaultIfEmpty_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static List<int> DefaultIfEmpty_noArgImpl(){
        return new[]{1,2,3,4}.DefaultIfEmpty().ToList();
    }
}
