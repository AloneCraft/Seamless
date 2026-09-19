using Verification.Core;
using Repository=Verification.Infrastructure.VerificationRepository;
namespace Verification.Tests;
public sealed class VerificationRepository{
    [Fact]public void Initialize(){
        using var workspace=new TestWorkspace();
        var path=Path.Combine(workspace.Root,"verification.db");
        Assert.Throws<FileNotFoundException>(()=>new Repository(path));
        Repository.Initialize(path);
        Assert.Empty(new Repository(path).GetRuns());
        Assert.Throws<IOException>(()=>Repository.Initialize(path));
        Assert.Null(new Repository(path).GetBaseline());
    }
    [Fact]public void SetBaseline(){
        using var workspace=new TestWorkspace();
        var repository=workspace.Repository;
        var plan=TestWorkspace.Plan(workspace.Root,1);
        repository.CreateRun(new("first",null,DateTimeOffset.UtcNow,CaseStatus.Running,plan));
        Assert.Throws<InvalidOperationException>(()=>repository.SetBaseline("first"));
        repository.SaveResult("first",TestWorkspace.Passed(plan.Cases[0]));
        repository.CompleteRun("first",CaseStatus.Passed);
        repository.SetBaseline("first");
        repository.CreateRun(new("second",null,DateTimeOffset.UtcNow,CaseStatus.Running,plan));
        repository.SaveResult("second",TestWorkspace.Passed(plan.Cases[0]));
        repository.CompleteRun("second",CaseStatus.Passed);
        Assert.Equal("first",new Repository(workspace.Database).GetBaseline());
        repository.SetBaseline("second");
        Assert.Equal("second",new Repository(workspace.Database).GetBaseline());
        Assert.Equal(2,repository.GetRuns().Count);
    }
    [Fact]public void RecoverInterruptedRuns_DoesNotTouchLiveLeaseAndMarksOrphan(){
        using var workspace=new TestWorkspace();
        var repository=workspace.Repository;
        var plan=TestWorkspace.Plan(workspace.Root,2);
        repository.CreateRun(new("orphan",null,DateTimeOffset.UtcNow,CaseStatus.Running,plan));
        repository.SaveResult("orphan",new(plan.Cases[0].Id,CaseStatus.Running,plan.Cases[0].Conditions,[],null,null,null));
        repository.SaveResult("orphan",new(plan.Cases[1].Id,CaseStatus.NotRun,plan.Cases[1].Conditions,[],null,null,"未実行"));
        var artifacts=Path.Combine(workspace.Root,"artifacts");
        var directory=Path.Combine(artifacts,"orphan");
        Directory.CreateDirectory(directory);
        using(var live=new FileStream(Path.Combine(directory,"run.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
            Assert.Equal(0,repository.RecoverInterruptedRuns(artifacts));
            Assert.Equal(CaseStatus.Running,repository.GetRun("orphan").Status);
        }

        Assert.Equal(1,repository.RecoverInterruptedRuns(artifacts));
        Assert.Equal(CaseStatus.Interrupted,repository.GetRun("orphan").Status);
        var results=repository.GetResults("orphan").ToDictionary(static result=>result.CaseId);
        Assert.Equal(CaseStatus.Interrupted,results[plan.Cases[0].Id].Status);
        Assert.Equal(CaseStatus.NotRun,results[plan.Cases[1].Id].Status);
        Assert.Contains("resume",results[plan.Cases[0].Id].Diagnostic,StringComparison.Ordinal);
    }
}
internal sealed class TestWorkspace:IDisposable{
    public string Root{get;}=Path.Combine(Path.GetTempPath(),"seamless-verification-tests",Guid.NewGuid().ToString("N"));
    public string Database=>Path.Combine(this.Root,"test.db");
    public Repository Repository{get;}
    public TestWorkspace(){
        Directory.CreateDirectory(this.Root);
        Repository.Initialize(this.Database);
        this.Repository=new(this.Database);
    }
    public static VerificationPlan Plan(string root,int count){
        var conditions=new ComparisonConditions("v1","input","dotnet","none","test-outcome-v1","host","cpu","Release");
        return new(1,root,"source","quick",0,Enumerable.Range(0,count).Select(i=>new VerificationCase("case"+i,"test","area","fake","test.csproj",[],[],conditions,"test")).ToArray());
    }
    public static CaseResult Passed(VerificationCase item)=>new(item.Id,CaseStatus.Passed,item.Conditions,[],null,"ok",null);
    public void Dispose(){
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(this.Root,true);
    }
}
