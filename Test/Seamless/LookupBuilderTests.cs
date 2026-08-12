using Seamless.Linq;

namespace Test.Seamless;

public sealed class LookupBuilderTests {
    [Fact]
    public void AddUnpooledPreservesGroupOrderAndValues() {
        var builder=new LookupBuilder<int,int>(8,null);
        builder.AddUnpooled(2,20);
        builder.AddUnpooled(1,10);
        builder.AddUnpooled(2,21);

        using var lookup=builder.BuildAndClear();

        Assert.Equal(2,lookup.Count);
        Assert.Equal(new[]{20,21},lookup[2].ToArray());
        Assert.Equal(new[]{10},lookup[1].ToArray());
        Assert.Equal(new[]{2,1},lookup.Select(static group => group.Key).ToArray());
    }

    [Fact]
    public void PooledBuilderReturnsAllValuesBeforeDispose() {
        var builder=new LookupBuilder<int,int>(8,null,true);
        builder.Add(7,70);
        builder.Add(7,71);
        builder.Add(7,72);

        using var lookup=builder.BuildPooledAndClear();

        Assert.Equal(new[]{70,71,72},lookup[7].ToArray());
        Assert.Single(lookup);
    }
}