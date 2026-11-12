using Seamless.Management;
using Plan=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class PlanSnapshotCallSites{
    [Fact]public void CallSiteIsMaskedByDefaultAndRetainedOnlyWithExplicitCode(){
        var plan=new Plan(2,"Query.Compute()","test",new('A',64),new('B',64),[],ManagementJson.Element(new{})){
            CallSite=new("private-source/secret.cs",32,17,"PrivateNamespace.Query.Run()")
        };
        var target=new QueryPlanResult("definition",1,new('C',64),new('D',64),false,new("Query",[plan.MethodKey],[plan]));
        var masked=PlanSnapshot.Create(target,plan);Assert.Null(masked.Data.Plan.CallSite);Assert.Null(PlanSnapshot.Read(masked.ToJson()).Data.Plan.CallSite);
        var raw=PlanSnapshot.Create(target,plan,includeCode:true);Assert.Equal(plan.CallSite,PlanSnapshot.Read(raw.ToJson()).Data.Plan.CallSite);
        Assert.Null(raw.WithoutCode().Data.Plan.CallSite);
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Create(target,plan with{CallSite=plan.CallSite with{Caller="Other.Run()"}},includeCode:true));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void InvalidRawCallSiteCannotEnterSnapshots(bool invalidUnicode){
        var site=new ExecutionPlanCallSite("q.cs",1,1,"Query.Run()");if(invalidUnicode){site=site with{Path="broken\ud800"};}else{site=site with{Start=int.MaxValue};}
        var plan=new Plan(2,"Query.Compute()","test",new('A',64),new('B',64),[],ManagementJson.Element(new{})){CallSite=site};
        var target=new QueryPlanResult("definition",1,new('C',64),new('D',64),false,new("Query",[plan.MethodKey],[plan]));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Create(target,plan,includeCode:true));
    }
}
