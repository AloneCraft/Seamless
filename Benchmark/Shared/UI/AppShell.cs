namespace UI;

public sealed class アプリシェル:Shell {
    public アプリシェル(MainPage ページ) {
        起動診断.書き込む("アプリシェルの初期化を開始しました。");
        this.Items.Add(new ShellContent {
            Title="Benchmark",
            Content=ページ,
        });
        起動診断.書き込む("アプリシェルの初期化が完了しました。");
    }
}
