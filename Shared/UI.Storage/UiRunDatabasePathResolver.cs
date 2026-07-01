namespace Seamless.UI.Storage;

public static class UiRunDatabasePathResolver{
    public const string DatabaseFileName = "runs.db";

    public static string Resolve(string? solutionRoot = null){
        var root = !string.IsNullOrWhiteSpace(solutionRoot)
            ? solutionRoot
            : FindSolutionRoot()??Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Seamless");
        return Path.Combine(root,"Work","UI",DatabaseFileName);
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
