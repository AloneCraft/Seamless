namespace Sample.NetShop;

public partial class App:Application{
    private readonly MainPage 起動ページ;

    public App(MainPage 起動ページ){
        this.InitializeComponent();
        this.UserAppTheme=AppTheme.Light;
        this.起動ページ=起動ページ;
    }

    protected override Window CreateWindow(IActivationState? 有効化状態){
        var 窓=new Window(this.起動ページ){
            Title="小さなネット商店 — Seamless サンプル",
            MinimumWidth=1200,
            MinimumHeight=760,
        };
        // 閉じるときに再生を止め、商店DBを確実に閉じる。
        窓.Destroying+=(_,_)=>this.起動ページ.終了する();
        return 窓;
    }
}
