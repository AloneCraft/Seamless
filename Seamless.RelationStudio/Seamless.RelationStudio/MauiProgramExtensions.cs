using Microsoft.Extensions.Logging;

using Syncfusion.Maui.Core.Hosting;

namespace Seamless.RelationStudio;

public static class MauiProgramExtensions {
    public static MauiAppBuilder UseSharedMauiApp(this MauiAppBuilder builder) {
        builder
            .UseMauiApp<App>()
            .ConfigureSyncfusionCore()
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf","OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf","OpenSansSemibold");
            });

#if DEBUG
		builder.Logging.AddDebug();
#endif

        return builder;
    }
}
