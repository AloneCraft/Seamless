using System.Security.Cryptography;
using System.Text;
using Seamless.Management;

namespace Management.Tests;

public sealed class GeneratedCodeContracts{
    [Fact]public void CyclesAndUnmappedImplicitOperationsAreValid(){
        var code=Example();
        GeneratedExecutionCodeValidator.Validate(code);
        GeneratedExecutionCodeValidator.Validate(code with{Blocks=[code.Blocks[0],code.Blocks[1] with{BranchValue=new("implicit","FlowCaptureReference",-1,0,"bool",null,true)}]});
        GeneratedExecutionCodeValidator.Validate(new("Unknown",null,null,null,[],[],[],["GeneratedCodeUnavailable"]));
    }
    [Fact]public void HashRangesTargetsAndRegionCyclesAreRejected(){
        var code=Example();
        Assert.Throws<InvalidDataException>(()=>GeneratedExecutionCodeValidator.Validate(code with{BodyText="changed"}));
        Assert.Throws<InvalidDataException>(()=>GeneratedExecutionCodeValidator.Validate(code with{Blocks=[code.Blocks[0],code.Blocks[1] with{Operations=[new("op","Return",int.MaxValue,2,null,null,false)]}]}));
        Assert.Throws<InvalidDataException>(()=>GeneratedExecutionCodeValidator.Validate(code with{Edges=[new(1,77,"Conditional","Regular",[],[],[])]}));
        Assert.Throws<InvalidDataException>(()=>GeneratedExecutionCodeValidator.Validate(code with{Regions=[new(0,"Root",0,1,1,[]),new(1,"LocalLifetime",0,1,0,[])]}));
        Assert.Throws<InvalidDataException>(()=>GeneratedExecutionCodeValidator.Validate(code with{Status="Available",OmittedOperationCount=1}));
        Assert.Throws<InvalidDataException>(()=>GeneratedExecutionCodeValidator.Validate(code with{Blocks=[code.Blocks[0],code.Blocks[1] with{Operations=[new("op","Implicit",-1,1,null,null,true)]}]}));
    }
    [Fact]public void SavedGeneratedCodeIsExplicitAndDefaultMaskRemovesTheEntireDocument(){
        var code=Example();
        var plan=new Seamless.Management.ExecutionPlanDocument(2,"Secret.Query","v",new('A',64),new('B',64),[],ManagementJson.Element(new{})){GeneratedCode=code};
        var target=new QueryPlanResult("db",1,new('C',64),new('D',64),false,new("query",[plan.MethodKey],[plan]));
        var masked=PlanSnapshot.Create(target,plan,display:new(){StructureLevel="Generated"});
        Assert.Null(masked.Data.Plan.GeneratedCode);
        Assert.DoesNotContain("secretValue",Encoding.UTF8.GetString(masked.ToJson()));
        var saved=PlanSnapshot.Create(target,plan,display:new(){StructureLevel="Generated"},includeCode:true);
        var read=PlanSnapshot.Read(saved.ToJson());
        Assert.Equal(code.BodyText,read.Data.Plan.GeneratedCode!.BodyText);
        Assert.Equal("Generated",read.Data.Display.StructureLevel);
        Assert.Null(read.WithoutCode().Data.Plan.GeneratedCode);
        Assert.Equal(code.BodyText,read.Data.Plan.GeneratedCode.BodyText);
    }
    private static GeneratedExecutionCode Example(){
        const string body="{ return secretValue; }";
        return new("Available","Interceptor.Query",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))),body,
            [new(0,"Entry",true,"None",[],null),new(1,"Block",true,"None",[new("op","Return",2,19,"int","secretValue",false)],null)],
            [new(0,1,"FallThrough","Regular",[],[],[]),new(1,1,"Conditional","Regular",[],[],[])],[new(0,"Root",0,1,null,[])],[]);
    }
}
