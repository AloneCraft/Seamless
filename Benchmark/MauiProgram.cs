using Syncfusion.Maui.Toolkit.Hosting;

namespace Benchmark;

public static class MauiProgram {
    private const string SyncfusionLicenseKey = "";

    public static MauiApp CreateMauiApp() {
        if(!string.IsNullOrWhiteSpace(SyncfusionLicenseKey)) {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(SyncfusionLicenseKey);
        }

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureSyncfusionToolkit();

        return builder.Build();
    }
}
