namespace Seamless.UI.Storage;

public static class UiRunDatabasePathResolver{
    public const string BenchmarkDatabaseFileName = "Benchmark.db";
    public const string ScalableDatabaseFileName = "Benchmark.Scalable.db";
    public const string ParameterSweepDatabaseFileName = "Benchmark.ParameterSweep.db";

    public static string Resolve(UiRunAppKind appKind,string? solutionRoot = null){
        var root = !string.IsNullOrWhiteSpace(solutionRoot)
            ? solutionRoot
            : FindSolutionRoot()??Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Seamless");
        var fileName=appKind switch{
            UiRunAppKind.Benchmark=>BenchmarkDatabaseFileName,
            UiRunAppKind.ScalableBenchmark=>ScalableDatabaseFileName,
            UiRunAppKind.ParameterSweep=>ParameterSweepDatabaseFileName,
            _=>throw new ArgumentOutOfRangeException(nameof(appKind),appKind,null),
        };
        return Path.Combine(root,"Work","UI",fileName);
    }

    private static string? FindSolutionRoot(){
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null){
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))){
                return directory.FullName;
            }

            directory=directory.Parent;
        }

        return null;
    }
}
