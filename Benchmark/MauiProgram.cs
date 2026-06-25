using Syncfusion.Maui.Core.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;

namespace Benchmark;

public static class MauiProgram {
    private const string SyncfusionLicenseKey = "";

    public static MauiApp CreateMauiApp() {
        StartupDiagnostics.InstallGlobalHandlers();
        StartupDiagnostics.Write("CreateMauiApp started.");
        if(!string.IsNullOrWhiteSpace(SyncfusionLicenseKey)) {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(SyncfusionLicenseKey);
        }

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureSyncfusionCore()
            .ConfigureSyncfusionToolkit();

        var app = builder.Build();
        StartupDiagnostics.Write("CreateMauiApp completed.");
        return app;
    }
}
