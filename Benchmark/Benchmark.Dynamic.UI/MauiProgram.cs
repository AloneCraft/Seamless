using Syncfusion.Maui.Core.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;

namespace Benchmark.Dynamic.UI;

public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
        var ビルダー=MauiApp.CreateBuilder();
        ビルダー
            .UseMauiApp<App>()
            .ConfigureSyncfusionCore()
            .ConfigureSyncfusionToolkit();

        ビルダー.Services.AddSingleton<I動的実行調停サービス,動的実行調停サービス>();
        ビルダー.Services.AddSingleton<I動的UI設定,Maui動的UI設定>();
        ビルダー.Services.AddSingleton<動的ベンチマーク画面モデル>();
        ビルダー.Services.AddSingleton<MainPage>();

        return ビルダー.Build();
    }
}
