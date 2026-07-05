using Syncfusion.Maui.Core.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;
using Microsoft.Maui.LifecycleEvents;

namespace UI;

public static class MauiProgram {
    private const string SyncfusionLicenseKey = "Ngo9BigBOggjHTQxAR8/V1JHaF1cXmhOYVFzWmFZfVhgdl9HYVZRRmYuP1ZhSXxVdkNhXH9cc3VVR2VZUUV9XEE=";

#if WINDOWS
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
#endif

    private static void AttachTraceConsole(){
#if WINDOWS
        AllocConsole();
#endif
        System.Diagnostics.Trace.AutoFlush=true;
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
    }

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
        AttachTraceConsole();
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

        var app = builder.Build();
        StartupDiagnostics.WriteSyncfusionAssemblies("CreateMauiApp completed");
        StartupDiagnostics.Write("CreateMauiApp completed.");
        return app;
    }
}
