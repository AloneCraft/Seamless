using Benchmark.Tsavorite.ParameterSweep;

namespace Test.Benchmark.Tsavorite.ParameterSweep;

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

    [Fact]
    public async Task 実行Async_ストレージ失敗_結果として捕捉する() {
        var オプション = new パラメータスイープ実行オプション {
            最大スイープ手順数=1,
            最大成功Count=64,
            検索試行Count=16,
            ケース別タイムアウト=TimeSpan.FromSeconds(3),
            ストレージデバイス容量=8192,
        };
        var ケース一覧 = new[] {
            new パラメータスイープケース(
                "ストレージ.無効",
            パラメータスイープコレクション種別.辞書,
                "Int32/Int32",
            パラメータスイープバックエンド.ストレージ,
            パラメータスイープ軸.Segmentサイズ,
                new(64,4096,4096,8192,1.0)),
        };

        var 結果一覧 = await new パラメータスイープ実行器().実行Async(ケース一覧,オプション);

        var 結果 = Assert.Single(結果一覧);
        Assert.NotEqual(パラメータスイープ失敗種別.完了,結果.失敗種別);
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
