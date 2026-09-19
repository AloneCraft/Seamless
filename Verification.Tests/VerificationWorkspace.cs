using System.Diagnostics;
using Verification.Core;
using Workspace=Verification.Infrastructure.VerificationWorkspace;

namespace Verification.Tests;

public sealed class VerificationWorkspace {
    [Fact]
    public void ExplicitInitialization_PlanAndInterruptedResumeUseOneFacade() {
        using var repository=new GitRepository();
        Assert.False(Workspace.IsInitialized(repository.Root));
        Assert.Throws<FileNotFoundException>(()=>new Workspace(repository.Root));

        Workspace.Initialize(repository.Root);
        var workspace=new Workspace(repository.Root);
        var plan=workspace.CreatePlan(new(
            Verification.Infrastructure.VerificationSuite.Benchmark,
            "quick",
            "System.Array",
            "Int32",
            "Where_predicate"));
        var item=Assert.Single(plan.Cases);
        Assert.Equal("headless-benchmark",item.Adapter);
        Assert.Equal("system-linq-dotnet-equality",item.Conditions.Semantics);

        var storage=new Verification.Infrastructure.VerificationRepository(workspace.DatabasePath);
        var run=new VerificationRun("interrupted",null,DateTimeOffset.UtcNow,CaseStatus.Running,plan);
        storage.CreateRun(run);
        storage.SaveResult(run.Id,new(item.Id,CaseStatus.Running,item.Conditions,[],null,null,null));

        var recovered=new Workspace(repository.Root);
        Assert.Equal(1,recovered.RecoveredInterruptedRuns);
        Assert.Equal(CaseStatus.Interrupted,recovered.GetRun(run.Id).Status);
        Assert.Equal(CaseStatus.Interrupted,Assert.Single(recovered.GetResults(run.Id)).Status);
        Assert.Equal(item.Id,Assert.Single(recovered.CreateResumePlan(run.Id).Cases).Id);
    }

    private sealed class GitRepository:IDisposable {
        public string Root { get; }=Path.Combine(Path.GetTempPath(),"seamless-workspace-tests",Guid.NewGuid().ToString("N"));

        public GitRepository() {
            Directory.CreateDirectory(this.Root);
            File.WriteAllText(Path.Combine(this.Root,"Seamless.slnx"),"<Solution />");
            File.WriteAllText(Path.Combine(this.Root,"README.md"),"verification workspace");
            this.Git("init");
            this.Git("config","user.email","verification@example.invalid");
            this.Git("config","user.name","Verification Tests");
            this.Git("add",".");
            this.Git("commit","-m","fixture");
        }

        private void Git(params string[] arguments) {
            var start=new ProcessStartInfo("git") {
                WorkingDirectory=this.Root,
                UseShellExecute=false,
                RedirectStandardOutput=true,
                RedirectStandardError=true,
                CreateNoWindow=true,
            };
            foreach(var argument in arguments){start.ArgumentList.Add(argument);}
            using var process=Process.Start(start)??throw new InvalidOperationException("git could not start.");
            var output=process.StandardOutput.ReadToEnd();
            var error=process.StandardError.ReadToEnd();
            process.WaitForExit();
            if(process.ExitCode!=0){throw new InvalidOperationException(output+Environment.NewLine+error);}
        }

        public void Dispose() {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach(var file in Directory.EnumerateFiles(this.Root,"*",SearchOption.AllDirectories)) {
                File.SetAttributes(file,FileAttributes.Normal);
            }
            Directory.Delete(this.Root,true);
        }
    }
}
