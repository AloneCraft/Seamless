namespace Benchmark.Dynamic.Worker;

public static class Program{
    public static Task<int> Main(){
        var Host=new WorkerHost(Console.In,Console.Out,Console.Error);
        return Host.RunAsync();
    }
}
