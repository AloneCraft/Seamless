namespace Test.System.Linq.Intercept.Optimization;

public sealed class WherePredicateFusion:OptimizationTestBase {
    [Fact, Trait("Category","Unit")]
    public static void Count() {
        Assert.Equal(Numbers().Where(static x => x>4).Count(),Count_MatchOptimized());
        Assert.Equal(Numbers().Where(static x => x>4).Count(),Count_MatchNoOptimized());
        Assert.Equal(Numbers().Where(static x => x>100).Count(),Count_NoMatchOptimized());
        Assert.Equal(Numbers().Where(static x => x>100).Count(),Count_NoMatchNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int Count_MatchOptimized() => Numbers().Where(static x => x>4).Count();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int Count_MatchNoOptimized() => Numbers().Where(static x => x>4).Count();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int Count_NoMatchOptimized() => Numbers().Where(static x => x>100).Count();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int Count_NoMatchNoOptimized() => Numbers().Where(static x => x>100).Count();

    [Fact, Trait("Category","Unit")]
    public static void LongCount() {
        Assert.Equal(Numbers().Where(static x => x>4).LongCount(),LongCountOptimized());
        Assert.Equal(Numbers().Where(static x => x>4).LongCount(),LongCountNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static long LongCountOptimized() => Numbers().Where(static x => x>4).LongCount();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static long LongCountNoOptimized() => Numbers().Where(static x => x>4).LongCount();

    [Fact, Trait("Category","Unit")]
    public static void Any() {
        Assert.Equal(Numbers().Where(static x => x>4).Any(),Any_MatchOptimized());
        Assert.Equal(Numbers().Where(static x => x>4).Any(),Any_MatchNoOptimized());
        Assert.Equal(Numbers().Where(static x => x>100).Any(),Any_NoMatchOptimized());
        Assert.Equal(Numbers().Where(static x => x>100).Any(),Any_NoMatchNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static bool Any_MatchOptimized() => Numbers().Where(static x => x>4).Any();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static bool Any_MatchNoOptimized() => Numbers().Where(static x => x>4).Any();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static bool Any_NoMatchOptimized() => Numbers().Where(static x => x>100).Any();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static bool Any_NoMatchNoOptimized() => Numbers().Where(static x => x>100).Any();

    [Fact, Trait("Category","Unit")]
    public static void First() {
        Assert.Equal(Numbers().Where(static x => x>4).First(),First_MatchOptimized());
        Assert.Equal(Numbers().Where(static x => x>4).First(),First_MatchNoOptimized());
        Assert.Throws<InvalidOperationException>(static () => Numbers().Where(static x => x>100).First());
        Assert.Throws<InvalidOperationException>(static () => First_NoMatchOptimized());
        Assert.Throws<InvalidOperationException>(static () => First_NoMatchNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int First_MatchOptimized() => Numbers().Where(static x => x>4).First();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int First_MatchNoOptimized() => Numbers().Where(static x => x>4).First();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int First_NoMatchOptimized() => Numbers().Where(static x => x>100).First();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int First_NoMatchNoOptimized() => Numbers().Where(static x => x>100).First();

    [Fact, Trait("Category","Unit")]
    public static void FirstOrDefault() {
        Assert.Equal(Numbers().Where(static x => x>4).FirstOrDefault(),FirstOrDefault_MatchOptimized());
        Assert.Equal(Numbers().Where(static x => x>4).FirstOrDefault(),FirstOrDefault_MatchNoOptimized());
        Assert.Equal(Numbers().Where(static x => x>100).FirstOrDefault(),FirstOrDefault_NoMatchOptimized());
        Assert.Equal(Numbers().Where(static x => x>100).FirstOrDefault(),FirstOrDefault_NoMatchNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int FirstOrDefault_MatchOptimized() => Numbers().Where(static x => x>4).FirstOrDefault();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int FirstOrDefault_MatchNoOptimized() => Numbers().Where(static x => x>4).FirstOrDefault();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int FirstOrDefault_NoMatchOptimized() => Numbers().Where(static x => x>100).FirstOrDefault();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int FirstOrDefault_NoMatchNoOptimized() => Numbers().Where(static x => x>100).FirstOrDefault();

    [Fact, Trait("Category","Unit")]
    public static void Last() {
        Assert.Equal(Numbers().Where(static x => x>4).Last(),LastOptimized());
        Assert.Equal(Numbers().Where(static x => x>4).Last(),LastNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int LastOptimized() => Numbers().Where(static x => x>4).Last();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int LastNoOptimized() => Numbers().Where(static x => x>4).Last();

    [Fact, Trait("Category","Unit")]
    public static void LastOrDefault() {
        Assert.Equal(Numbers().Where(static x => x>4).LastOrDefault(),LastOrDefault_MatchOptimized());
        Assert.Equal(Numbers().Where(static x => x>4).LastOrDefault(),LastOrDefault_MatchNoOptimized());
        Assert.Equal(Numbers().Where(static x => x>100).LastOrDefault(),LastOrDefault_NoMatchOptimized());
        Assert.Equal(Numbers().Where(static x => x>100).LastOrDefault(),LastOrDefault_NoMatchNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int LastOrDefault_MatchOptimized() => Numbers().Where(static x => x>4).LastOrDefault();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int LastOrDefault_MatchNoOptimized() => Numbers().Where(static x => x>4).LastOrDefault();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int LastOrDefault_NoMatchOptimized() => Numbers().Where(static x => x>100).LastOrDefault();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int LastOrDefault_NoMatchNoOptimized() => Numbers().Where(static x => x>100).LastOrDefault();

    [Fact, Trait("Category","Unit")]
    public static void Single() {
        Assert.Equal(Numbers().Where(static x => x==8).Single(),Single_MatchOptimized());
        Assert.Equal(Numbers().Where(static x => x==8).Single(),Single_MatchNoOptimized());
        Assert.Throws<InvalidOperationException>(static () => Numbers().Where(static x => x>4).Single());
        Assert.Throws<InvalidOperationException>(static () => Single_MultipleOptimized());
        Assert.Throws<InvalidOperationException>(static () => Single_MultipleNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int Single_MatchOptimized() => Numbers().Where(static x => x==8).Single();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int Single_MatchNoOptimized() => Numbers().Where(static x => x==8).Single();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int Single_MultipleOptimized() => Numbers().Where(static x => x>4).Single();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int Single_MultipleNoOptimized() => Numbers().Where(static x => x>4).Single();

    [Fact, Trait("Category","Unit")]
    public static void SingleOrDefault() {
        Assert.Equal(Numbers().Where(static x => x==8).SingleOrDefault(),SingleOrDefault_MatchOptimized());
        Assert.Equal(Numbers().Where(static x => x==8).SingleOrDefault(),SingleOrDefault_MatchNoOptimized());
        Assert.Equal(Numbers().Where(static x => x>100).SingleOrDefault(),SingleOrDefault_NoMatchOptimized());
        Assert.Equal(Numbers().Where(static x => x>100).SingleOrDefault(),SingleOrDefault_NoMatchNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int SingleOrDefault_MatchOptimized() => Numbers().Where(static x => x==8).SingleOrDefault();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int SingleOrDefault_MatchNoOptimized() => Numbers().Where(static x => x==8).SingleOrDefault();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int SingleOrDefault_NoMatchOptimized() => Numbers().Where(static x => x>100).SingleOrDefault();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static int SingleOrDefault_NoMatchNoOptimized() => Numbers().Where(static x => x>100).SingleOrDefault();
}
