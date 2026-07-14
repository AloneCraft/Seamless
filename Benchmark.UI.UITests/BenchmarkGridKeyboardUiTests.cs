using OpenQA.Selenium;

namespace Benchmark.UI.UITests;

public sealed class ベンチマークグリッドキーボードUIテスト {
    private const string 配列タブ = "Array";
    private const string 配列タブ自動化キー = "System配列";

    [ベンチマークUI自動化ファクト属性]
    public void 矢印とスペースで選択を移動して現在行を切り替える() {
        using var セッション = new ベンチマークUIセッション();
        セッション.名前を待機("Selected "+配列タブ);
        var チェック一覧 = セッション.表示中の自動化IDを待機(ベンチマークUI自動化ID.行チェック,2);
        var 変更前 = チェック一覧.Take(2).Select(セッション.チェック済みか).ToArray();
        try {
            セッション.通常セルをクリック(チェック一覧[0]);
            セッション.自動化IDへキーを送信(ベンチマークUI自動化ID.グリッド(配列タブ自動化キー),Keys.ArrowDown);
            セッション.自動化IDへキーを送信(ベンチマークUI自動化ID.グリッド(配列タブ自動化キー),Keys.Space);

            Assert.Equal(変更前[0],セッション.チェック済みか(チェック一覧[0]));
            Assert.NotEqual(変更前[1],セッション.チェック済みか(チェック一覧[1]));
        } finally {
            this.チェック状態を復元(セッション,チェック一覧,変更前);
        }
    }

    [ベンチマークUI自動化ファクト属性]
    public void シフトとコントロールで行選択を拡張してスペースで選択を切り替える() {
        using var セッション = new ベンチマークUIセッション();
        セッション.名前を待機("Selected "+配列タブ);
        var チェック一覧 = セッション.表示中の自動化IDを待機(ベンチマークUI自動化ID.行チェック,3);
        var 変更前 = チェック一覧.Take(3).Select(セッション.チェック済みか).ToArray();
        try {
            セッション.通常セルをクリック(チェック一覧[0]);
            セッション.通常セルをクリック(チェック一覧[1],Keys.Shift);
            セッション.通常セルをクリック(チェック一覧[2],Keys.Control);
            セッション.自動化IDへキーを送信(ベンチマークUI自動化ID.グリッド(配列タブ自動化キー),Keys.Space);

            var 期待値か = 変更前.All(static 値 => 値) ? false : true;
            Assert.All(チェック一覧.Take(3),チェック => Assert.Equal(期待値か,セッション.チェック済みか(チェック)));
        } finally {
            this.チェック状態を復元(セッション,チェック一覧,変更前);
        }
    }

    private void チェック状態を復元(ベンチマークUIセッション セッション,IReadOnlyList<IWebElement> チェック一覧,IReadOnlyList<bool> 状態一覧) {
        for(var 番号 = 0;番号<状態一覧.Count;番号++) {
            if(セッション.チェック済みか(チェック一覧[番号])!=状態一覧[番号]) {
                セッション.通常セルをクリック(チェック一覧[番号]);
                セッション.自動化IDへキーを送信(ベンチマークUI自動化ID.グリッド(配列タブ自動化キー),Keys.Space);
            }
        }
    }
}
