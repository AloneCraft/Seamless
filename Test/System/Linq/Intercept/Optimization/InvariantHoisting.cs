using System.Linq;

namespace Test.System.Linq.Intercept.Optimization;

public sealed class InvariantHoisting:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void GlobalInvariant() {
        AssertSequenceEqual(Outer().Select(static a => a+StaticCheck()).ToList(),GlobalInvariant_NonEmptyOptimized());
        AssertSequenceEqual(Outer().Select(static a => a+StaticCheck()).ToList(),GlobalInvariant_NonEmptyNoOptimized());
        AssertSequenceEqual(EmptyOuter().Select(static a => a+StaticCheck()).ToList(),GlobalInvariant_EmptyOptimized());
        AssertSequenceEqual(EmptyOuter().Select(static a => a+StaticCheck()).ToList(),GlobalInvariant_EmptyNoOptimized());
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static List<int> GlobalInvariant_NonEmptyOptimized() => Outer().Select(static a => a+StaticCheck()).ToList();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static List<int> GlobalInvariant_NonEmptyNoOptimized() => Outer().Select(static a => a+StaticCheck()).ToList();

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static List<int> GlobalInvariant_EmptyOptimized() => EmptyOuter().Select(static a => a+StaticCheck()).ToList();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static List<int> GlobalInvariant_EmptyNoOptimized() => EmptyOuter().Select(static a => a+StaticCheck()).ToList();

    [Fact,Trait("Category","Unit")]
    public static void IntermediateScopeInvariant() {
        AssertSequenceEqual(Outer().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList(),IntermediateScopeInvariant_NonEmptyOptimized());
        AssertSequenceEqual(Outer().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList(),IntermediateScopeInvariant_NonEmptyNoOptimized());
        AssertSequenceEqual(EmptyOuter().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList(),IntermediateScopeInvariant_EmptyOptimized());
        AssertSequenceEqual(EmptyOuter().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList(),IntermediateScopeInvariant_EmptyNoOptimized());
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static List<int> IntermediateScopeInvariant_NonEmptyOptimized() =>
        Outer().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static List<int> IntermediateScopeInvariant_NonEmptyNoOptimized() =>
        Outer().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList();

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static List<int> IntermediateScopeInvariant_EmptyOptimized() =>
        EmptyOuter().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static List<int> IntermediateScopeInvariant_EmptyNoOptimized() =>
        EmptyOuter().SelectMany(static a => Inner().Select(b => (a+StaticCheck())-(b*StackCheck()))).ToList();

    [Fact,Trait("Category","Unit")]
    public static void CapturedLocalEliminatedToStructField() {
        var threshold=3;
        AssertSequenceEqual(Outer().Where(v => v<threshold).ToList(),CapturedLocalEliminatedToStructFieldOptimized());
        AssertSequenceEqual(Outer().Where(v => v<threshold).ToList(),CapturedLocalEliminatedToStructFieldNoOptimized());
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static List<int> CapturedLocalEliminatedToStructFieldOptimized() {
        var threshold=3;
        return Outer().Where(v => v<threshold).ToList();
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static List<int> CapturedLocalEliminatedToStructFieldNoOptimized() {
        var threshold=3;
        return Outer().Where(v => v<threshold).ToList();
    }

    [Fact,Trait("Category","Unit")]
    public static void InvariantNotHoistedPastLazyGuard() {
        var divisor=0;
        AssertSequenceEqual(EmptyOuter().Where(v => v>100/divisor).ToList(),InvariantNotHoistedPastLazyGuardOptimized());
        AssertSequenceEqual(EmptyOuter().Where(v => v>100/divisor).ToList(),InvariantNotHoistedPastLazyGuardNoOptimized());
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static List<int> InvariantNotHoistedPastLazyGuardOptimized() {
        var divisor=0;
        return EmptyOuter().Where(v => v>100/divisor).ToList();
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static List<int> InvariantNotHoistedPastLazyGuardNoOptimized() {
        var divisor=0;
        return EmptyOuter().Where(v => v>100/divisor).ToList();
    }
}
