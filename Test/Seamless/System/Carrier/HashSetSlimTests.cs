namespace Test.Seamless.System.Carrier;

public sealed class HashSetSlimTests {
    [Fact]
    public void AddContainsRemoveRespectComparerAndNull() {
        using var set = new global::Seamless.System.Carrier.HashSetSlim<string>(global::System.StringComparer.OrdinalIgnoreCase);

        Assert.True(set.Add("A"));
        Assert.False(set.Add("a"));
        Assert.True(set.Contains("a"));
        Assert.True(set.Remove("a"));
        Assert.False(set.Contains("A"));

        Assert.True(set.Add(null!));
        Assert.False(set.Add(null!));
        Assert.True(set.Contains(null!));
        Assert.True(set.Remove(null!));
        Assert.False(set.Contains(null!));
    }

    [Fact]
    public void GrowKeepsAddedValues() {
        using var set = new global::Seamless.System.Carrier.HashSetSlim<int>(1,null);

        for(var i = 0;i<256;i++) {
            Assert.True(set.Add(i));
        }

        for(var i = 0;i<256;i++) {
            Assert.True(set.Contains(i));
            Assert.False(set.Add(i));
        }
    }
}
