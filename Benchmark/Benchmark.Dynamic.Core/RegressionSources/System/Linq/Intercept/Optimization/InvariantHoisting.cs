using Seamless.Linq;

namespace Test.System.Linq.Intercept.Optimization;

public sealed class InvariantHoisting:OptimizationTestBase {
    private static int mutatingCallCount;

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
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

    [Fact, Trait("Category","Unit")]
    public static void CapturedLocalEliminatedToStructField() {
        var threshold = 3;
        AssertSequenceEqual(Outer().Where(v => v<threshold).ToList(),CapturedLocalEliminatedToStructFieldOptimized());
        AssertSequenceEqual(Outer().Where(v => v<threshold).ToList(),CapturedLocalEliminatedToStructFieldNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> CapturedLocalEliminatedToStructFieldOptimized() {
        var threshold = 3;
        return Outer().Where(v => v<threshold).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> CapturedLocalEliminatedToStructFieldNoOptimized() {
        var threshold = 3;
        return Outer().Where(v => v<threshold).ToList();
    }

    [Fact, Trait("Category","Unit")]
    public static void InvariantDoesNotEvaluateBeforeLazyGuard() {
        Assert.Empty(InvariantNotHoistedPastLazyGuardOptimized());
        Assert.Empty(InvariantNotHoistedPastLazyGuardNoOptimized());
        Assert.Throws<DivideByZeroException>(()=>DivisionInvariantOptimized([1],0));
        Assert.Equal([3],DivisionInvariantOptimized([1],5));
        Assert.Empty(NullMemberInvariantOptimized([],null));
        Assert.Throws<NullReferenceException>(()=>NullMemberInvariantOptimized([1],null));
        Assert.Equal([2],NullMemberInvariantOptimized([1],[1]));
        Assert.Empty(CheckedInvariantOptimized([],int.MaxValue));
        Assert.Throws<OverflowException>(()=>CheckedInvariantOptimized([1],int.MaxValue));
        Assert.Equal([3],CheckedInvariantOptimized([1],1));
        Assert.Empty(NarrowingInvariantOptimized([],int.MaxValue));
        Assert.Throws<OverflowException>(()=>NarrowingInvariantOptimized([1],int.MaxValue));
        Assert.Equal([2],NarrowingInvariantOptimized([1],1));
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> InvariantNotHoistedPastLazyGuardOptimized() {
        var divisor = 0;
        return EmptyOuter().Where(v => v>100/divisor).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> InvariantNotHoistedPastLazyGuardNoOptimized() {
        var divisor = 0;
        return EmptyOuter().Where(v => v>100/divisor).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] DivisionInvariantOptimized(int[] source,int divisor)=>source.Select(value=>value+10/divisor).ToArray();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] NullMemberInvariantOptimized(int[] source,int[]? values)=>source.Select(value=>value+values!.Length).ToArray();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] CheckedInvariantOptimized(int[] source,int offset)=>source.Select(value=>value+checked(offset+1)).ToArray();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] NarrowingInvariantOptimized(int[] source,int offset)=>source.Select(value=>value+checked((byte)offset)).ToArray();

    [Fact, Trait("Category","Unit")]
    public static void PartialExpressionHoistedForSelect() {
        AssertSequenceEqual(SelectPartialInvariantNoOptimized(),SelectPartialInvariantOptimized());
        var generated = GeneratedInterceptorAssert.SourceFor<InvariantHoisting>(nameof(SelectPartialInvariantOptimized));
        // 巻き上げた x*x は、ループの外で1回だけ計算して構造体のフィールドへ渡し、ループの中ではそのフィールドを足す(名前ではなく形で確かめる)。
        Assert.Matches(@",\s*unchecked\(x\*x\)\)",generated);
        Assert.Matches(@"\+this\.\w+;",generated);
        Assert.Equal([10,11],ValueParameterInvariantOptimized([1,2],3));
        Assert.Equal([unchecked(1+50000*50000)],UncheckedInvariantInsideCheckedContextOptimized([1],50000));
    }

    [Fact, Trait("Category","Unit")]
    public static void PartialExpressionHoistedAcrossDelegateKinds() {
        AssertSequenceEqual(WherePartialInvariantNoOptimized(),WherePartialInvariantOptimized());
        AssertSequenceEqual(GroupByPartialInvariantNoOptimized(),GroupByPartialInvariantOptimized());
        AssertSequenceEqual(JoinPartialInvariantNoOptimized(),JoinPartialInvariantOptimized());
    }

    [Fact, Trait("Category","Unit")]
    public static void PartialExpressionKeepsMethodAndMutableCaptureEvaluationAtLambdaUse() {
        mutatingCallCount=0;
        AssertSequenceEqual([8,9,10,11,12],MutatingPartialInvariantOptimized());
        Assert.Equal(Outer().Length,mutatingCallCount);

        mutatingCallCount=0;
        AssertSequenceEqual([8,9,10,11,12],MutatingPartialInvariantNoOptimized());
        Assert.Equal(Outer().Length,mutatingCallCount);
        AssertSequenceEqual(MutableCaptureNoOptimized(),MutableCaptureOptimized());
        AssertSequenceEqual([2,6,12,20,30],MutableCaptureOptimized());
        AssertSequenceEqual(MutableCaptureByRefNoOptimized(),MutableCaptureByRefOptimized());
        AssertSequenceEqual([2,6,12,20,30],MutableCaptureByRefOptimized());
    }

    [Fact, Trait("Category","Unit")]
    public static void PartialExpressionDoesNotEvaluateBeforeEmptySourceEnumeration() {
        mutatingCallCount=0;
        Assert.Empty(EmptySourceMutatingPartialInvariantOptimized());
        Assert.Equal(0,mutatingCallCount);
    }

    [Fact, Trait("Category","Unit")]
    public static void AnchorPreventsPartialExpressionHoist() {
        mutatingCallCount=0;
        Assert.Empty(AnchorMutatingPartialInvariantOptimized());
        Assert.Equal(0,mutatingCallCount);
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> SelectPartialInvariantOptimized() {
        var x = 3;
        return Outer().Select(p => p+x*x).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> SelectPartialInvariantNoOptimized() {
        var x = 3;
        return Outer().Select(p => p+x*x).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] ValueParameterInvariantOptimized(int[] source,int offset)=>source.Select(value=>value+offset*offset).ToArray();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] UncheckedInvariantInsideCheckedContextOptimized(int[] source,int factor){
        checked{
            return source.Select(value=>unchecked(value+factor*factor)).ToArray();
        }
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] MutableCaptureOptimized(){
        var offset=0;
        return Outer().Select(value=>value+(offset++*0)).Select(value=>value+offset*offset).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] MutableCaptureNoOptimized(){
        var offset=0;
        return Outer().Select(value=>value+(offset++*0)).Select(value=>value+offset*offset).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] MutableCaptureByRefOptimized(){
        var offset=0;
        return Outer().Select(value=>IncrementAndReturn(value,ref offset)).Select(value=>value+offset*offset).ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] MutableCaptureByRefNoOptimized(){
        var offset=0;
        return Outer().Select(value=>IncrementAndReturn(value,ref offset)).Select(value=>value+offset*offset).ToArray();
    }

    private static int IncrementAndReturn(int value,ref int offset){offset++;return value;}

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> WherePartialInvariantOptimized() {
        var threshold = 1;
        var offset = 2;
        return Outer().Where(p => p>threshold+offset).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> WherePartialInvariantNoOptimized() {
        var threshold = 1;
        var offset = 2;
        return Outer().Where(p => p>threshold+offset).ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> GroupByPartialInvariantOptimized() {
        var x = 2;
        return Outers()
            .GroupBy(p => p.Key,p => p.Id+x*x,(key,values) => key+values.Sum()+x*x)
            .OrderBy(value => value)
            .ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> GroupByPartialInvariantNoOptimized() {
        var x = 2;
        return Outers()
            .GroupBy(p => p.Key,p => p.Id+x*x,(key,values) => key+values.Sum()+x*x)
            .OrderBy(value => value)
            .ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> JoinPartialInvariantOptimized() {
        var x = 2;
        var y = 2;
        return Outers()
            .Join(Inners(),outer => outer.Key+x*x,inner => inner.Key+y*y,(outer,inner) => outer.Id+inner.Id+x*x)
            .OrderBy(value => value)
            .ToList();
    }

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> JoinPartialInvariantNoOptimized() {
        var x = 2;
        var y = 2;
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
