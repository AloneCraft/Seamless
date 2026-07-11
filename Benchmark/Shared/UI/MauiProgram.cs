using Syncfusion.Maui.Core.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;
using Microsoft.Maui.LifecycleEvents;
using Seamless.UI.Storage;
using UI.ViewModels;

namespace UI;

public static class MauiProgram {
    private const string SyncfusionLicenseKey = "Ngo9BigBOggjHTQxAR8/V1JHaF1cXmhOYVFzWmFZfVhgdl9HYVZRRmYuP1ZhSXxVdkNhXH9cc3VVR2VZUUV9XEE=";

    private static async Task HandleWindowClosingAsync(Microsoft.UI.Xaml.Window window,App app){
        try{
            if(await app.RequestCloseAsync()){
                window.DispatcherQueue.TryEnqueue(window.Close);
            }
        }catch(Exception ex){
            StartupDiagnostics.Write("Window closing flow failed.",ex);
        }
    }

    public static MauiApp CreateMauiApp() {
        StartupDiagnostics.InstallGlobalHandlers();
        StartupDiagnostics.Write("CreateMauiApp started.");
        StartupDiagnostics.WriteSyncfusionAssemblies("CreateMauiApp started");
        if(!string.IsNullOrWhiteSpace(SyncfusionLicenseKey)) {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(SyncfusionLicenseKey);
        }
        StartupDiagnostics.WriteSyncfusionAssemblies("Syncfusion license registered");

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureSyncfusionCore()
            .ConfigureSyncfusionToolkit()
            .ConfigureLifecycleEvents(events => {
#if WINDOWS
                events.AddWindows(windows => {
                    windows.OnWindowCreated(window => {
                        window.AppWindow.Closing+=(sender,args) => {
                            if(Application.Current is not App app){
                                return;
                            }

                            if(app.IsCloseConfirmed){
                                return;
                            }

                            args.Cancel=true;
                            _=HandleWindowClosingAsync(window,app);
                        };
                    });
                });
#endif
            });
        ConfigureServices(builder.Services);

        var app = builder.Build();
        StartupDiagnostics.WriteSyncfusionAssemblies("CreateMauiApp completed");
        StartupDiagnostics.Write("CreateMauiApp completed.");
        return app;
    }

    private static void ConfigureServices(IServiceCollection services) {
        services.AddSingleton<AppShell>();
        services.AddSingleton<MainPage>();
        services.AddSingleton<IBenchmarkCatalogService,BenchmarkCatalogService>();
        services.AddSingleton<ICpuTopologyService,CpuTopologyService>();
        services.AddSingleton<IBenchmarkResultService,BenchmarkResultService>();
        services.AddSingleton<IBenchmarkComparisonService,BenchmarkComparisonService>();
        services.AddSingleton<IBenchmarkQueueService,BenchmarkQueueService>();
        services.AddSingleton<IBenchmarkWorkerProcessService,BenchmarkWorkerProcessService>();
        services.AddSingleton<IBenchmarkRunService,BenchmarkRunService>();
        services.AddSingleton<IBenchmarkUiDispatcher,MauiBenchmarkUiDispatcher>();
        services.AddSingleton<IBenchmarkUiPreferences,MauiBenchmarkUiPreferences>();
        services.AddSingleton(static _ => new UiRunStore(
#if SCALABLE_BENCHMARK
            UiRunDatabasePathResolver.Resolve(UiRunAppKind.ScalableBenchmark,FindSolutionRoot()),
            UiRunAppKind.ScalableBenchmark));
#else
            UiRunDatabasePathResolver.Resolve(UiRunAppKind.Benchmark,FindSolutionRoot()),
            UiRunAppKind.Benchmark));
#endif
        services.AddSingleton(static provider => new RunnerViewModel(
            provider.GetRequiredService<IBenchmarkCatalogService>(),
            provider.GetRequiredService<ICpuTopologyService>(),
            provider.GetRequiredService<IBenchmarkRunService>(),
            provider.GetRequiredService<IBenchmarkComparisonService>(),
            provider.GetRequiredService<UiRunStore>(),
            CreateBenchmarkRoot(),
            provider.GetRequiredService<IBenchmarkUiDispatcher>(),
            provider.GetRequiredService<IBenchmarkUiPreferences>()));
    }

    private static string CreateBenchmarkRoot() =>
        Path.Combine(
            FindSolutionRoot()??Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Work",
            BenchmarkAppProfile.WorkRootName);

    private static string? FindSolutionRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null) {
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))) {
                return directory.FullName;
            }

            directory=directory.Parent;
        }

        return null;
    }

}
