using UI;

namespace Test.Benchmark;

public sealed class ベンチマークソースタブ実行中状態 {
    [Fact]
    public void 仕様確認01() {
        var 状態 = new global::UI.ベンチマークソースタブ実行中状態();

        状態.適用(結果を作成(ベンチマークソースタブ.System配列,"System.Array","Where",4096,0,ジョブ状態.実行中));

        Assert.True(状態.実行中か(ベンチマークソースタブ.System配列));
        Assert.False(状態.実行中か(ベンチマークソースタブ.Systemリスト));
    }

    [Fact]
    public void 仕様確認02() {
        var 状態 = new global::UI.ベンチマークソースタブ実行中状態();
        状態.適用(結果を作成(ベンチマークソースタブ.System配列,"System.Array","Where",4096,0,ジョブ状態.実行中));
        状態.適用(結果を作成(ベンチマークソースタブ.System配列,"System.Array","Select",4096,1,ジョブ状態.実行中));

        状態.適用(結果を作成(ベンチマークソースタブ.System配列,"System.Array","Where",4096,0,ジョブ状態.完了));

        Assert.True(状態.実行中か(ベンチマークソースタブ.System配列));
    }

    [Fact]
    public void 仕様確認03() {
        var 状態 = new global::UI.ベンチマークソースタブ実行中状態();
        状態.適用(結果を作成(ベンチマークソースタブ.System配列,"System.Array","Where",4096,0,ジョブ状態.実行中));

        状態.適用(結果を作成(ベンチマークソースタブ.System配列,"System.Array","Where",4096,0,ジョブ状態.完了));

        Assert.False(状態.実行中か(ベンチマークソースタブ.System配列));
    }

    [Fact]
    public void 仕様確認04() {
        var 状態 = new global::UI.ベンチマークソースタブ実行中状態();
        状態.適用(結果を作成(ベンチマークソースタブ.System配列,"System.Array","Where",4096,0,ジョブ状態.実行中));

        状態.適用(結果を作成(ベンチマークソースタブ.System配列,"System.Array","Where",4096,1,ジョブ状態.完了));

        Assert.False(状態.実行中か(ベンチマークソースタブ.System配列));
    }

    [Fact]
    public void 仕様確認05() {
        var 状態 = new global::UI.ベンチマークソースタブ実行中状態();
        状態.適用(結果を作成(ベンチマークソースタブ.System配列,"System.Array","Where",4096,0,ジョブ状態.実行中));

        状態.適用(結果を作成(ベンチマークソースタブ.System配列,"System.Array","Where",4096,0,ジョブ状態.失敗));

        Assert.False(状態.実行中か(ベンチマークソースタブ.System配列));
    }

    [Fact]
    public void 仕様確認06() {
        var 状態 = new global::UI.ベンチマークソースタブ実行中状態();
        状態.適用(結果を作成(ベンチマークソースタブ.System配列,"System.Array","Where",4096,0,ジョブ状態.実行中));
        状態.適用(結果を作成(ベンチマークソースタブ.Systemリスト,"System.Collections.Generic.List","Where",4096,1,ジョブ状態.実行中));

        Assert.True(状態.クリア());

        Assert.False(状態.実行中か(ベンチマークソースタブ.System配列));
        Assert.False(状態.実行中か(ベンチマークソースタブ.Systemリスト));
    }

    private static ベンチマーク結果項目 結果を作成(
        ベンチマークソースタブ ソースタブ,
        string プロジェクト,
        string メソッド,
        int 要素数,
        int 論理CPU,
        ジョブ状態 状態文字列) {
        var 定義 = ベンチマークソースタブ一覧.取得(ソースタブ);
        var 解決済みプロジェクト = 定義.左プロジェクト;
        var 型接頭辞 = 定義.左型接頭辞;
        return new(new ベンチマークジョブ {
            ソースタブ=ソースタブ,
            プロジェクト=解決済みプロジェクト,
            アセンブリ名=定義.左アセンブリ名,
            型完全名=string.IsNullOrWhiteSpace(型接頭辞) ? "Sample.Type" : 型接頭辞+".Sample",
            メソッド名=メソッド,
            引数キー="Count=4096",
            要素数=要素数,
            ループ回数=1,
            割り当て論理CPU=論理CPU,
            状態=状態文字列,
            開始日時=DateTimeOffset.UtcNow,
            平均ナノ秒=状態文字列==ジョブ状態.完了 ? 100 : null,
        });
    }
}
