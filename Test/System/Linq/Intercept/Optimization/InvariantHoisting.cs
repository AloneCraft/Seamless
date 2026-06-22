using System.Linq;
using Test.System.Linq.Intercept;
using Seamless.Collections.Tsavorite.Linq;

namespace Test.System.Linq.Intercept.Optimization;

public sealed class InvariantHoisting:OptimizationTestBase{
    private static int mutatingCallCount;

    [Fact,Trait("Category","Unit")]
    public static void GlobalInvariant() {
        AssertSequenceEqual(Outer().Select(static a => a+StaticCheck()).ToList(),GlobalInvariant_NonEmptyOptimized());
        AssertSequenceEqual(Outer().Select(static a => a+StaticCheck()).ToList(),GlobalInvariant_NonEmptyNoOptimized());
        AssertSequenceEqual(EmptyOuter().Select(static a => a+StaticCheck()).ToList(),GlobalInvariant_EmptyOptimized());
        AssertSequenceEqual(EmptyOuter().Select(static a => a+StaticCheck()).ToList(),GlobalInvariant_EmptyNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> GlobalInvariant_NonEmptyOptimized() => Outer().Select(static a => a+StaticCheck()).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> GlobalInvariant_NonEmptyNoOptimized() => Outer().Select(static a => a+StaticCheck()).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> GlobalInvariant_EmptyOptimized() => EmptyOuter().Select(static a => a+StaticCheck()).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> GlobalInvariant_EmptyNoOptimized() => EmptyOuter().Select(static a => a+StaticCheck()).ToList();

    [Fact,Trait("Category","Unit")]
    public static void IntermediateScopeInvariant() {
        AssertSequenceEqual(Outer().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList(),IntermediateScopeInvariant_NonEmptyOptimized());
        AssertSequenceEqual(Outer().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList(),IntermediateScopeInvariant_NonEmptyNoOptimized());
        AssertSequenceEqual(EmptyOuter().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList(),IntermediateScopeInvariant_EmptyOptimized());
        AssertSequenceEqual(EmptyOuter().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList(),IntermediateScopeInvariant_EmptyNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> IntermediateScopeInvariant_NonEmptyOptimized() =>
        Outer().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> IntermediateScopeInvariant_NonEmptyNoOptimized() =>
        Outer().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> IntermediateScopeInvariant_EmptyOptimized() =>
        EmptyOuter().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> IntermediateScopeInvariant_EmptyNoOptimized() =>
        EmptyOuter().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList();

    [Fact,Trait("Category","Unit")]
    public static void CapturedLocalEliminatedToStructField() {
        var threshold=3;
        AssertSequenceEqual(Outer().Where(v => v<threshold).ToList(),CapturedLocalEliminatedToStructFieldOptimized());
        AssertSequenceEqual(Outer().Where(v => v<threshold).ToList(),CapturedLocalEliminatedToStructFieldNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> CapturedLocalEliminatedToStructFieldOptimized() {
        var threshold=3;
        return Outer().Where(v => v<threshold).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> CapturedLocalEliminatedToStructFieldNoOptimized() {
        var threshold=3;
        return Outer().Where(v => v<threshold).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void InvariantMayEvaluateBeforeLazyGuard() {
        Assert.Throws<DivideByZeroException>(() => InvariantNotHoistedPastLazyGuardOptimized());
        Assert.Empty(InvariantNotHoistedPastLazyGuardNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> InvariantNotHoistedPastLazyGuardOptimized() {
        var divisor=0;
        return EmptyOuter().Where(v => v>100/divisor).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> InvariantNotHoistedPastLazyGuardNoOptimized() {
        var divisor=0;
        return EmptyOuter().Where(v => v>100/divisor).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void PartialExpressionHoistedForSelect() {
        AssertSequenceEqual(SelectPartialInvariantNoOptimized(),SelectPartialInvariantOptimized());
        var generated=GeneratedInterceptorAssert.SourceFor<InvariantHoisting>(nameof(SelectPartialInvariantOptimized));
        Assert.Contains("__hoist",generated);
    }

    [Fact,Trait("Category","Unit")]
    public static void PartialExpressionHoistedAcrossDelegateKinds() {
        AssertSequenceEqual(WherePartialInvariantNoOptimized(),WherePartialInvariantOptimized());
        AssertSequenceEqual(GroupByPartialInvariantNoOptimized(),GroupByPartialInvariantOptimized());
        AssertSequenceEqual(JoinPartialInvariantNoOptimized(),JoinPartialInvariantOptimized());
    }

    [Fact,Trait("Category","Unit")]
    public static void PartialExpressionHoistEvaluatesOncePerEnumeratorConstruction() {
        mutatingCallCount=0;
        AssertSequenceEqual([8,9,10,11,12],MutatingPartialInvariantOptimized());
        Assert.Equal(1,mutatingCallCount);

        mutatingCallCount=0;
        AssertSequenceEqual([8,9,10,11,12],MutatingPartialInvariantNoOptimized());
        Assert.Equal(Outer().Length,mutatingCallCount);
    }

    [Fact,Trait("Category","Unit")]
    public static void PartialExpressionHoistEvaluatesBeforeEmptySourceEnumeration() {
        mutatingCallCount=0;
        Assert.Empty(EmptySourceMutatingPartialInvariantOptimized());
        Assert.Equal(1,mutatingCallCount);
    }

    [Fact,Trait("Category","Unit")]
    public static void AnchorPreventsPartialExpressionHoist() {
        mutatingCallCount=0;
        Assert.Empty(AnchorMutatingPartialInvariantOptimized());
        Assert.Equal(0,mutatingCallCount);
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> SelectPartialInvariantOptimized() {
        var x=3;
        return Outer().Select(p => p+x*x).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> SelectPartialInvariantNoOptimized() {
        var x=3;
        return Outer().Select(p => p+x*x).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> WherePartialInvariantOptimized() {
        var threshold=1;
        var offset=2;
        return Outer().Where(p => p>threshold+offset).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> WherePartialInvariantNoOptimized() {
        var threshold=1;
        var offset=2;
        return Outer().Where(p => p>threshold+offset).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> GroupByPartialInvariantOptimized() {
        var x=2;
        return Outers()
            .GroupBy(p => p.Key,p => p.Id+x*x,(key,values) => key+values.Sum()+x*x)
            .OrderBy(value => value)
            .ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> GroupByPartialInvariantNoOptimized() {
        var x=2;
        return Outers()
            .GroupBy(p => p.Key,p => p.Id+x*x,(key,values) => key+values.Sum()+x*x)
            .OrderBy(value => value)
            .ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> JoinPartialInvariantOptimized() {
        var x=2;
        var y=2;
        return Outers()
            .Join(Inners(),outer => outer.Key+x*x,inner => inner.Key+y*y,(outer,inner) => outer.Id+inner.Id+x*x)
            .OrderBy(value => value)
            .ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> JoinPartialInvariantNoOptimized() {
        var x=2;
        var y=2;
        return Outers()
            .Join(Inners(),outer => outer.Key+x*x,inner => inner.Key+y*y,(outer,inner) => outer.Id+inner.Id+x*x)
            .OrderBy(value => value)
            .ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> MutatingPartialInvariantOptimized() =>
        Outer().Select(p => p+MutatingValue()).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> MutatingPartialInvariantNoOptimized() =>
        Outer().Select(p => p+MutatingValue()).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> EmptySourceMutatingPartialInvariantOptimized() =>
        EmptyOuter().Select(p => p+MutatingValue()).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> AnchorMutatingPartialInvariantOptimized() =>
        EmptyOuter().Select(p => p+MutatingValue().Anchor).ToList();

    private static int MutatingValue() {
        mutatingCallCount++;
        return 7;
    }
}
