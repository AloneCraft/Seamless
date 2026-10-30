namespace Benchmark.Dynamic.Infrastructure;

public static class DynamicDatabasePathResolver{
    public static string Resolve(string? solutionRoot=null){
        var root=solutionRoot;
        if(string.IsNullOrWhiteSpace(root)){
            root=FindSolutionRoot();
        }
        if(string.IsNullOrWhiteSpace(root)){
            root=Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Seamless");
        }
        return Path.Combine(
            Path.GetFullPath(root),
            "Work",
            "Benchmark",
            DynamicInfrastructureSchema.DatabaseFileName);
    }

    private static string? FindSolutionRoot(){
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null){
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))){
                return directory.FullName;
            }
            directory=directory.Parent;
        }
        return null;
    }
}
