using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Seamless.Management;
using Seamless.Management.Components;
using PlanDocument=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class PlanSnapshots{
    private const string Secret="SECRET_PAYLOAD_712";
    [Fact]public void FailureOutcomeSurvivesSavingWithoutExposingAnArbitraryErrorCode(){
        var (target,plan,statistics)=Example();
        var failed=new QueryExecutionObservation(statistics.ExecutionId,statistics.DefinitionId,statistics.ObjectId,statistics.Version,statistics.ArtifactHash,true,
            statistics.StartedAtUtc,statistics.CompletedAtUtc,statistics.Elapsed,statistics.DatabaseGateWait,statistics.TimeToFirstOutput,statistics.ObservedOutputCount,statistics.ReturnedOutputCount,true,"Failed","Execution",Secret);
        var saved=PlanSnapshot.Create(target,plan,statistics,observation:failed);
        var restored=PlanSnapshot.Read(saved.ToJson());
        Assert.Equal("Failed",restored.Data.Observation!.Outcome);Assert.Null(restored.Data.Observation.ErrorCode);
        Assert.DoesNotContain(Secret,Encoding.UTF8.GetString(restored.ToJson()));
        Assert.Contains("Failed",計画保存出力.HTML(restored,value=>value));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Create(target,plan,statistics,observation:failed with{Version=2}));
    }
    [Fact]public void DefaultProjectionOmitsEveryFreeFormCodeLocationAndKeepsActualStatistics(){
        var (target,plan,statistics)=Example();
        var snapshot=PlanSnapshot.Create(target,plan,statistics);
        var json=Encoding.UTF8.GetString(snapshot.ToJson());
        Assert.DoesNotContain(Secret,json);Assert.DoesNotContain("private-source",json);Assert.False(snapshot.Data.IncludesCode);
        Assert.Equal(statistics,snapshot.Data.Statistics);Assert.Equal(target.DefinitionId,snapshot.Data.DefinitionId);Assert.Equal(target.Selection.ObjectId,snapshot.Data.ObjectId);
        Assert.All(snapshot.Data.Plan.Nodes,node=>{Assert.Null(node.SourcePath);Assert.Null(node.QueryId);Assert.Empty(node.Properties);Assert.Equal(node.Kind,node.Label);});
        Assert.All(snapshot.Data.Plan.Edges,edge=>Assert.Null(edge.InputRole));
        var restored=PlanSnapshot.Read(snapshot.ToJson());Assert.Equal(snapshot.ContentHash,restored.ContentHash);
        Assert.Equal(snapshot.Data.Plan.Nodes.Length,restored.Data.Plan.Nodes.Length);
        var trace=OptimizationTraceReader.Read(restored.Data.Plan.Trace);
        Assert.True(trace.Available);Assert.Equal("Pipeline.Take",trace.Events[0].RuleId);Assert.Equal("Unknown",trace.Events[1].RuleId);
        Assert.All(trace.Events,item=>{Assert.Null(item.Before);Assert.Null(item.After);});
    }
    [Fact]public void ExplicitCodeExportAndSubsequentMaskingAreSeparateOperations(){
        var (target,plan,statistics)=Example();
        var raw=PlanSnapshot.Create(target,plan,statistics,includeCode:true);
        Assert.True(raw.Data.IncludesCode);Assert.Contains(Secret,Encoding.UTF8.GetString(raw.ToJson()));
        var restored=PlanSnapshot.Read(raw.ToJson());Assert.Equal(Secret,OptimizationTraceReader.Read(restored.Data.Plan.Trace,true).Events[0].Before);
        var masked=restored.WithoutCode();Assert.False(masked.Data.IncludesCode);Assert.DoesNotContain(Secret,Encoding.UTF8.GetString(masked.ToJson()));
        Assert.Contains(Secret,Encoding.UTF8.GetString(raw.ToJson()));
    }
    [Fact]public void ImportChecksContentHashFormatTypesStructureAndBudgets(){
        var (target,plan,_)=Example();var snapshot=PlanSnapshot.Create(target,plan);
        var changed=JsonNode.Parse(snapshot.ToJson())!;changed["data"]!["version"]=2;
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Encoding.UTF8.GetBytes(changed.ToJsonString())));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(snapshot.Data,99)));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(snapshot.Data with{ArtifactHash="invalid"})));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(snapshot.Data with{Plan=plan with{SchemaVersion=99}})));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(snapshot.Data with{Plan=plan with{Edges=[new("missing",plan.Nodes[0].Id,"SelectedReference")]}})));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(snapshot.Data with{Plan=plan with{Nodes=[plan.Nodes[0],plan.Nodes[0]]}})));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Raw(snapshot.Data with{Plan=plan with{Nodes=[plan.Nodes[0] with{OutputType="int\ninvalid"}]}})));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(new byte[PlanSnapshot.MaximumBytes+1]));
        changed=JsonNode.Parse(snapshot.ToJson())!;changed["unrecognized"]=true;
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Encoding.UTF8.GetBytes(changed.ToJsonString())));
        var duplicated=Encoding.UTF8.GetString(snapshot.ToJson()).Replace("\"format\":1","\"format\":1,\"Format\":1",StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Read(Encoding.UTF8.GetBytes(duplicated)));
    }
    [Fact]public void MaskedImportRemovesUndeclaredSourceTextEvenWithAValidContentHash(){
        var (target,plan,_)=Example();var snapshot=PlanSnapshot.Create(target,plan);
        var forged=PlanSnapshot.Read(Raw(snapshot.Data with{Plan=plan}));
        Assert.False(forged.Data.IncludesCode);Assert.DoesNotContain(Secret,Encoding.UTF8.GetString(forged.ToJson()));
    }
    [Fact]public void StatisticsMustMatchThePublishedTargetAndHaveValidMeasurementRanges(){
        var (target,plan,statistics)=Example();
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Create(target,plan,statistics with{Version=2}));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Create(target,plan,statistics with{ObjectId="Other"}));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Create(target,plan,statistics with{DatabaseGateWait=TimeSpan.FromSeconds(1)}));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Create(target with{FromDraft=true,DraftRevision=1},plan,statistics));
        Assert.Throws<InvalidDataException>(()=>PlanSnapshot.Create(target,plan with{SourceHash=new('E',64)},statistics));
    }
    [Fact]public void StaticOutputsUseTheSameMaskedProjectionAndEncodeUntrustedCode(){
        var (target,plan,_)=Example();var snapshot=PlanSnapshot.Create(target,plan);
        var svg=計画保存出力.SVG(snapshot,value=>value);var html=計画保存出力.HTML(snapshot,value=>value);
        Assert.DoesNotContain(Secret,svg);Assert.DoesNotContain(Secret,html);
        var xml=XDocument.Parse(svg);XNamespace ns="http://www.w3.org/2000/svg";
        Assert.Equal(ns+"svg",xml.Root!.Name);Assert.DoesNotContain(xml.Descendants(),item=>item.Name.LocalName is "foreignObject" or "script");
        Assert.Contains(xml.Descendants(ns+"text"),item=>item.Value=="Result");
        Assert.Contains("Content-Security-Policy",html);Assert.DoesNotContain("<script",html,StringComparison.OrdinalIgnoreCase);
        var code=PlanSnapshot.Create(target,plan,includeCode:true);var raw=計画保存出力.HTML(code,value=>value);
        Assert.Contains(Secret,raw);Assert.DoesNotContain("<script>steal()</script>",raw);Assert.Contains("&lt;script&gt;steal()&lt;/script&gt;",raw);
    }
    [Fact]public void ComparisonReportsRecordedStructureWithoutMatchingIdsAcrossDifferentPlans(){
        var (target,plan,_)=Example();var original=PlanSnapshot.Create(target,plan);
        var same=PlanSnapshot.Compare(original,PlanSnapshot.Read(original.ToJson()));
        Assert.True(same.SameTarget);Assert.True(same.SamePlan);Assert.True(same.NodeCorrespondenceKnown);
        var changedPlan=plan with{GeneratedBodyHash=new('D',64),Nodes=[plan.Nodes[0]],Edges=[]};
        var changedTarget=target with{Version=2,ArtifactHash=new('E',64),Selection=target.Selection with{Plans=[changedPlan]}};
        var comparison=PlanSnapshot.Compare(original,PlanSnapshot.Create(changedTarget,changedPlan));
        Assert.True(comparison.SameTarget);Assert.False(comparison.SameArtifact);Assert.False(comparison.NodeCorrespondenceKnown);
        Assert.Contains(comparison.NodeKinds,item=>item.Left!=item.Right);Assert.Equal(1,comparison.LeftRewrites);
        Assert.False(PlanSnapshot.Compare(original,PlanSnapshot.Create(changedTarget with{Selection=changedTarget.Selection with{ObjectId="dbo1.Query"}},changedPlan)).SameTarget);
    }
    [Fact]public void TraceReaderDistinguishesUnknownInvalidAndTruncatedRecords(){
        Assert.False(OptimizationTraceReader.Read(ManagementJson.Element(new{})).Available);
        Assert.False(OptimizationTraceReader.Read(ManagementJson.Element(new{schemaVersion=1,events=new[]{new{sequence=true}},layerTerminations=Array.Empty<object>()})).Available);
        var trace=ManagementJson.Element(new{schemaVersion=1,events=Enumerable.Range(1,OptimizationTraceReader.MaximumEvents+1).Select(index=>new{sequence=index,ruleId="Pipeline.Take",layer=1}).ToArray(),layerTerminations=new[]{new{runId=1,layer=1,roundCount=2,termination=0}}});
        var result=OptimizationTraceReader.Read(trace);Assert.True(result.Available);Assert.False(result.Complete);Assert.Equal(256,result.Events.Length);Assert.Equal("FixedPoint",result.LayerTerminations[0].Termination);
        var (target,plan,_)=Example();var projected=PlanSnapshot.Create(target,plan with{Trace=trace});
        Assert.False(OptimizationTraceReader.Read(projected.Data.Plan.Trace).Complete);
    }
    private static byte[] Raw(PlanSnapshotData data,int format=1){
        var hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data,ManagementJson.Options)));
        return JsonSerializer.SerializeToUtf8Bytes(new PlanSnapshot(format,data,hash),ManagementJson.Options);
    }
    [Fact]public void AnonymousTypeDisplayMetadataRemainsSaveableWithoutBeingTreatedAsCSharpSyntax(){
        var (target,plan,_)=Example();var anonymous="global::System.Collections.Generic.IEnumerable<<anonymous type: int Id, string Title>>";
        var actual=plan with{Nodes=[plan.Nodes[0] with{OutputType=anonymous}],Edges=[]};
        var masked=PlanSnapshot.Create(target,actual);Assert.Null(masked.Data.Plan.Nodes[0].OutputType);
        var raw=PlanSnapshot.Create(target,actual,includeCode:true);Assert.Equal(anonymous,PlanSnapshot.Read(raw.ToJson()).Data.Plan.Nodes[0].OutputType);
    }
    [Fact]public void CodeMetadataDictionaryPreservesDistinctCaseSensitiveKeys(){
        var (target,plan,_)=Example();
        plan.Nodes[0].Properties.Add("GeneratorState.Value","upper");plan.Nodes[0].Properties.Add("GeneratorState.value","lower");
        var saved=PlanSnapshot.Create(target,plan,includeCode:true);
        var restored=PlanSnapshot.Read(saved.ToJson());
        Assert.Equal("upper",restored.Data.Plan.Nodes[0].Properties["GeneratorState.Value"]);
        Assert.Equal("lower",restored.Data.Plan.Nodes[0].Properties["GeneratorState.value"]);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task SaveViewDefaultsToMaskedTextAndRequiresPermissionForExplicitCode(bool authorized){
        var (target,plan,statistics)=Example();
        using var provider=new ServiceCollection().AddLogging().AddSingleton<IJSRuntime,StaticRenderScripts>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(provider,provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async()=>{
            TestSnapshot? view=null;
            var rendered=await renderer.RenderComponentAsync<TestSnapshot>(ParameterView.FromDictionary(new Dictionary<string,object?>{
                {"Capture",(Action<TestSnapshot>)(value=>view=value)},{"対象",target},{"計画",plan},{"実測",statistics},{"コード出力許可か",authorized}
            }));
            Assert.DoesNotContain(Secret,System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()));
            view!.IncludeCode();
            Assert.Equal(authorized,System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()).Contains(Secret,StringComparison.Ordinal));
        });
    }
    public sealed class TestSnapshot:計画保存{
        [Parameter]public Action<TestSnapshot> Capture{get;set;}=null!;
        protected override void OnInitialized()=>this.Capture(this);
        public void IncludeCode(){typeof(計画保存).GetMethod("コード選択時",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,[new ChangeEventArgs{Value=true}]);this.StateHasChanged();}
    }
    private static (QueryPlanResult,PlanDocument,ObjectExecutionStatistics) Example(){
        var trace=ManagementJson.Element(new{schemaVersion=1,methodKey=Secret,events=new[]{
            new{sequence=1,runId=1,layer=1,phase="NormalizeStages",ruleId="Pipeline.Take",round=1,pass=1,changed=true,revisited=false,before=Secret,after="<script>steal()</script>"},
            new{sequence=2,runId=1,layer=1,phase=Secret,ruleId=Secret,round=2,pass=1,changed=true,revisited=true,before=Secret,after=Secret}},
            layerTerminations=new[]{new{runId=1,layer=1,roundCount=2,termination=0}},summary=new{rewriteCount=1,maxRewriteDepth=1,optimizationPassCount=2,reachedFixedPoint=true,appliedRuleNames=new[]{"Pipeline.Take",Secret}}});
        var first=new ExecutionPlanNode(Secret,"RuntimeCondition",Secret+"<script>steal()</script>",[],Secret,"private-source/"+Secret,1,12,new(){{"Expression",Secret},{"LoweredExpression",Secret}}){Layer="GeneratedControl",OutputType="global::System.Boolean",Nullability="NotNullable"};
        var second=new ExecutionPlanNode("second","Expression",Secret,[first.Id],null,null,0,0,new(){{"Before",Secret}}){Layer="OriginalExpression",OutputType="int",Measurement="NotIndependentlyMeasured"};
        var plan=new PlanDocument(2,Secret,"1.0.0",new('B',64),new('C',64),[first,second],trace,"build1"){
            Edges=[new(first.Id,second.Id,"SourceMapping",Secret)],Diagnostics=[Secret]
        };
        var target=new QueryPlanResult("definition",1,new('A',64),new('F',64),false,new("dbo.Query",[Secret],[plan]));
        var statistics=new ObjectExecutionStatistics(Guid.NewGuid(),"definition","dbo.Query",1,new('A',64),DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch.AddMilliseconds(10),TimeSpan.FromMilliseconds(10),TimeSpan.FromMilliseconds(2),TimeSpan.FromMilliseconds(4),2,1,true);
        return(target,plan,statistics);
    }
}
