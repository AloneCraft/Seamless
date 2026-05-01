using EqualityEmpty = Seamless.Collections.EqualityEmpty;
namespace Test.Seamless.Unit.Collections;


public class TestEqualityEmpty {
    [Fact, Trait("Category","Unit")]
    public void opEqual() {
        var a = EqualityEmpty.Default;
        var b = default(EqualityEmpty);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object?)b));
        Assert.True(a==b);
    }
    [Fact, Trait("Category","Unit")]
    public void opInequal() {
        var a = EqualityEmpty.Default;
        var b = default(EqualityEmpty);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object?)b));
        Assert.False(a!=b);
    }
}
