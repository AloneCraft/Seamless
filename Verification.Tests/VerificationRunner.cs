using Verification.Core;
using Runner=Verification.Core.VerificationRunner;
namespace Verification.Tests;
public sealed class VerificationRunner{
    [Fact]public async Task RunAsync(){
        {
            using var workspace=new TestWorkspace();
            var plan=TestWorkspace.Plan(workspace.Root,3);
            var adapter=new Adapter();
            var runner=new Runner(workspace.Repository,[adapter]);
            var run=await runner.RunAsync(plan,Path.Combine(workspace.Root,"artifacts"));
            Assert.Equal(CaseStatus.Passed,run.Status);
            Assert.All(workspace.Repository.GetResults(run.Id),x=>Assert.Equal(CaseStatus.Passed,x.Status));
            Assert.Equal(["prepare:case0","prepare:case1","prepare:case2","execute:case0","execute:case1","execute:case2"],adapter.Events);
            Assert.Null(workspace.Repository.GetBaseline());
        }
        {
            using var workspace=new TestWorkspace();
            using var cancel=new CancellationTokenSource();
            var adapter=new Adapter{Execute=(item,token)=>{
                if(item.Id=="case1"){cancel.Cancel();token.ThrowIfCancellationRequested();}
                return TestWorkspace.Passed(item);
            }};
            var run=await new Runner(workspace.Repository,[adapter]).RunAsync(TestWorkspace.Plan(workspace.Root,3),Path.Combine(workspace.Root,"artifacts"),cancellationToken:cancel.Token);
            Assert.Equal(CaseStatus.Cancelled,run.Status);
            Assert.Equal([CaseStatus.Passed,CaseStatus.Cancelled,CaseStatus.NotRun],workspace.Repository.GetResults(run.Id).Select(x=>x.Status));
            Assert.Throws<InvalidOperationException>(()=>workspace.Repository.SetBaseline(run.Id));
        }
        {
            using var workspace=new TestWorkspace();
            var adapter=new Adapter{Execute=(item,_)=>TestWorkspace.Passed(item) with{Conditions=item.Conditions with{Semantics="different"}}};
            var run=await new Runner(workspace.Repository,[adapter]).RunAsync(TestWorkspace.Plan(workspace.Root,1),Path.Combine(workspace.Root,"artifacts"));
            Assert.Equal(CaseStatus.Failed,run.Status);
            Assert.Equal(CaseStatus.Failed,Assert.Single(workspace.Repository.GetResults(run.Id)).Status);
        }
    }
    private sealed class Adapter:IVerificationAdapter{
        public string Name=>"fake";
        public List<string> Events{get;}=[];
        public Func<VerificationCase,CancellationToken,CaseResult> Execute{get;init;}=(item,_)=>TestWorkspace.Passed(item);
        public Task PrepareAsync(VerificationCase item,string root,string artifacts,CancellationToken cancellationToken){
            this.Events.Add("prepare:"+item.Id);
            return Task.CompletedTask;
        }
        public Task<CaseResult> ExecuteAsync(VerificationCase item,string root,string artifacts,CancellationToken cancellationToken){
            this.Events.Add("execute:"+item.Id);
            return Task.FromResult(this.Execute(item,cancellationToken));
        }
    }
}
