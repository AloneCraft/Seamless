using System.Linq;

namespace Test.System.Linq.Intercept.Optimization;

public sealed class CountComparisonToAny:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void GreaterThanZero() {
        Assert.Equal(Numbers().Count()>0,GreaterThanZero_NonEmptyOptimized());
        Assert.Equal(Numbers().Count()>0,GreaterThanZero_NonEmptyNoOptimized());
        Assert.Equal(EmptyNumbers().Count()>0,GreaterThanZero_EmptyOptimized());
        Assert.Equal(EmptyNumbers().Count()>0,GreaterThanZero_EmptyNoOptimized());
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool GreaterThanZero_NonEmptyOptimized() => Numbers().Count()>0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool GreaterThanZero_NonEmptyNoOptimized() => Numbers().Count()>0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool GreaterThanZero_EmptyOptimized() => EmptyNumbers().Count()>0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool GreaterThanZero_EmptyNoOptimized() => EmptyNumbers().Count()>0;

    [Fact,Trait("Category","Unit")]
    public static void EqualsZero() {
        Assert.Equal(Numbers().Count()==0,EqualsZero_NonEmptyOptimized());
        Assert.Equal(Numbers().Count()==0,EqualsZero_NonEmptyNoOptimized());
        Assert.Equal(EmptyNumbers().Count()==0,EqualsZero_EmptyOptimized());
        Assert.Equal(EmptyNumbers().Count()==0,EqualsZero_EmptyNoOptimized());
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool EqualsZero_NonEmptyOptimized() => Numbers().Count()==0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool EqualsZero_NonEmptyNoOptimized() => Numbers().Count()==0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool EqualsZero_EmptyOptimized() => EmptyNumbers().Count()==0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool EqualsZero_EmptyNoOptimized() => EmptyNumbers().Count()==0;

    [Fact,Trait("Category","Unit")]
    public static void OtherForms() {
        Assert.Equal(Numbers().Count()>=1,OtherForms_GreaterThanOrEqualOneOptimized());
        Assert.Equal(Numbers().Count()>=1,OtherForms_GreaterThanOrEqualOneNoOptimized());
        Assert.Equal(Numbers().Count()!=0,OtherForms_NotEqualZeroOptimized());
        Assert.Equal(Numbers().Count()!=0,OtherForms_NotEqualZeroNoOptimized());
        Assert.Equal(EmptyNumbers().Count()<1,OtherForms_LessThanOneOptimized());
        Assert.Equal(EmptyNumbers().Count()<1,OtherForms_LessThanOneNoOptimized());
        Assert.Equal(EmptyNumbers().Count()<=0,OtherForms_LessThanOrEqualZeroOptimized());
        Assert.Equal(EmptyNumbers().Count()<=0,OtherForms_LessThanOrEqualZeroNoOptimized());
        Assert.Equal(0<Numbers().Count(),OtherForms_ZeroLessThanCountOptimized());
        Assert.Equal(0<Numbers().Count(),OtherForms_ZeroLessThanCountNoOptimized());
        Assert.Equal(0==EmptyNumbers().Count(),OtherForms_ZeroEqualsCountOptimized());
        Assert.Equal(0==EmptyNumbers().Count(),OtherForms_ZeroEqualsCountNoOptimized());
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool OtherForms_GreaterThanOrEqualOneOptimized() => Numbers().Count()>=1;

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool OtherForms_GreaterThanOrEqualOneNoOptimized() => Numbers().Count()>=1;

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool OtherForms_NotEqualZeroOptimized() => Numbers().Count()!=0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool OtherForms_NotEqualZeroNoOptimized() => Numbers().Count()!=0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool OtherForms_LessThanOneOptimized() => EmptyNumbers().Count()<1;

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool OtherForms_LessThanOneNoOptimized() => EmptyNumbers().Count()<1;

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool OtherForms_LessThanOrEqualZeroOptimized() => EmptyNumbers().Count()<=0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool OtherForms_LessThanOrEqualZeroNoOptimized() => EmptyNumbers().Count()<=0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool OtherForms_ZeroLessThanCountOptimized() => 0<Numbers().Count();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool OtherForms_ZeroLessThanCountNoOptimized() => 0<Numbers().Count();

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool OtherForms_ZeroEqualsCountOptimized() => 0==EmptyNumbers().Count();

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool OtherForms_ZeroEqualsCountNoOptimized() => 0==EmptyNumbers().Count();

    [Fact,Trait("Category","Unit")]
    public static void LongCountForm() {
        Assert.Equal(Numbers().LongCount()>0,LongCountFormOptimized());
        Assert.Equal(Numbers().LongCount()>0,LongCountFormNoOptimized());
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool LongCountFormOptimized() => Numbers().LongCount()>0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool LongCountFormNoOptimized() => Numbers().LongCount()>0;

    [Fact,Trait("Category","Unit")]
    public static void WithPredicate() {
        Assert.Equal(Numbers().Where(static x => x>4).Count()>0,WithPredicate_WhereMatchOptimized());
        Assert.Equal(Numbers().Where(static x => x>4).Count()>0,WithPredicate_WhereMatchNoOptimized());
        Assert.Equal(Numbers().Where(static x => x>100).Count()>0,WithPredicate_WhereNoMatchOptimized());
        Assert.Equal(Numbers().Where(static x => x>100).Count()>0,WithPredicate_WhereNoMatchNoOptimized());
        Assert.Equal(Numbers().Count(static x => x>4)>0,WithPredicate_CountPredicateOptimized());
        Assert.Equal(Numbers().Count(static x => x>4)>0,WithPredicate_CountPredicateNoOptimized());
    }

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool WithPredicate_WhereMatchOptimized() => Numbers().Where(static x => x>4).Count()>0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool WithPredicate_WhereMatchNoOptimized() => Numbers().Where(static x => x>4).Count()>0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool WithPredicate_WhereNoMatchOptimized() => Numbers().Where(static x => x>100).Count()>0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool WithPredicate_WhereNoMatchNoOptimized() => Numbers().Where(static x => x>100).Count()>0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(true,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool WithPredicate_CountPredicateOptimized() => Numbers().Count(static x => x>4)>0;

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private static bool WithPredicate_CountPredicateNoOptimized() => Numbers().Count(static x => x>4)>0;
}
