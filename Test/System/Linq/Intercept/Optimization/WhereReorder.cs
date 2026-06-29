namespace Test.System.Linq.Intercept.Optimization;

public sealed class WhereReorder:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void CostAndSelectivity() {
        AssertSequenceEqual(Numbers().Where(static x => x>3).Where(static x => x*x%2==0).ToList(),CostAndSelectivity_CostOptimized());
        AssertSequenceEqual(Numbers().Where(static x => x>3).Where(static x => x*x%2==0).ToList(),CostAndSelectivity_CostNoOptimized());
        AssertSequenceEqual(Numbers().Where(static x => x>0).Where(static x => x==3).ToList(),CostAndSelectivity_SelectivityOptimized());
        AssertSequenceEqual(Numbers().Where(static x => x>0).Where(static x => x==3).ToList(),CostAndSelectivity_SelectivityNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> CostAndSelectivity_CostOptimized() =>
        Numbers().Where(static x => x>3).Where(static x => x*x%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> CostAndSelectivity_CostNoOptimized() =>
        Numbers().Where(static x => x>3).Where(static x => x*x%2==0).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> CostAndSelectivity_SelectivityOptimized() =>
        Numbers().Where(static x => x>0).Where(static x => x==3).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> CostAndSelectivity_SelectivityNoOptimized() =>
        Numbers().Where(static x => x>0).Where(static x => x==3).ToList();

    [Fact,Trait("Category","Unit")]
    public static void StructMemberAccess() {
        AssertSequenceEqual(Points().Where(static p => p.X>0).Where(static p => p.Y==1).ToList(),StructMemberAccessOptimized());
        AssertSequenceEqual(Points().Where(static p => p.X>0).Where(static p => p.Y==1).ToList(),StructMemberAccessNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Point> StructMemberAccessOptimized() =>
        Points().Where(static p => p.X>0).Where(static p => p.Y==1).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<Point> StructMemberAccessNoOptimized() =>
        Points().Where(static p => p.X>0).Where(static p => p.Y==1).ToList();

    [Fact,Trait("Category","Unit")]
    public static void GuardSuppressed() {
        AssertSequenceEqual(WithNulls().Where(static s => s!=null).Where(static s => s!.Length>3).ToList(),GuardSuppressedOptimized());
        AssertSequenceEqual(WithNulls().Where(static s => s!=null).Where(static s => s!.Length>3).ToList(),GuardSuppressedNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string?> GuardSuppressedOptimized() =>
        WithNulls().Where(static s => s!=null).Where(static s => s!.Length>3).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string?> GuardSuppressedNoOptimized() =>
        WithNulls().Where(static s => s!=null).Where(static s => s!.Length>3).ToList();
}
