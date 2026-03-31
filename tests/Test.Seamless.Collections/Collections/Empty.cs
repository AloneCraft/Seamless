namespace Test.Seamless.Collections;


public class Empty {
    [Fact, Trait("Category","Coverage")]
    public void opEqual() {
        var a = global::Seamless.Collections.Empty.Default;
        var b = default(global::Seamless.Collections.Empty);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object?)b));
        Assert.True(a==b);
    }
    [Fact, Trait("Category","Coverage")]
    public void opInequal() {
        var a = global::Seamless.Collections.Empty.Default;
        var b = default(global::Seamless.Collections.Empty);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object?)b));
        Assert.False(a!=b);
        Assert.Equal(0,a.GetHashCode());
    }
    [Fact, Trait("Category","Coverage")]
    public void GetHashCode_() {
        var a = global::Seamless.Collections.Empty.Default;
        Assert.Equal(0,a.GetHashCode());
    }
}
