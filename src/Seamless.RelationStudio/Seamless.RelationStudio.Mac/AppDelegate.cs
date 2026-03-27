using Foundation;

namespace Seamless.RelationStudio.Mac;

[Register(nameof(AppDelegate))]
public class AppDelegate:MauiUIApplicationDelegate {
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
