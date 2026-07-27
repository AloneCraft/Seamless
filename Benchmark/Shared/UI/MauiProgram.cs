using Microsoft.Maui.LifecycleEvents;

using UI.Storage;

using Syncfusion.Maui.Core.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;

using UI.ViewModels;

namespace UI;

public static class MauiProgram {
    private const string Syncfusionライセンスキー = "Ngo9BigBOggjHTQxAR8/V1JHaF1cXmhOYVFzWmFZfVhgdl9HYVZRRmYuP1ZhSXxVdkNhXH9cc3VVR2VZUUV9XEE=";

    private static async Task ウィンドウ終了時Async(Microsoft.UI.Xaml.Window ウィンドウ,App アプリ) {
        try {
            if(await アプリ.終了を要求Async()) {
                ウィンドウ.DispatcherQueue.TryEnqueue(ウィンドウ.Close);
            }
        } catch(Exception 例外) {
            起動診断.書き込む("ウィンドウ終了処理に失敗しました。",例外);
        }
    }

    public static MauiApp CreateMauiApp() {
        起動診断.グローバルハンドラーを設定();
        起動診断.書き込む("CreateMauiAppを開始しました。");
        起動診断.Syncfusionアセンブリを書き込む("CreateMauiApp開始");
        if(!string.IsNullOrWhiteSpace(Syncfusionライセンスキー)) {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(Syncfusionライセンスキー);
        }
        起動診断.Syncfusionアセンブリを書き込む("Syncfusionライセンス登録完了");

        var ビルダー = MauiApp.CreateBuilder();
        ビルダー
            .UseMauiApp<App>()
            .ConfigureSyncfusionCore()
            .ConfigureSyncfusionToolkit()
            .ConfigureLifecycleEvents(イベント => {
#if WINDOWS
                イベント.AddWindows(Windowsイベント => {
                    Windowsイベント.OnWindowCreated(ウィンドウ => {
                        ウィンドウ.AppWindow.Closing+=(送信元,引数) => {
                            if(Application.Current is not App アプリ) {
                                return;
                            }

                            if(アプリ.終了確認済みか) {
                                return;
                            }

                            引数.Cancel=true;
                            _=ウィンドウ終了時Async(ウィンドウ,アプリ);
                        };
                    });
                });
#endif
            });
        サービスを構成(ビルダー.Services);

        var アプリ = ビルダー.Build();
        起動診断.Syncfusionアセンブリを書き込む("CreateMauiApp完了");
        起動診断.書き込む("CreateMauiAppが完了しました。");
        return アプリ;
    }

    private static void サービスを構成(IServiceCollection サービス) {
        サービス.AddSingleton<アプリシェル>();
        サービス.AddSingleton<MainPage>();
        サービス.AddSingleton<Iベンチマークカタログサービス,ベンチマークカタログサービス>();
        サービス.AddSingleton<ICPUトポロジーサービス,CPUトポロジーサービス>();
        サービス.AddSingleton<Iベンチマーク結果サービス,ベンチマーク結果サービス>();
        サービス.AddSingleton<Iベンチマーク比較サービス,ベンチマーク比較サービス>();
        サービス.AddSingleton<Iベンチマークワーカープロセスサービス,ベンチマークワーカープロセスサービス>();
        サービス.AddSingleton<Iベンチマーク実行サービス,ベンチマーク実行サービス>();
        サービス.AddSingleton<IベンチマークUIディスパッチャー,MauiベンチマークUIディスパッチャー>();
        サービス.AddSingleton<IベンチマークUI設定,MauiベンチマークUI設定>();
        サービス.AddSingleton(static _ => new UI実行ストア(
#if SCALABLE_BENCHMARK
            UI実行DBパスリゾルバー.解決(UI実行アプリ種別.スケーラブルベンチマーク,ソリューションルートを検索()),
            UI実行アプリ種別.スケーラブルベンチマーク));
#else
            UI実行DBパスリゾルバー.解決(UI実行アプリ種別.ベンチマーク,ソリューションルートを検索()),
            UI実行アプリ種別.ベンチマーク));
#endif
        サービス.AddSingleton(static 提供元 => new 実行ビューモデル(
            提供元.GetRequiredService<Iベンチマークカタログサービス>(),
            提供元.GetRequiredService<ICPUトポロジーサービス>(),
            提供元.GetRequiredService<Iベンチマーク実行サービス>(),
            提供元.GetRequiredService<Iベンチマーク比較サービス>(),
            提供元.GetRequiredService<UI実行ストア>(),
            ベンチマークルートを作成(),
            提供元.GetRequiredService<IベンチマークUIディスパッチャー>(),
            提供元.GetRequiredService<IベンチマークUI設定>()));
    }

    private static string ベンチマークルートを作成() =>
        Path.Combine(
            ソリューションルートを検索()??Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Work",
            ベンチマークアプリプロファイル.作業ルート名);

    private static string? ソリューションルートを検索() {
        var ディレクトリ = new DirectoryInfo(AppContext.BaseDirectory);
        while(ディレクトリ is not null) {
            if(File.Exists(Path.Combine(ディレクトリ.FullName,"Seamless.slnx"))) {
                return ディレクトリ.FullName;
            }

            ディレクトリ=ディレクトリ.Parent;
        }

        return null;
    }

}
