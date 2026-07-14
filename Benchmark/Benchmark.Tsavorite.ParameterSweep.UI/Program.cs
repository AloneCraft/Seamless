namespace Benchmark.Tsavorite.ParameterSweep.UI;

public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
        var ビルダー = MauiApp.CreateBuilder();
        ビルダー.UseMauiApp<App>();
        return ビルダー.Build();
    }
}
