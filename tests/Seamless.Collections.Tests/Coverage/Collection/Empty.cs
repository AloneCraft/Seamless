namespace Seamless.Collections.Tests;


public class Empty {
    [Fact, Trait("Category","Coverage")]
    public void opEqual() {
        var a = Collections.Empty.Default;
        var b = default(Collections.Empty);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object?)b));
        Assert.True(a==b);
    }
    [Fact, Trait("Category","Coverage")]
    public void opInequal() {
        var a = Collections.Empty.Default;
        var b = default(Collections.Empty);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object?)b));
        Assert.False(a!=b);
        Assert.Equal(0,a.GetHashCode());
    }
    [Fact, Trait("Category","Coverage")]
    public void GetHashCode_() {
        var a = Collections.Empty.Default;
        Assert.Equal(0,a.GetHashCode());
    }
}
