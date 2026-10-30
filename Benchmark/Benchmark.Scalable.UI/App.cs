namespace Benchmark.Scalable.UI;
public sealed class App:Application{
    protected override Window CreateWindow(IActivationState? 状態){
        var 画面=new MainPage();
        var ウィンドウ=new Window(画面){Title="Seamless Scalable Benchmark"};
#if WINDOWS
        var 終了調停=new Benchmark.Infrastructure.アプリ終了調停(画面.終了Async);
        ウィンドウ.Created+=(_,_)=>{
            if(ウィンドウ.Handler?.PlatformView is Microsoft.UI.Xaml.Window 実体){
                実体.AppWindow.Closing+=async (_,要求)=>{
                    if(終了調停.終了可能か){return;}
                    要求.Cancel=true;
                    if(await 終了調停.終了を要求Async()){
                        実体.DispatcherQueue.TryEnqueue(()=>実体.Close());
                    }else if(終了調停.最終障害 is{} 障害){
                        画面.終了障害を表示(障害.Message);
                    }
                };
            }
        };
#endif
        ウィンドウ.Destroying+=(_,_)=>画面.終了を要求する();
        return ウィンドウ;
    }
}
