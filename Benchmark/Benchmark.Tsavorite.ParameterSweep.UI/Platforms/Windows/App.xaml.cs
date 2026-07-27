// App.xaml の x:Class と一致させる必要があるため、MAUI プラットフォーム規約の .WinUI を維持する。
namespace Benchmark.Tsavorite.ParameterSweep.UI.WinUI;

public partial class App:MauiWinUIApplication {
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
