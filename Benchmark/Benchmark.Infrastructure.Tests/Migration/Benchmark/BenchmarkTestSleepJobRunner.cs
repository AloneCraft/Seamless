namespace Test.Benchmark;

public sealed class ベンチマークテスト待機ジョブ実行器 {
    [Fact]
    public async Task 実行Async() {
        var 実行器 = new global::UI.ベンチマークテスト待機ジョブ実行器(TimeSpan.FromMilliseconds(1));
        var ジョブ = new global::UI.ベンチマークジョブ {
            プロジェクト="Test.プロジェクト",
            アセンブリ名="Test.Assembly",
            型完全名="Test.Type",
            メソッド名="Sleep",
            ソースタブ=global::UI.ベンチマークソースタブ.System配列,
            要素数=1,
            ループ回数=1,
            準備済み要素数一覧= [1],
        };
        var 実行ルート = Path.Combine(Path.GetTempPath(),"Seamless-TestSleepJobRunner-"+Guid.NewGuid().ToString("N"));

        var 結果 = await 実行器.実行Async(ジョブ,実行ルート,0,TimeSpan.FromSeconds(5),CancellationToken.None);

        Assert.Equal("Completed",結果.状態);
        Assert.Equal("Test.プロジェクト",結果.プロジェクト);
        Assert.Equal("Sleep",結果.メソッド);
        Assert.NotNull(結果.平均ナノ秒);
        Assert.Equal(0,結果.割り当てバイト数);
        Assert.Equal("test",結果.結果ダイジェスト);
    }

    [Fact]
    public async Task 仕様確認01() {
        var 実行器 = new global::UI.ベンチマークテスト待機ジョブ実行器(TimeSpan.FromMilliseconds(1));
        var ジョブ = new global::UI.ベンチマークジョブ {
            プロジェクト="System.IEnumerable.FLinq",
            アセンブリ名="Benchmark.System.Linq.Enumerable.IEnumerable.FLinq",
            型完全名="Benchmark.System.Linq.Enumerable.IEnumerable.FLinqInt32",
            メソッド名="Any",
            ソースタブ=global::UI.ベンチマークソースタブ.SystemIEnumerable,
            要素数=4_096,
            ループ回数=1,
            準備済み要素数一覧= [4_096],
        };
        var 実行ルート = Path.Combine(Path.GetTempPath(),"Seamless-IEnumerableSourceTab-"+Guid.NewGuid().ToString("N"));
        try {
            var 結果 = await 実行器.実行Async(ジョブ,実行ルート,0,TimeSpan.FromSeconds(5),CancellationToken.None);

            Assert.Equal("Completed",結果.状態);
            Assert.True(Directory.Exists(結果.成果物フォルダー));
            Assert.Contains("System.Collections.Generic.IEnumerable__",結果.成果物フォルダー,StringComparison.Ordinal);
        } finally {
            if(Directory.Exists(実行ルート)) {
                Directory.Delete(実行ルート,true);
            }
        }
    }

    [Fact]
    public async Task 仕様確認02() {
        var 実行器 = new global::UI.ベンチマークテスト待機ジョブ実行器(TimeSpan.FromSeconds(5));
        var ジョブ = new global::UI.ベンチマークジョブ {
            プロジェクト="Test.プロジェクト",
            アセンブリ名="Test.Assembly",
            型完全名="Test.Type",
            メソッド名="Sleep",
            ソースタブ=global::UI.ベンチマークソースタブ.System配列,
            要素数=1,
            ループ回数=1,
            準備済み要素数一覧= [1],
        };
        var 実行ルート = Path.Combine(Path.GetTempPath(),"Seamless-TestSleepJobRunner-"+Guid.NewGuid().ToString("N"));
        using var 取消 = new CancellationTokenSource();
        取消.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => 実行器.実行Async(ジョブ,実行ルート,0,TimeSpan.FromSeconds(5),取消.Token));
    }

    [Fact]
    public async Task 仕様確認03() {
        var 実行器 = new global::UI.ベンチマークテスト待機ジョブ実行器(TimeSpan.FromMilliseconds(1),true);
        var ジョブ = new global::UI.ベンチマークジョブ {
            プロジェクト="Test.プロジェクト",
            アセンブリ名="Test.Assembly",
            型完全名="Test.Type",
            メソッド名="Sleep",
            ソースタブ=global::UI.ベンチマークソースタブ.System配列,
            要素数=1,
            ループ回数=1,
            準備済み要素数一覧= [1],
        };
        var 実行ルート = Path.Combine(Path.GetTempPath(),"Seamless-TestSleepJobRunner-"+Guid.NewGuid().ToString("N"));
        using var 取消 = new CancellationTokenSource();

        var 実行タスク = 実行器.実行Async(ジョブ,実行ルート,0,TimeSpan.FromSeconds(5),取消.Token);
        var 先頭完了通知 = await Task.WhenAny(実行タスク,Task.Delay(50));
        Assert.NotSame(実行タスク,先頭完了通知);

        取消.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await 実行タスク);
    }
}
