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
        // Destroyingでは遅いため、Windowsの閉じる要求をdrain完了まで延期する。
#if WINDOWS
        var 終了調停=new Benchmark.Infrastructure.アプリ終了調停(this.起動ページ.終了Async);
        ウィンドウ.Created+=(_,_)=>{
            if(ウィンドウ.Handler?.PlatformView is Microsoft.UI.Xaml.Window 実体){
                実体.AppWindow.Closing+=async (_,要求)=>{
                    if(終了調停.終了可能か){return;}
                    要求.Cancel=true;
                    if(await 終了調停.終了を要求Async()){
                        実体.DispatcherQueue.TryEnqueue(()=>実体.Close());
                    }else if(終了調停.最終障害 is{} 障害){
                        this.起動ページ.終了障害を表示(障害.Message);
                    }
                };
            }
        };
#endif
        ウィンドウ.Destroying+=(_,_)=>this.起動ページ.ライブ検証を終了する();
        return ウィンドウ;
    }
}
