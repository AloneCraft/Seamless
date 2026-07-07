using UI;

namespace Test.Benchmark;

public sealed class BenchmarkRunDisplaySourceLogic{
    [Fact]
    public void ShouldUseCurrentRunMemory(){
        Assert.True(global::UI.BenchmarkRunDisplaySourceLogic.ShouldUseCurrentRunMemory(true));
        Assert.False(global::UI.BenchmarkRunDisplaySourceLogic.ShouldUseCurrentRunMemory(false));
    }

    [Fact]
    public void ShouldKeepCurrentRunMemoryDisplay(){
        Assert.True(global::UI.BenchmarkRunDisplaySourceLogic.ShouldKeepCurrentRunMemoryDisplay(true));
        Assert.False(global::UI.BenchmarkRunDisplaySourceLogic.ShouldKeepCurrentRunMemoryDisplay(false));
    }

    [Fact]
    public void ShouldLoadSelectedRunFromDatabase(){
        {
            var shouldLoad = global::UI.BenchmarkRunDisplaySourceLogic.ShouldLoadSelectedRunFromDatabase(
                false,
                true,
                false,
                false,
                false,
                false,
                false,
                true);

            Assert.True(shouldLoad);
        }

        {
            var shouldLoad = global::UI.BenchmarkRunDisplaySourceLogic.ShouldLoadSelectedRunFromDatabase(
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                true);

            Assert.True(shouldLoad);
        }

        {
            var shouldLoad = global::UI.BenchmarkRunDisplaySourceLogic.ShouldLoadSelectedRunFromDatabase(
                false,
                false,
                false,
                false,
                false,
                true,
                false,
                false);

            Assert.False(shouldLoad);
        }

        {
            var shouldLoad = global::UI.BenchmarkRunDisplaySourceLogic.ShouldLoadSelectedRunFromDatabase(
                false,
                true,
                true,
                false,
                false,
                false,
                false,
                true);

            Assert.False(shouldLoad);
        }

        {
            var shouldLoad = global::UI.BenchmarkRunDisplaySourceLogic.ShouldLoadSelectedRunFromDatabase(
                false,
                true,
                false,
                false,
                false,
                false,
                true,
                true);

            Assert.False(shouldLoad);
        }
    }
}
