using Syncfusion.Maui.Core.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;
using Microsoft.Maui.LifecycleEvents;

namespace Benchmark;

public static class MauiProgram {
    private const string SyncfusionLicenseKey = "Ngo9BigBOggjHTQxAR8/V1JHaF1cXmhOYVFzWmFZfVhgdl9HYVZRRmYuP1ZhSXxVdkNhXH9cc3VVR2VZUUV9XEE=";

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
                        window.Closed+=async (_,args) => {
                            if(Application.Current is not App app){
                                return;
                            }

                            args.Handled=true;
                            if(await app.RequestCloseAsync()){
                                window.Close();
                            }
                        };
                    });
                });
#endif
            });

        var app = builder.Build();
        StartupDiagnostics.WriteSyncfusionAssemblies("CreateMauiApp completed");
        StartupDiagnostics.Write("CreateMauiApp completed.");
        return app;
    }
}
