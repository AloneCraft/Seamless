using Benchmark.Tsavorite.ParameterSweep;

namespace Test.Tsavorite.ParameterSweep;

public sealed class パラメータスイープ実行器テスト {
    [Fact]
    public async Task 実行Async_ハッシュセットInt32スモーク_完了して検索する() {
        var オプション = new パラメータスイープ実行オプション {
            最大スイープ手順数=1,
            最大成功Count=64,
            検索試行Count=16,
            ケース別タイムアウト=TimeSpan.FromSeconds(3),
        };
        var ケース一覧 = パラメータスイープ計画.既定ケース一覧を作成(オプション)
            .Where(static 項目 => 項目.バックエンド==パラメータスイープバックエンド.メモリ
                &&項目.コレクション==パラメータスイープコレクション種別.ハッシュセット
                &&項目.ジェネリック名=="Int32"
                &&項目.スイープ軸==パラメータスイープ軸.Memoryサイズ)
            .Take(1)
            .ToArray();

        var 結果一覧 = await new パラメータスイープ実行器().実行Async(ケース一覧,オプション);

        var 結果 = Assert.Single(結果一覧);
        Assert.Equal(64,結果.最大成功Count);
        Assert.Equal(パラメータスイープ失敗種別.完了,結果.失敗種別);
        Assert.NotNull(結果.検索平均ナノ秒);
        Assert.Equal(結果.ケース名,ケース一覧[0].ケース名);
    }

    [Fact]
    public async Task 実行Async_ハッシュセットInt32並行ワーカー_共有ストアとワーカーを記録する() {
        var オプション = new パラメータスイープ実行オプション {
            最大スイープ手順数=1,
            最大成功Count=128,
            検索試行Count=32,
            ケース別タイムアウト=TimeSpan.FromSeconds(3),
            論理CPU一覧= [0,1],
        };
        var ケース = パラメータスイープ計画.既定ケース一覧を作成(オプション)
            .Single(static 値 => 値.バックエンド==パラメータスイープバックエンド.メモリ
                &&値.コレクション==パラメータスイープコレクション種別.ハッシュセット
                &&値.ジェネリック名=="Int32"
                &&値.スイープ軸==パラメータスイープ軸.Memoryサイズ);

        var 結果 = await new パラメータスイープ実行器().ケースを実行Async(ケース,オプション);

        Assert.Equal(パラメータスイープ失敗種別.完了,結果.失敗種別);
        Assert.Equal(128,結果.最大成功Count);
        Assert.Equal(128,結果.エントリCount);
        Assert.Equal(2,結果.並行数);
        Assert.Equal(2,結果.ワーカー指標一覧.Count);
        Assert.Equal(128,結果.ワーカー指標一覧.Sum(static 指標 => 指標.追加操作Count));
        Assert.Equal(32,結果.ワーカー指標一覧.Sum(static 指標 => 指標.検索操作Count));
        Assert.All(結果.ワーカー指標一覧,static 指標 => Assert.Equal(パラメータスイープ失敗種別.完了,指標.失敗種別));
    }

    [Theory]
    [InlineData(パラメータスイープバックエンド.メモリ)]
    [InlineData(パラメータスイープバックエンド.ストレージ)]
    public async Task 実行Async_ログメモリが2ページ未満_構築失敗をメモリ上限超過として捕捉する(パラメータスイープバックエンド バックエンド) {
        var ディレクトリ = Directory.CreateTempSubdirectory("SeamlessParameterSweep-").FullName;
        try {
            var オプション = new パラメータスイープ実行オプション { 作業ディレクトリ=ディレクトリ };
            var ケース = ケースを作成(パラメータスイープコレクション種別.ハッシュセット,"Int32",バックエンド,4096);

            var 結果 = await new パラメータスイープ実行器().ケースを実行Async(ケース,オプション);

            Assert.Equal(パラメータスイープ失敗種別.メモリ上限超過,結果.失敗種別);
            Assert.Equal(0,結果.最大成功Count);
            Assert.Equal(0,結果.エントリCount);
            Assert.Null(結果.検索平均ナノ秒);
            Assert.Empty(結果.ワーカー指標一覧);
            Assert.Null(結果.索引バケット数);
            Assert.Null(結果.溢れバケット数);
            Assert.Null(結果.メモリログバイト数);
            Assert.Null(結果.ディスクログバイト数);
            Assert.Contains("ResourceLimitException",結果.失敗メッセージ);
            Assert.Empty(Directory.EnumerateFileSystemEntries(ディレクトリ));
        } finally {
            Directory.Delete(ディレクトリ,true);
        }
    }

    [Fact]
    public async Task 実行Async_ディスク予算1バイト_上限超過を記録して並行ワーカーと一時領域を片付ける() {
        var ディレクトリ = Directory.CreateTempSubdirectory("SeamlessParameterSweep-").FullName;
        try {
            var オプション = new パラメータスイープ実行オプション {
                最大成功Count=64,
                検索試行Count=16,
                ケース別タイムアウト=TimeSpan.FromSeconds(3),
                ストレージデバイス容量=1,
                作業ディレクトリ=ディレクトリ,
                論理CPU一覧= [0,1],
            };
            var ケース = ケースを作成(パラメータスイープコレクション種別.辞書,"Int32/Int32",パラメータスイープバックエンド.ストレージ);

            var 結果 = await new パラメータスイープ実行器().ケースを実行Async(ケース,オプション).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(パラメータスイープ失敗種別.ディスク上限超過,結果.失敗種別);
            Assert.Equal(0,結果.最大成功Count);
            Assert.Equal(0,結果.エントリCount);
            Assert.Null(結果.検索平均ナノ秒);
            Assert.Equal(2,結果.ワーカー指標一覧.Count);
            Assert.Equal(1,結果.索引バケット数);
            Assert.NotNull(結果.溢れバケット数);
            Assert.NotNull(結果.メモリログバイト数);
            Assert.NotNull(結果.ディスクログバイト数);
            Assert.Contains(結果.ワーカー指標一覧,static 指標 => 指標.失敗種別==パラメータスイープ失敗種別.ディスク上限超過);
            Assert.Empty(Directory.EnumerateFileSystemEntries(ディレクトリ));
        } finally {
            Directory.Delete(ディレクトリ,true);
        }
    }

    [Theory]
    [InlineData(パラメータスイープコレクション種別.ハッシュセット,"String",パラメータスイープバックエンド.メモリ)]
    [InlineData(パラメータスイープコレクション種別.ハッシュセット,"String",パラメータスイープバックエンド.ストレージ)]
    [InlineData(パラメータスイープコレクション種別.セット,"管理セットレコード",パラメータスイープバックエンド.メモリ)]
    [InlineData(パラメータスイープコレクション種別.セット,"管理セットレコード",パラメータスイープバックエンド.ストレージ)]
    [InlineData(パラメータスイープコレクション種別.辞書,"String/Int32",パラメータスイープバックエンド.メモリ)]
    [InlineData(パラメータスイープコレクション種別.辞書,"String/Int32",パラメータスイープバックエンド.ストレージ)]
    public async Task 実行Async_共通ストアの全コレクション_追加と検索と一時領域の片付けが成功する(
        パラメータスイープコレクション種別 コレクション,
        string ジェネリック名,
        パラメータスイープバックエンド バックエンド) {
        var ディレクトリ = Directory.CreateTempSubdirectory("SeamlessParameterSweep-").FullName;
        try {
            var オプション = new パラメータスイープ実行オプション {
                最大成功Count=32,
                検索試行Count=8,
                ケース別タイムアウト=TimeSpan.FromSeconds(3),
                作業ディレクトリ=ディレクトリ,
            };
            var ケース = ケースを作成(コレクション,ジェネリック名,バックエンド);

            var 結果 = await new パラメータスイープ実行器().ケースを実行Async(ケース,オプション);

            Assert.Equal(パラメータスイープ失敗種別.完了,結果.失敗種別);
            Assert.Equal(32,結果.最大成功Count);
            Assert.InRange(結果.エントリCount,1,32);
            Assert.Equal(1,結果.索引バケット数);
            Assert.NotNull(結果.溢れバケット数);
            Assert.True(結果.メモリログバイト数>0);
            Assert.NotNull(結果.ディスクログバイト数);
            if(バックエンド==パラメータスイープバックエンド.メモリ) {
                Assert.Equal(0,結果.ディスクログバイト数);
            }
            Assert.NotNull(結果.検索平均ナノ秒);
            var 指標 = Assert.Single(結果.ワーカー指標一覧);
            Assert.Equal(32,指標.追加操作Count);
            Assert.Equal(8,指標.検索操作Count);
            Assert.Empty(Directory.EnumerateFileSystemEntries(ディレクトリ));
        } finally {
            Directory.Delete(ディレクトリ,true);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task 実行Async_ディスク予算が非正数_既定上限を使う(long ストレージデバイス容量) {
        var オプション = new パラメータスイープ実行オプション {
            最大成功Count=16,
            検索試行Count=4,
            ケース別タイムアウト=TimeSpan.FromSeconds(3),
            ストレージデバイス容量=ストレージデバイス容量,
        };
        var ケース = ケースを作成(パラメータスイープコレクション種別.ハッシュセット,"Int32",パラメータスイープバックエンド.ストレージ);

        var 結果 = await new パラメータスイープ実行器().ケースを実行Async(ケース,オプション);

        Assert.Equal(パラメータスイープ失敗種別.完了,結果.失敗種別);
        Assert.Equal(16,結果.最大成功Count);
    }

    [Fact]
    public async Task ケースを実行Async_既キャンセル_並行ワーカーを終了して一時領域を片付ける() {
        var ディレクトリ = Directory.CreateTempSubdirectory("SeamlessParameterSweep-").FullName;
        try {
            var オプション = new パラメータスイープ実行オプション {
                最大成功Count=64,
                検索試行Count=16,
                作業ディレクトリ=ディレクトリ,
                論理CPU一覧= [0,1],
            };
            var ケース = ケースを作成(パラメータスイープコレクション種別.辞書,"Int32/Int32",パラメータスイープバックエンド.ストレージ);
            using var キャンセルソース = new CancellationTokenSource();
            キャンセルソース.Cancel();

            var 結果 = await new パラメータスイープ実行器().ケースを実行Async(ケース,オプション,キャンセルソース.Token).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(0,結果.最大成功Count);
            Assert.Equal(2,結果.ワーカー指標一覧.Count);
            Assert.All(結果.ワーカー指標一覧,static 指標 => Assert.Equal(0,指標.検索操作Count));
            Assert.Empty(Directory.EnumerateFileSystemEntries(ディレクトリ));
        } finally {
            Directory.Delete(ディレクトリ,true);
        }
    }

    [Fact]
    public async Task 実行Async_既キャンセル_ケースを開始せずに中止する() {
        var ケース = ケースを作成(パラメータスイープコレクション種別.ハッシュセット,"Int32",パラメータスイープバックエンド.メモリ);
        using var キャンセルソース = new CancellationTokenSource();
        キャンセルソース.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new パラメータスイープ実行器().実行Async([ケース],キャンセルトークン: キャンセルソース.Token));
    }

    [Fact]
    public void 比較_読込結果を使用する() {
        var 結果 = new パラメータスイープ結果(
            "run",
            DateTimeOffset.UtcNow,
            "HashSet<Int32>.Memory.IndexSize.I64_P4K_S4K_M8K_MF1",
            パラメータスイープコレクション種別.ハッシュセット,
            "Int32",
            パラメータスイープバックエンド.メモリ,
            パラメータスイープ軸.Indexサイズ,
            64,
            4096,
            4096,
            8192,
            1,
            10,
            10,
            パラメータスイープ失敗種別.完了,
            string.Empty,
            1.5,
            20);

        var 比較結果 = Assert.Single(パラメータスイープ比較器.比較する([結果],[結果]));

        Assert.Equal(0,比較結果.最大Count差分);
        Assert.Equal(0.5,比較結果.今回追加時間シェア);
        Assert.Equal(0.5,比較結果.前回追加時間シェア);
        Assert.False(比較結果.失敗種別変更か);
    }

    [Fact]
    public void 比較_両側ゼロ_ゼロの時間シェアを返す() {
        var 今回 = 比較用結果を作成(0,0);
        var 前回 = 比較用結果を作成(0,0);

        var 比較結果 = Assert.Single(パラメータスイープ比較器.比較する([今回],[前回]));

        Assert.Equal(0,比較結果.今回追加時間シェア);
        Assert.Equal(0,比較結果.前回追加時間シェア);
        Assert.Equal(0,比較結果.今回検索時間シェア);
        Assert.Equal(0,比較結果.前回検索時間シェア);
    }

    [Fact]
    public void 比較_片側ゼロ_ゼロ対一の時間シェアを返す() {
        var 今回 = 比較用結果を作成(0,0);
        var 前回 = 比較用結果を作成(2,4);

        var 比較結果 = Assert.Single(パラメータスイープ比較器.比較する([今回],[前回]));

        Assert.Equal(0,比較結果.今回追加時間シェア);
        Assert.Equal(1,比較結果.前回追加時間シェア);
        Assert.Equal(0,比較結果.今回検索時間シェア);
        Assert.Equal(1,比較結果.前回検索時間シェア);
    }

    [Fact]
    public void 比較_負数と非有限値_時間シェアを欠測にする() {
        var 今回 = 比較用結果を作成(-1,double.PositiveInfinity);
        var 前回 = 比較用結果を作成(1,1);

        var 比較結果 = Assert.Single(パラメータスイープ比較器.比較する([今回],[前回]));

        Assert.Null(比較結果.今回追加時間シェア);
        Assert.Null(比較結果.前回追加時間シェア);
        Assert.Null(比較結果.今回検索時間シェア);
        Assert.Null(比較結果.前回検索時間シェア);
    }

    private static パラメータスイープケース ケースを作成(
        パラメータスイープコレクション種別 コレクション,
        string ジェネリック名,
        パラメータスイープバックエンド バックエンド,
        long Memoryサイズ = 32768) {
        var 設定 = new パラメータスイープ設定(64,4096,4096,Memoryサイズ,1);
        var ケース名 = パラメータスイープフォーマッター.ケース名を作成(コレクション,ジェネリック名,バックエンド,パラメータスイープ軸.Memoryサイズ,設定);
        return new(ケース名,コレクション,ジェネリック名,バックエンド,パラメータスイープ軸.Memoryサイズ,設定);
    }

    private static パラメータスイープ結果 比較用結果を作成(double 追加経過ミリ秒,double? 検索平均ナノ秒) => new(
        "run",
        DateTimeOffset.UtcNow,
        "ハッシュセット<Int32>.メモリ.Indexサイズ",
        パラメータスイープコレクション種別.ハッシュセット,
        "Int32",
        パラメータスイープバックエンド.メモリ,
        パラメータスイープ軸.Indexサイズ,
        64,
        4096,
        4096,
        8192,
        1,
        10,
        10,
        パラメータスイープ失敗種別.完了,
        string.Empty,
        追加経過ミリ秒,
        検索平均ナノ秒);
}
