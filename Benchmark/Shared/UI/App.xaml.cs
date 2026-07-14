namespace UI;

public partial class App:Application {
    private bool 終了確認状態か;
    private bool 終了処理中か;
    private readonly Page? 起動ページ;

    public App(アプリシェル アプリシェル) {
        try {
            起動診断.書き込む("Appの初期化を開始しました。");
            this.InitializeComponent();
            this.起動ページ=アプリシェル;
            起動診断.書き込む("Appの初期化が完了しました。");
        } catch(Exception 例外) {
            起動診断.書き込む("Appの初期化に失敗しました。",例外);
            this.起動ページ=起動診断.エラーページを作成("ベンチマークの起動に失敗しました",例外);
        }
    }

    protected override Window CreateWindow(IActivationState? 有効化状態) =>
        new(this.起動ページ??起動診断.エラーページを作成("ベンチマークの起動に失敗しました",new InvalidOperationException("起動ページが初期化されていません。")));

    public async Task<bool> 終了を要求Async() {
        if(this.終了確認状態か||this.終了処理中か) {
            return this.終了確認状態か;
        }

        var ページ = メインページを取得();
        if(ページ is null) {
            this.終了確認状態か=true;
            return true;
        }

        this.終了処理中か=true;
        try {
            if(await ページ.終了を要求Async()) {
                this.終了確認状態か=true;
                return true;
            }

            return false;
        } finally {
            this.終了処理中か=false;
        }
    }

    public bool 終了確認済みか => this.終了確認状態か;

    private static MainPage? メインページを取得() {
        if(Shell.Current?.CurrentPage is MainPage ページ) {
            return ページ;
        }

        return null;
    }
}
