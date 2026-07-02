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
            //現在Runのメモリ結果がある間は、保存キュー未flushでも暗黙DBロードしない
            var shouldLoad = global::UI.BenchmarkRunDisplaySourceLogic.ShouldLoadSelectedRunFromDatabase(
                false,
                true,
                false,
                false,
                false,
                false,
                false,
                true);

            Assert.False(shouldLoad);
        }

        {
            //現在Runがなく、保存済みRunが明示的に選択されている場合だけDBロードする
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
            //新規/未保存/外部Runの表示ではDBロードしない
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
    }
}
