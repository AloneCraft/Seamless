namespace Test.Benchmark;

public sealed class SharedProjectLayout{
    [Fact]
    public void SharedProjects_AreUnderTargetFoldersAndSolutionReferencesMovedPaths(){
        var root = FindRepositoryRoot();
        var slnx = File.ReadAllText(Path.Combine(root,"Seamless.slnx"));
        var paths = new[] {
            "Shared/UI.Core/UI.Core.shproj",
            "Shared/UI.Core/UI.Core.projitems",
            "Shared/Collections/Collections.shproj",
            "Shared/Collections/Collections.projitems",
            "Benchmark/Shared/UI/UI.shproj",
            "Benchmark/Shared/UI/UI.projitems",
        };

        foreach(var path in paths){
            Assert.True(File.Exists(Path.Combine(root,path.Replace('/',Path.DirectorySeparatorChar))),path);
            Assert.Contains(path,slnx,StringComparison.Ordinal);
        }

        Assert.Empty(Directory.GetFiles(Path.Combine(root,"Shared"),"*.shproj"));
        Assert.Empty(Directory.GetFiles(Path.Combine(root,"Shared"),"*.projitems"));
        Assert.Empty(Directory.GetFiles(Path.Combine(root,"Benchmark","Shared"),"*.shproj"));
        Assert.Empty(Directory.GetFiles(Path.Combine(root,"Benchmark","Shared"),"*.projitems"));
    }

    private static string FindRepositoryRoot(){
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))){
            directory=directory.Parent;
        }

        return directory?.FullName??throw new InvalidOperationException("Repository root was not found.");
    }
}
