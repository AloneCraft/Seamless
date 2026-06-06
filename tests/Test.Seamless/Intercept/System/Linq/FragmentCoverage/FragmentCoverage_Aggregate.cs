using System.Collections.Generic;
using System.Linq;
using SystemEnumerable=System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.FragmentCoverage;

public sealed class Aggregate {
    [Fact,Trait("Category","Unit")]
    public static void Aggregate_func(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(Aggregate_funcImpl));
        var expected=SystemEnumerable.Aggregate(new[]{1,2,3,4},static (a,v) => a+v);
        var actual=Aggregate_funcImpl();
        Assert.Equal(expected,actual);
    }
    private static int Aggregate_funcImpl(){
        return new[]{1,2,3,4}.Aggregate(static (a,v) => a+v);
    }

    [Fact,Trait("Category","Unit")]
    public static void Aggregate_seed_func(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(Aggregate_seed_funcImpl));
        var expected=SystemEnumerable.Aggregate(new[]{1,2,3,4},0,static (a,v) => a+v);
        var actual=Aggregate_seed_funcImpl();
        Assert.Equal(expected,actual);
    }
    private static int Aggregate_seed_funcImpl(){
        return new[]{1,2,3,4}.Aggregate(0,static (a,v) => a+v);
    }

    [Fact,Trait("Category","Unit")]
    public static void Aggregate_seed_func_resultSelector(){
        GeneratedInterceptorAssert.ContainsMethod<Aggregate>(nameof(Aggregate_seed_func_resultSelectorImpl));
        var expected=SystemEnumerable.Aggregate(new[]{1,2,3,4},0,static (a,v) => a+v,static r => r*2);
        var actual=Aggregate_seed_func_resultSelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static int Aggregate_seed_func_resultSelectorImpl(){
        return new[]{1,2,3,4}.Aggregate(0,static (a,v) => a+v,static r => r*2);
    }
}

public sealed class AggregateBy {
    [Fact,Trait("Category","Unit")]
    public static void AggregateBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<AggregateBy>(nameof(AggregateBy_keySelectorImpl));
        var expected=SystemEnumerable.AggregateBy(new[]{1,2,3,4},static v => v%2,0,static (a,v) => a+v).OrderBy(static kv => kv.Key).ToList();
        var actual=AggregateBy_keySelectorImpl().OrderBy(static kv => kv.Key).ToList();
        Assert.Equal(expected,actual);
    }
    private static List<KeyValuePair<int,int>> AggregateBy_keySelectorImpl(){
        return new[]{1,2,3,4}.AggregateBy(static v => v%2,0,static (a,v) => a+v).ToList();
    }
}

public sealed class All {
    [Fact,Trait("Category","Unit")]
    public static void All_predicate(){
        GeneratedInterceptorAssert.ContainsMethod<All>(nameof(All_predicateImpl));
        var expected=SystemEnumerable.All(new[]{1,2,3,4},static v => v>0);
        var actual=All_predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static bool All_predicateImpl(){
        return new[]{1,2,3,4}.All(static v => v>0);
    }
}

public sealed class Any {
    [Fact,Trait("Category","Unit")]
    public static void Any_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Any>(nameof(Any_noArgImpl));
        var expected=SystemEnumerable.Any(new[]{1,2,3,4});
        var actual=Any_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static bool Any_noArgImpl(){
        return new[]{1,2,3,4}.Any();
    }

    [Fact,Trait("Category","Unit")]
    public static void Any_predicate(){
        GeneratedInterceptorAssert.ContainsMethod<Any>(nameof(Any_predicateImpl));
        var expected=SystemEnumerable.Any(new[]{1,2,3,4},static v => v>2);
        var actual=Any_predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static bool Any_predicateImpl(){
        return new[]{1,2,3,4}.Any(static v => v>2);
    }
}

public sealed class Count {
    [Fact,Trait("Category","Unit")]
    public static void Count_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(Count_noArgImpl));
        var expected=SystemEnumerable.Count(new[]{1,2,3,4});
        var actual=Count_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int Count_noArgImpl(){
        return new[]{1,2,3,4}.Count();
    }

    [Fact,Trait("Category","Unit")]
    public static void Count_predicate(){
        GeneratedInterceptorAssert.ContainsMethod<Count>(nameof(Count_predicateImpl));
        var expected=SystemEnumerable.Count(new[]{1,2,3,4},static v => v>2);
        var actual=Count_predicateImpl();
        Assert.Equal(expected,actual);
    }
    private static int Count_predicateImpl(){
        return new[]{1,2,3,4}.Count(static v => v>2);
    }
}

public sealed class LongCount {
    [Fact,Trait("Category","Unit")]
    public static void LongCount_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<LongCount>(nameof(LongCount_noArgImpl));
        var expected=SystemEnumerable.LongCount(new[]{1,2,3,4});
        var actual=LongCount_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static long LongCount_noArgImpl(){
        return new[]{1,2,3,4}.LongCount();
    }
}

public sealed class Sum {
    [Fact,Trait("Category","Unit")]
    public static void Sum_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Sum>(nameof(Sum_noArgImpl));
        var expected=SystemEnumerable.Sum(new[]{1,2,3,4});
        var actual=Sum_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int Sum_noArgImpl(){
        return new[]{1,2,3,4}.Sum();
    }

    [Fact,Trait("Category","Unit")]
    public static void Sum_selector(){
        GeneratedInterceptorAssert.ContainsMethod<Sum>(nameof(Sum_selectorImpl));
        var expected=SystemEnumerable.Sum(new[]{1,2,3,4},static v => v*2);
        var actual=Sum_selectorImpl();
        Assert.Equal(expected,actual);
    }
    private static int Sum_selectorImpl(){
        return new[]{1,2,3,4}.Sum(static v => v*2);
    }
}

public sealed class Average {
    [Fact,Trait("Category","Unit")]
    public static void Average_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Average>(nameof(Average_noArgImpl));
        var expected=SystemEnumerable.Average(new[]{1,2,3,4});
        var actual=Average_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static double Average_noArgImpl(){
        return new[]{1,2,3,4}.Average();
    }

    [Fact,Trait("Category","Unit")]
    public static void Average_selector(){
        GeneratedInterceptorAssert.ContainsMethod<Average>(nameof(Average_selectorImpl));
        var expected=SystemEnumerable.Average(new[]{1,2,3,4},static v => v*2);
        var actual=Average_selectorImpl();
        Assert.Equal(expected,actual);
    }
    private static double Average_selectorImpl(){
        return new[]{1,2,3,4}.Average(static v => v*2);
    }
}

public sealed class Min {
    [Fact,Trait("Category","Unit")]
    public static void Min_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Min>(nameof(Min_noArgImpl));
        var expected=SystemEnumerable.Min(new[]{1,2,3,4});
        var actual=Min_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int Min_noArgImpl(){
        return new[]{1,2,3,4}.Min();
    }
}

public sealed class Max {
    [Fact,Trait("Category","Unit")]
    public static void Max_noArg(){
        GeneratedInterceptorAssert.ContainsMethod<Max>(nameof(Max_noArgImpl));
        var expected=SystemEnumerable.Max(new[]{1,2,3,4});
        var actual=Max_noArgImpl();
        Assert.Equal(expected,actual);
    }
    private static int Max_noArgImpl(){
        return new[]{1,2,3,4}.Max();
    }
}

public sealed class MinBy {
    [Fact,Trait("Category","Unit")]
    public static void MinBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<MinBy>(nameof(MinBy_keySelectorImpl));
        var expected=SystemEnumerable.MinBy(new[]{1,2,3,4},static v => v);
        var actual=MinBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static int MinBy_keySelectorImpl(){
        return new[]{1,2,3,4}.MinBy(static v => v);
    }
}

public sealed class MaxBy {
    [Fact,Trait("Category","Unit")]
    public static void MaxBy_keySelector(){
        GeneratedInterceptorAssert.ContainsMethod<MaxBy>(nameof(MaxBy_keySelectorImpl));
        var expected=SystemEnumerable.MaxBy(new[]{1,2,3,4},static v => v);
        var actual=MaxBy_keySelectorImpl();
        Assert.Equal(expected,actual);
    }
    private static int MaxBy_keySelectorImpl(){
        return new[]{1,2,3,4}.MaxBy(static v => v);
    }
}

