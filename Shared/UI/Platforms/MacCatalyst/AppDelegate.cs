using Foundation;

namespace UI;

[Register("AppDelegate")]
public sealed class AppDelegate:MauiUIApplicationDelegate {
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
