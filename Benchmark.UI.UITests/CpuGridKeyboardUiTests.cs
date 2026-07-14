using OpenQA.Selenium;

namespace Benchmark.UI.UITests;

public sealed class CPUグリッドキーボードUIテスト {
    [ベンチマークUI自動化ファクト属性]
    public void マウス入力後にシフト選択とスペースで選択CPUセルを切り替える() {
        using var セッション = new ベンチマークUIセッション();
        var チェック一覧 = セッション.表示中の自動化IDを待機(ベンチマークUI自動化ID.CPUセルチェック,2);
        var 対象 = チェック一覧.Take(2).ToArray();
        var 変更前 = 対象.Select(セッション.チェック済みか).ToArray();
        Assert.Equal(変更前[0],変更前[1]);

        セッション.CPUセルをクリック(対象[0]);
        セッション.CPUセルをクリック(対象[1],Keys.Shift);
        セッション.キーを送信(Keys.Space);

        var 期待値 = !変更前[0];
        Assert.All(対象,チェック => Assert.Equal(期待値,セッション.チェック済みか(チェック)));

        セッション.キーを送信(Keys.Space);
        Assert.All(対象,チェック => Assert.Equal(変更前[0],セッション.チェック済みか(チェック)));
    }
}
