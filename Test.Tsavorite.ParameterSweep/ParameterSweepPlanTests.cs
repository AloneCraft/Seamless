using Benchmark.Tsavorite.ParameterSweep;

namespace Test.Benchmark.Tsavorite.ParameterSweep;

public sealed class パラメータスイープ計画テスト {
    [Fact]
    public void 既定ケース作成_一要因スイープ順を使用する() {
        var オプション = new パラメータスイープ実行オプション {
            最大スイープ手順数=3,
            最大成功Count=16,
        };
        var ケース一覧 = パラメータスイープ計画.既定ケース一覧を作成(オプション)
            .Where(static 項目 => 項目.バックエンド==パラメータスイープバックエンド.メモリ
                &&項目.コレクション==パラメータスイープコレクション種別.ハッシュセット
                &&項目.ジェネリック名=="Int32"
                &&項目.スイープ軸==パラメータスイープ軸.Indexサイズ)
            .ToArray();

        Assert.Equal(3,ケース一覧.Length);
        Assert.Equal(64,ケース一覧[0].設定.Indexサイズ);
        Assert.Equal(128,ケース一覧[1].設定.Indexサイズ);
        Assert.Equal(256,ケース一覧[2].設定.Indexサイズ);
        Assert.All(ケース一覧,項目 => Assert.Contains("ハッシュセット<Int32>.メモリ.Indexサイズ",項目.ケース名));
    }

    [Fact]
    public void 既定ケース作成_ページスイープの設定が有効() {
        var オプション = new パラメータスイープ実行オプション {
            最大スイープ手順数=4,
            最大成功Count=16,
        };
        var ケース一覧 = パラメータスイープ計画.既定ケース一覧を作成(オプション)
            .Where(static 項目 => 項目.スイープ軸==パラメータスイープ軸.Pageサイズ)
            .Take(4)
            .ToArray();

        Assert.All(ケース一覧,項目 => {
            Assert.True(項目.設定.Pageサイズ<=項目.設定.Segmentサイズ);
            Assert.True(項目.設定.Memoryサイズ>=2*項目.設定.Pageサイズ);
        });
    }

    [Fact]
    public void 既定ケース作成_無効なオプションで例外() {
        var オプション = new パラメータスイープ実行オプション {
            最大スイープ手順数=0,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => パラメータスイープ計画.既定ケース一覧を作成(オプション));
    }
}
