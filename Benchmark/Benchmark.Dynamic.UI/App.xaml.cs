namespace Benchmark.Dynamic.UI;

public partial class App:Application {
    private readonly MainPage 起動ページ;

    public App(MainPage 起動ページ) {
        this.InitializeComponent();
        this.起動ページ=起動ページ;
    }

    protected override Window CreateWindow(IActivationState? 有効化状態) {
        var ウィンドウ=new Window(this.起動ページ) {
            Title="Seamless Dynamic Benchmark",
            MinimumWidth=1120,
            MinimumHeight=720,
        };
        // ライブ検証が動かしている Worker を、ウィンドウを閉じるときに止める。
        ウィンドウ.Destroying+=(_,_)=>this.起動ページ.ライブ検証を終了する();
        return ウィンドウ;
    }
}
