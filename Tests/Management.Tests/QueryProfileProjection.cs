using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Seamless.Management;
using Projection=Seamless.Management.QueryProfileProjection;
using PlanDocument=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class QueryProfileProjection{
    [Fact]public void ActualConsumerBoundaryMapsToItsPhysicalNodeWithoutLogicalPortAllocation(){
        var (_,data,profile)=Example();var projected=Projection.Apply(data,profile);var interval=Assert.Single(projected.Intervals);var quantity=Assert.Single(projected.NodeQuantities!);
        Assert.Equal(2_000_000,interval.StartTicks);Assert.Equal(8_000_000,interval.EndTicks);Assert.Equal("FusedPipelineInclusive",interval.Scope);Assert.Equal("ManagedThread",interval.LaneKind);
        Assert.Equal("private-node-secret",interval.Node!.NodeId);Assert.Equal(interval.InstanceId,quantity.InstanceId);Assert.Equal("0",quantity.InputRows.DecimalValue);Assert.Equal("ConsumerInput",quantity.InputRows.Scope);Assert.Equal("ScalarReturn",quantity.OutputRows.Scope);
        Assert.Equal("Observed",quantity.InputRows.Evidence);Assert.Equal("1",quantity.OutputRows.DecimalValue);Assert.Empty(projected.PortQuantities);Assert.Empty(projected.LinkQuantities);
        Assert.Equal(TimeSpan.TicksPerSecond,Assert.Single(projected.Clocks).TicksPerSecond);Assert.Null(projected.Clocks[0].StartedAtUtc);
    }
    [Theory][InlineData("Definition")][InlineData("Object")][InlineData("Version")][InlineData("Artifact")][InlineData("Execution")]
    public void AForeignTopLevelIdentityCannotContributeAnyRegion(string field){
        var (_,data,profile)=Example();profile=field switch{"Definition"=>profile with{DefinitionId="Other"},"Object"=>profile with{ObjectId="Other"},"Version"=>profile with{Version=2},"Artifact"=>profile with{ArtifactHash=new('D',64)},_=>profile with{ExecutionId=Guid.NewGuid()}};
        Assert.Throws<InvalidDataException>(()=>Projection.Apply(data,profile));
    }
    [Theory][InlineData("Method")][InlineData("Source")][InlineData("Body")][InlineData("Path")][InlineData("Start")][InlineData("Length")][InlineData("Caller")][InlineData("Node")]
    public void EveryCallSiteFieldAndNodeMustMatchBeforeARegionIsProjected(string field){
        var (_,data,profile)=Example();var target=profile.Regions[0].Target;
        target=field switch{"Method"=>target with{MethodKey="Other"},"Source"=>target with{SourceHash=new('D',64)},"Body"=>target with{GeneratedBodyHash=new('D',64)},"Path"=>target with{CallSite=target.CallSite with{Path="other.cs"}},"Start"=>target with{CallSite=target.CallSite with{Start=11}},"Length"=>target with{CallSite=target.CallSite with{Length=5}},"Caller"=>target with{CallSite=target.CallSite with{Caller="Other"}},_=>target with{NodeId="missing"}};
        var projected=Projection.Apply(data,profile with{Regions=[profile.Regions[0] with{Target=target}]});Assert.Empty(projected.Intervals);Assert.Empty(projected.NodeQuantities!);Assert.Contains("ProfileRegionsNotCollected",projected.Diagnostics);
    }
    [Fact]public void UnknownInputIsDifferentFromAnObservedZeroAndFailureRetainsItsActualPrefix(){
        var (_,data,profile)=Example();var region=profile.Regions[0] with{InputCount=123,InputCountKnown=false,OutputCount=0,Complete=false,Outcome="Failed"};
        var projected=Projection.Apply(data,profile with{Regions=[region],Complete=false});var quantity=Assert.Single(projected.NodeQuantities!);
        Assert.Null(quantity.InputRows.DecimalValue);Assert.Equal("Unknown",quantity.InputRows.Evidence);Assert.Equal("0",quantity.OutputRows.DecimalValue);Assert.Equal("Observed",quantity.OutputRows.Evidence);Assert.False(quantity.OutputRows.Complete);Assert.Equal("Failed",projected.Intervals[0].Outcome);
    }
    [Fact]public void MixedCallSitesAreSeparatedAndRepeatedProjectionDoesNotDuplicateEvidence(){
        var (_,data,profile)=Example();profile=profile with{Regions=[profile.Regions[0],profile.Regions[0] with{InstanceId=2,Target=profile.Regions[0].Target with{CallSite=profile.Regions[0].Target.CallSite with{Start=20}}}]};
        var projected=Projection.Apply(data,profile);Assert.Single(projected.Intervals);Assert.Contains("ProfileExcludedBinding=1",projected.Diagnostics);
        var repeated=Projection.Apply(projected,profile);Assert.Single(repeated.Intervals);Assert.Single(repeated.NodeQuantities!);
    }
    [Fact]public void MaskedFormatTwoRetainsNodeAndInstanceReferencesWithoutRawProfileTargets(){
        var (target,data,profile)=Example();data=Projection.Apply(data,profile);
        var saved=PlanSnapshot.CreateVisualization(target,data.Plan,data);var text=Encoding.UTF8.GetString(saved.ToJson());Assert.DoesNotContain("private",text,StringComparison.OrdinalIgnoreCase);
        var restored=PlanSnapshot.Read(saved.ToJson()).Visualization()!;var quantity=Assert.Single(restored.NodeQuantities!);var interval=Assert.Single(restored.Intervals);
        Assert.Equal(quantity.Node,interval.Node);Assert.Equal(quantity.InstanceId,interval.InstanceId);Assert.Equal(restored.Identity.PlanKey,quantity.Node.PlanKey);Assert.Equal("0",quantity.InputRows.DecimalValue);Assert.Contains("ProfileWorkerNotCollected",restored.Diagnostics);
    }
    [Fact]public void MissingOptionalNodeFieldsKeepThePreviousFormatTwoContentHash(){
        var (target,data,_)=Example();var snapshot=PlanSnapshot.CreateVisualization(target,data.Plan,data);var value=snapshot.Data;var extension=value.Extension!;
        var legacyExtension=new{extension.ExecutionId,extension.PortQuantities,extension.LinkQuantities,extension.Clocks,extension.Intervals,extension.Events,extension.ResourceHeat,extension.View};
        var legacy=new{value.DefinitionId,value.ObjectId,value.Version,value.ArtifactHash,value.SourceHash,value.FromDraft,value.DraftRevision,value.Plan,value.Statistics,value.Display,value.IncludesCode,Extension=legacyExtension};
        var bytes=JsonSerializer.SerializeToUtf8Bytes(legacy,ManagementJson.Options);Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)),snapshot.ContentHash);
        Assert.Null(PlanSnapshot.Read(snapshot.ToJson()).Visualization()!.NodeQuantities);
    }
    private static (QueryPlanResult Target,QueryVisualizationData Data,ObjectExecutionProfile Profile) Example(){
        var source=new ExecutionPlanNode("private-source-secret","SourceOperator","private-source-secret",[],null,"private/path.cs",10,4,[]){Layer="Original"};
        var node=new ExecutionPlanNode("private-node-secret","Fragment","PipelineTerminal",[source.Id],null,null,0,0,[]){Layer="Selected"};
        var plan=new PlanDocument(2,"private-method-secret","test",new('B',64),new('C',64),[source,node],JsonSerializer.SerializeToElement<object?>(null)){CallSite=new("private/path.cs",10,4,"private-caller-secret"),Edges=[new(source.Id,node.Id,"SelectedReference")]};
        var target=new QueryPlanResult("db",1,new('A',64),new('B',64),false,new("Query",[],[plan]));var execution=Guid.NewGuid();var data=QueryVisualizationData.FromPlan(target,plan,execution);
        var profileTarget=new ObjectExecutionProfileTarget(plan.MethodKey,plan.SourceHash,plan.GeneratedBodyHash,new(plan.CallSite.Path,plan.CallSite.Start,plan.CallSite.Length,plan.CallSite.Caller),node.Id);
        var region=new ObjectExecutionProfileRegion(1,profileTarget,1200,1800,42,0,1,true,true,true,"Succeeded","FusedPipelineInclusive");
        return(target,data,new(execution,"db","Query",1,new('A',64),1000,DateTimeOffset.UnixEpoch,1100,[region],0,true,[]){QueryStartedTimestamp=1000});
    }
}
