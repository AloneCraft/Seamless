using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class EnumerableMethod2 {
    [Fact, Trait("Category","Unit")]
    public void Carrier1Test() {
        Carrier1TestImpl();
        AssertInterceptorGenerated();
    }

    private static void Carrier1TestImpl() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static value => value*10);
        var actual=Source().Select(static value => value+1).Select(static value => value*10);
        AssertSequenceEqual(expected,actual);
    }

    private void Carrier2TestImpl() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static value => value>20);
        var actual=Source().Select(static value => value*10).Where(static value => value>20);
        AssertSequenceEqual(expected,actual);
    }
    [Fact, Trait("Category","Unit")]
    public void Carrier2Test() {
        Carrier2TestImpl();
        AssertInterceptorGenerated();
    }

    [Fact, Trait("Category","Unit")]
    public void Select_Select_index() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Select(Source(),static value => value+1),static (value,index) => value+index);
        var actual=Source().Select(static value => value+1).Select(static (value,index) => value+index);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_Where_index() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Select(Source(),static value => value*10),static (value,index) => value>20&&index>0);
        var actual=Source().Select(static value => value*10).Where(static (value,index) => value>20&&index>0);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_index_Select() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Select(Source(),static (value,index) => value+index),static value => value*10);
        var actual=Source().Select(static (value,index) => value+index).Select(static value => value*10);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_index_Select_index() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Select(Source(),static (value,index) => value+index),static (value,index) => value+index);
        var actual=Source().Select(static (value,index) => value+index).Select(static (value,index) => value+index);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_index_Where() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Select(Source(),static (value,index) => value+index),static value => value>4);
        var actual=Source().Select(static (value,index) => value+index).Where(static value => value>4);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_index_Where_index() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Select(Source(),static (value,index) => value+index),static (value,index) => value>3&&index>1);
        var actual=Source().Select(static (value,index) => value+index).Where(static (value,index) => value>3&&index>1);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Select() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static value => value*10);
        var actual=Source().Where(static value => value>1).Select(static value => value*10);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Select_index() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value+index);
        var actual=Source().Where(static value => value>1).Select(static (value,index) => value+index);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Where() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static value => value<4);
        var actual=Source().Where(static value => value>1).Where(static value => value<4);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_Where_index() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(Source(),static value => value>1),static (value,index) => value<4&&index<2);
        var actual=Source().Where(static value => value>1).Where(static (value,index) => value<4&&index<2);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_index_Select() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Where(Source(),static (value,index) => value>index),static value => value*10);
        var actual=Source().Where(static (value,index) => value>index).Select(static value => value*10);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_index_Select_index() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Where(Source(),static (value,index) => value>index),static (value,index) => value+index);
        var actual=Source().Where(static (value,index) => value>index).Select(static (value,index) => value+index);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_index_Where() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(Source(),static (value,index) => value>index),static value => value<4);
        var actual=Source().Where(static (value,index) => value>index).Where(static value => value<4);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Where_index_Where_index() {
        var expected=SystemEnumerable.Where(SystemEnumerable.Where(Source(),static (value,index) => value>index),static (value,index) => value<4&&index<2);
        var actual=Source().Where(static (value,index) => value>index).Where(static (value,index) => value<4&&index<2);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Select_Take_Select() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Take(SystemEnumerable.Select(Source(),static value => value+1),2),static value => value*10);
        var actual=Source().Select(static value => value+1).Take(2).Select(static value => value*10);
        AssertSequenceEqual(expected,actual);
    }

    [Fact, Trait("Category","Unit")]
    public void Take_zero_Select_does_not_enumerate_source() {
        var expected=SystemEnumerable.Select(SystemEnumerable.Take(ThrowingSource(),0),static value => value*10);
        var actual=ThrowingSource().Take(0).Select(static value => value*10);
        AssertSequenceEqual(expected,actual);
    }

    private static void AssertSequenceEqual<T>(IEnumerable<T> expected,IEnumerable<T> actual) {
        Assert.Equal(expected,actual);
    }

    private static int[] Source() => [1,2,3,4];
    private static IEnumerable<int> ThrowingSource() => new ThrowingEnumerable();

    private static string AssertInterceptorGenerated([global::System.Runtime.CompilerServices.CallerMemberName] string methodName = "") {
        var generated = GeneratedInterceptorAssert.SourceFor<EnumerableMethod2>(methodName);
        Assert.Contains(methodName,generated);
        return generated;
    }

    private sealed class ThrowingEnumerable:IEnumerable<int> {
        public IEnumerator<int> GetEnumerator() => throw new InvalidOperationException("Take(0) must not enumerate the source.");
        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
