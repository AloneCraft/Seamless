using Scalable.Collection;

namespace Test.Scalable.Persisted.Collection;

public sealed class ScalableElementCountsTests {
    [Fact]
    public void All_StartsAt65536AndEndsAt8388608() {
        Assert.Equal([65536,131072,262144,524288,1048576,2097152,4194304,8388608],ScalableElementCounts.All);
    }

    [Fact]
    public void DefaultInitial_StartsAtBaseElementCount() {
        Assert.Equal([65536],ScalableElementCounts.DefaultInitial);
        Assert.Equal(65536,ScalableElementCounts.BaseElementCount);
    }
}
