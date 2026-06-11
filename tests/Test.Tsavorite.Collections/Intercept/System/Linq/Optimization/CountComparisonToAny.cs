using System.Linq;

using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Tsavorite.Collections.Intercept.System.Linq.Optimization;

// §1: Count()/LongCount() と 0/1 の比較を Any()/!Any() へ（全列挙を避け早期脱出）。
// expected=非 intercept の SystemEnumerable.Count(...)<比較>、actual=intercept される Source().Count()<比較>。
public sealed class CountComparisonToAny:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void GreaterThanZero() => GreaterThanZeroImpl();

    private static void GreaterThanZeroImpl(){
        {
            // 非空 -> true。
            var expected=SystemEnumerable.Count(Numbers())>0;
            var actual=Numbers().Count()>0;
            Assert.Equal(expected,actual);
        }
        {
            // 空 -> false。
            var expected=SystemEnumerable.Count(EmptyNumbers())>0;
            var actual=EmptyNumbers().Count()>0;
            Assert.Equal(expected,actual);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void EqualsZero() => EqualsZeroImpl();

    private static void EqualsZeroImpl(){
        {
            var expected=SystemEnumerable.Count(Numbers())==0;
            var actual=Numbers().Count()==0;
            Assert.Equal(expected,actual);
        }
        {
            var expected=SystemEnumerable.Count(EmptyNumbers())==0;
            var actual=EmptyNumbers().Count()==0;
            Assert.Equal(expected,actual);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void OtherForms() => OtherFormsImpl();

    private static void OtherFormsImpl(){
        {
            // >=1 -> Any。
            var expected=SystemEnumerable.Count(Numbers())>=1;
            var actual=Numbers().Count()>=1;
            Assert.Equal(expected,actual);
        }
        {
            // !=0 -> Any。
            var expected=SystemEnumerable.Count(Numbers())!=0;
            var actual=Numbers().Count()!=0;
            Assert.Equal(expected,actual);
        }
        {
            // <1 -> !Any。
            var expected=SystemEnumerable.Count(EmptyNumbers())<1;
            var actual=EmptyNumbers().Count()<1;
            Assert.Equal(expected,actual);
        }
        {
            // <=0 -> !Any。
            var expected=SystemEnumerable.Count(EmptyNumbers())<=0;
            var actual=EmptyNumbers().Count()<=0;
            Assert.Equal(expected,actual);
        }
        {
            // 逆順 0<Count() -> Any。
            var expected=0<SystemEnumerable.Count(Numbers());
            var actual=0<Numbers().Count();
            Assert.Equal(expected,actual);
        }
        {
            // 逆順 0==Count() -> !Any。
            var expected=0==SystemEnumerable.Count(EmptyNumbers());
            var actual=0==EmptyNumbers().Count();
            Assert.Equal(expected,actual);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void LongCountForm() => LongCountFormImpl();

    private static void LongCountFormImpl(){
        var expected=SystemEnumerable.LongCount(Numbers())>0;
        var actual=Numbers().LongCount()>0;
        Assert.Equal(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public static void WithPredicate() => WithPredicateImpl();

    private static void WithPredicateImpl(){
        {
            // Where(p).Count()>0 -> Where(p).Any() -> Any(p)。一致あり。
            var expected=SystemEnumerable.Count(SystemEnumerable.Where(Numbers(),static x => x>4))>0;
            var actual=Numbers().Where(static x => x>4).Count()>0;
            Assert.Equal(expected,actual);
        }
        {
            // 一致なし -> false。
            var expected=SystemEnumerable.Count(SystemEnumerable.Where(Numbers(),static x => x>100))>0;
            var actual=Numbers().Where(static x => x>100).Count()>0;
            Assert.Equal(expected,actual);
        }
        {
            // Count(pred)>0 -> Any(pred)。
            var expected=SystemEnumerable.Count(Numbers(),static x => x>4)>0;
            var actual=Numbers().Count(static x => x>4)>0;
            Assert.Equal(expected,actual);
        }
    }
}
