namespace Test.Benchmark;

public sealed class ベンチマーク実行表示元ロジック {
    [Fact]
    public void 今回実行メモリを使うか() {
        Assert.True(global::UI.ベンチマーク実行表示元ロジック.今回実行メモリを使うか(true));
        Assert.False(global::UI.ベンチマーク実行表示元ロジック.今回実行メモリを使うか(false));
    }

    [Fact]
    public void 今回実行メモリ表示を維持するか() {
        Assert.True(global::UI.ベンチマーク実行表示元ロジック.今回実行メモリ表示を維持するか(true));
        Assert.False(global::UI.ベンチマーク実行表示元ロジック.今回実行メモリ表示を維持するか(false));
    }

    [Fact]
    public void 選択実行をDBから読み込むか() {
        {
            var 読み込むか = global::UI.ベンチマーク実行表示元ロジック.選択実行をDBから読み込むか(
                false,
                true,
                false,
                false,
                false,
                false,
                false,
                true);

            Assert.True(読み込むか);
        }

        {
            var 読み込むか = global::UI.ベンチマーク実行表示元ロジック.選択実行をDBから読み込むか(
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                true);

            Assert.True(読み込むか);
        }

        {
            var 読み込むか = global::UI.ベンチマーク実行表示元ロジック.選択実行をDBから読み込むか(
                false,
                false,
                false,
                false,
                false,
                true,
                false,
                false);

            Assert.False(読み込むか);
        }

        {
            var 読み込むか = global::UI.ベンチマーク実行表示元ロジック.選択実行をDBから読み込むか(
                false,
                true,
                true,
                false,
                false,
                false,
                false,
                true);

            Assert.False(読み込むか);
        }

        {
            var 読み込むか = global::UI.ベンチマーク実行表示元ロジック.選択実行をDBから読み込むか(
                false,
                true,
                false,
                false,
                false,
                false,
                true,
                true);

            Assert.False(読み込むか);
        }
    }
}
