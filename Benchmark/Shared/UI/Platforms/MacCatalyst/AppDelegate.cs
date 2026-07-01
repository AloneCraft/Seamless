using Foundation;
using UI;

namespace UI.Platforms.MacCatalyst;

[Register("AppDelegate")]
public sealed class AppDelegate:MauiUIApplicationDelegate {
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}


