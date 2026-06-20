using Uno.UI.Hosting;

namespace Benchmark.Runner.Uno;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if(BenchmarkWorkerHost.IsWorkerMode(args)){
            Environment.ExitCode=BenchmarkWorkerHost.RunAsync(args).GetAwaiter().GetResult();
            return;
        }

        App.InitializeLogging();

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWin32()
            .Build();

        host.Run();
    }
}
