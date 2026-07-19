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
    [ベンチマークUI自動化ファクト属性]
    public void 選択範囲内のCPUチェックを直接クリックしても選択を維持する() {
        using var セッション = new ベンチマークUIセッション();
        var チェック一覧 = セッション.表示中の自動化IDを待機(ベンチマークUI自動化ID.CPUセルチェック,2).Take(2).ToArray();
        var 変更前 = チェック一覧.Select(セッション.チェック済みか).ToArray();
        セッション.CPUセルをクリック(チェック一覧[0]);
        セッション.CPUセルをクリック(チェック一覧[1],Keys.Shift);
        セッション.チェックボックスをクリック(チェック一覧[0]);

        var 期待値か = 変更前.All(static 値 => 値) ? false : true;
        Assert.All(チェック一覧,チェック => Assert.Equal(期待値か,セッション.チェック済みか(チェック)));

        セッション.キーを送信(Keys.Space);
        Assert.Equal(変更前[0],セッション.チェック済みか(チェック一覧[0]));
        Assert.Equal(変更前[1],セッション.チェック済みか(チェック一覧[1]));
    }

    [ベンチマークUI自動化ファクト属性]
    public void コア行チェックを直接クリックできる() {
        using var セッション = new ベンチマークUIセッション();
        var チェック = セッション.表示中の自動化IDを待機(ベンチマークUI自動化ID.CPUコア行チェック,1).First();
        var 変更前 = セッション.チェック済みか(チェック);

        セッション.チェックボックスをクリック(チェック);

        Assert.NotEqual(変更前,セッション.チェック済みか(チェック));
        セッション.チェックボックスをクリック(チェック);
        Assert.Equal(変更前,セッション.チェック済みか(チェック));
    }
    [ベンチマークUI自動化ファクト属性]
    public void 選択範囲内のコア行チェックを直接クリックしても選択を維持する() {
        using var セッション = new ベンチマークUIセッション();
        var チェック一覧 = セッション.表示中の自動化IDを待機(ベンチマークUI自動化ID.CPUコア行チェック,2).Take(2).ToArray();
        var 変更前 = チェック一覧.Select(セッション.チェック済みか).ToArray();
        セッション.CPUセルをクリック(チェック一覧[0]);
        セッション.CPUセルをクリック(チェック一覧[1],Keys.Shift);
        セッション.チェックボックスをクリック(チェック一覧[0]);

        var 期待値か = 変更前.All(static 値 => 値) ? false : true;
        Assert.All(チェック一覧,チェック => Assert.Equal(期待値か,セッション.チェック済みか(チェック)));

        セッション.キーを送信(Keys.Space);
        Assert.Equal(変更前[0],セッション.チェック済みか(チェック一覧[0]));
        Assert.Equal(変更前[1],セッション.チェック済みか(チェック一覧[1]));
    }
}
