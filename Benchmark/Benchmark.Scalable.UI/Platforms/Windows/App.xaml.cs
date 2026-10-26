namespace Benchmark.Scalable.UI.WinUI;

public partial class App:MauiWinUIApplication {
    public App(){
        this.UnhandledException+=(_,例外)=>Console.Error.WriteLine($"{DateTimeOffset.UtcNow:O} {例外.Message}{Environment.NewLine}{例外.Exception}");
    }
    protected override MauiApp CreateMauiApp() {
        return MauiProgram.CreateMauiApp();
    }
}
