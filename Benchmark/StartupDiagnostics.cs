namespace Benchmark;

internal static class StartupDiagnostics{
    private static readonly object Gate = new();
    private static readonly string[] SyncfusionAssemblyNames = [
        "Syncfusion.Licensing",
        "Syncfusion.Maui.Core",
        "Syncfusion.Maui.DataGrid",
        "Syncfusion.Maui.Toolkit",
    ];

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Seamless",
        "Benchmark",
        "Logs",
        "startup.log");

    public static void InstallGlobalHandlers(){
        AppDomain.CurrentDomain.UnhandledException+=(_,args) => {
            Write("Unhandled exception",args.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException+=(_,args) => {
            Write("Unobserved task exception",args.Exception);
        };
    }

    public static void Write(string message,Exception? exception = null){
        try{
            lock(Gate){
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(
                    LogPath,
                    DateTimeOffset.Now.ToString("O",System.Globalization.CultureInfo.InvariantCulture)
                    +" "
                    +message
                    +Environment.NewLine
                    +(exception is null ? "" : exception+Environment.NewLine));
            }
        }catch{
        }
    }

    public static void WriteSyncfusionAssemblies(string context){
        try{
            foreach(var assemblyName in SyncfusionAssemblyNames){
                var assembly = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .FirstOrDefault(assembly => string.Equals(
                        assembly.GetName().Name,
                        assemblyName,
                        StringComparison.OrdinalIgnoreCase));
                if(assembly is null){
                    Write($"{context}: {assemblyName} is not loaded.");
                    continue;
                }

                var location = assembly.Location;
                var productVersion = "";
                var fileVersion = "";
                if(!string.IsNullOrWhiteSpace(location) && File.Exists(location)){
                    var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(location);
                    productVersion=info.ProductVersion ?? "";
                    fileVersion=info.FileVersion ?? "";
                }

                Write(
                    $"{context}: {assemblyName} assembly={assembly.GetName().Version} product={productVersion} file={fileVersion} location={location}");
            }
        }catch(Exception ex){
            Write($"{context}: failed to inspect Syncfusion assemblies.",ex);
        }
    }

    public static ContentPage CreateErrorPage(string title,Exception exception){
        WriteSyncfusionAssemblies("CreateErrorPage");
        return new ContentPage{
            Title=title,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Content=new ScrollView{
                Content=new VerticalStackLayout{
                    Padding=new Thickness(24),
                    Spacing=12,
                    Children = {
                        new Label{
                            Text=title,
                            FontSize=20,
                            FontAttributes=FontAttributes.Bold,
                            TextColor=Color.FromArgb("#991B1B"),
                        },
                        new Label{
                            Text="Startup failed. See log:",
                            FontSize=13,
                            TextColor=Color.FromArgb("#334155"),
                        },
                        new Label{
                            Text=LogPath,
                            FontSize=12,
                            TextColor=Color.FromArgb("#0F766E"),
                            LineBreakMode=LineBreakMode.WordWrap,
                        },
                        new Label{
                            Text=exception.ToString(),
                            FontSize=11,
                            TextColor=Color.FromArgb("#1E293B"),
                            LineBreakMode=LineBreakMode.WordWrap,
                        },
                    },
                },
            },
        };
    }
}
