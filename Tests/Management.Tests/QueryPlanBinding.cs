using Seamless.Management;
using Seamless.Management.Server;
using System.Text.Json;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed partial class QueryPlanBinding{
    [Fact]public async Task PublishedAndDraftQueryPlansUseTheirVersionAndProtectedIpcEntry(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"query-plans-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        var owner=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        NativeWorkflow.Publish(service,owner,NativeWorkflow.Database());
        var source=new SourceSnapshot("query",[new("query.cs","""
            using System.Linq;using Seamless.FLinq;using Seamless.Management.Server;
            namespace Model;
            public sealed class PublicQueries{
                public static int Run(ManagementExecutionContext context)=>Sum(new[]{1,2,3});
                [LinqIntercept(true)] private static int Sum(int[] values)=>values.Where(value=>value>0).Sum();
            }
            """)],[]);
        var query=new Definition("sum-query","Native",DefinitionKind.Query,"Model.PublicQueries","Run",source);
        NativeWorkflow.Publish(service,owner,query);
        var result=service.QueryPlans(owner,new(query.Id,query.Id,1));
        Assert.False(result.FromDraft);Assert.Equal(1,result.Version);Assert.Single(result.Selection.Plans);
        Assert.Null(Assert.Single(result.Selection.Plans).GeneratedCode);
        var generated=service.QueryPlans(owner,new(query.Id,query.Id,1){IncludeGeneratedCode=true});
        Assert.Equal("Available",Assert.Single(generated.Selection.Plans).GeneratedCode!.Status);
        Assert.Null(Assert.Single(service.Plans(owner,query.Id).Plans).GeneratedCode);
        Assert.Equal(6,Assert.Single(service.Execute(owner,new(query.Id,null,JsonSerializer.SerializeToElement(new{})),CancellationToken.None)).Value.GetInt32());
        var viewer=new UserAccount("viewer","",[new("Native",ManagementPermission.View)]);
        Assert.Equal("Permission",Assert.Throws<ManagementException>(()=>service.QueryPlans(viewer,new(query.Id,query.Id))).Code);
        Assert.Equal("Permission",Assert.Throws<ManagementException>(()=>service.QueryPlans(viewer,new(query.Id,query.Id){IncludeGeneratedCode=true})).Code);
        Assert.Equal("SchemaMismatch",Assert.Throws<ManagementException>(()=>service.QueryPlans(owner,new(query.Id,query.Id,2))).Code);
        await using var server=global::Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();
        using var client=ManagementClient.ForLocalPipe(server.PipeName!);
        Assert.Single((await client.QueryPlansAsync(new(query.Id,query.Id,1))).Selection.Plans);
        Assert.Equal("Available",Assert.Single((await client.QueryPlansAsync(new(query.Id,query.Id,1){IncludeGeneratedCode=true})).Selection.Plans).GeneratedCode!.Status);
        Assert.Equal(query.Id,Assert.Single((await client.QueriesAsync(new("db:db"))).Items).Definition.Id);
        using var sessions=new LocalBrowserSessions(store.InstanceId);sessions.BindOrigin(new("http://127.0.0.1:24502/"));
        var session=sessions.Redeem(new Uri(sessions.CreateLaunch().Url).Fragment[1..],"http://127.0.0.1:24502");
        var browser=new BrowserManagementOperations(sessions,client,()=>Task.FromResult<string?>(session.Id));
        Assert.Single((await browser.QueryPlansAsync(new(query.Id,query.Id,1))).Selection.Plans);
        Assert.Equal(query.Id,Assert.Single((await browser.QueriesAsync(new("db:db"))).Items).Definition.Id);
        sessions.RevokeAll();
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>browser.QueryPlansAsync(new(query.Id,query.Id,1)))).Code);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>browser.QueryPlansAsync(new(query.Id,query.Id,1){IncludeGeneratedCode=true}))).Code);
        Assert.Equal("Authentication",(await Assert.ThrowsAsync<ManagementException>(()=>browser.QueriesAsync(new("db:db")))).Code);
        var changed=query with{Source=source with{Files=[source.Files[0] with{Text=source.Files[0].Text.Replace("value>0","value>1",StringComparison.Ordinal)}]}};
        var saved=await client.SaveDraftAsync(new(changed,0,1));
        var draft=await client.QueryPlansAsync(new(query.Id,query.Id,1,saved.Revision));
        Assert.True(draft.FromDraft);Assert.Single(draft.Selection.Plans);Assert.NotEqual(result.SourceHash,draft.SourceHash);
        var adminDraft=service.GetDraft(owner,query.Id);
        NativeWorkflow.Publish(service,owner,changed,adminDraft.Revision,1);
        Assert.Equal("SchemaMismatch",Assert.Throws<ManagementException>(()=>service.QueryPlans(owner,new(query.Id,query.Id,1))).Code);
        var old=await client.QueryPlansAsync(new(query.Id,query.Id,1){HistoricalVersion=1});
        Assert.Equal(result.ArtifactHash,old.ArtifactHash);Assert.Equal(source.Hash(),old.HistoricalSource!.Hash());
        Assert.Single(old.Selection.Plans);
        Assert.Equal("Permission",Assert.Throws<ManagementException>(()=>service.QueryPlans(viewer,new(query.Id,query.Id){HistoricalVersion=1})).Code);
        Assert.Equal("InvalidRequest",Assert.Throws<ManagementException>(()=>service.QueryPlans(owner,new(query.Id,query.Id,DraftRevision:1){HistoricalVersion=1})).Code);
    }
    [Fact]public void ObjectHistoryFiltersByExactIdentity(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"object-history-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        var now=DateTimeOffset.UtcNow;
        foreach(var id in new[]{"Items","dbo0.Items","Other"}){
            store.AppendAudit(new(Guid.NewGuid(),null,now,now,Guid.NewGuid(),"admin","Plans",id,AuditAction.Read,AuditOutcome.Succeeded,null,null,null,"Ok","Items"));
        }
        var history=store.AuditEvents(new(Database:"Plans"){ObjectId="Items"});
        Assert.Equal("Items",Assert.Single(history.Items).ObjectId);
        Assert.Equal("Unsupported",Assert.Throws<ManagementException>(()=>store.ReadSummaries(new(Database:"Plans"){ObjectId="Items"})).Code);
    }
    private static SourceSnapshot Source()=>new("plans",[new("queries.cs","""
        using System.Linq;
        using Seamless.FLinq;
        namespace QueryCases{
            public sealed class First{
                public static int Run(int[] values)=>Sum(values);
                [LinqIntercept(true)] private static int Sum(int[] values)=>values.Where(value=>value>0).Sum();
            }
            public sealed class Second{
                public static int Run(int[] values)=>Sum(values);
                [LinqIntercept(true)] private static int Sum(int[] values)=>values.Where(value=>value>0).Sum();
            }
        }
        """)],[]);

    [Fact]public void SameNamedMethodsAreBoundThroughTheirActualSourceCalls(){
        var source=Source();using var compiled=new RuntimeCompiler().Compile(source);
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        Assert.Equal(2,compiled.Result.Plans.Length);
        foreach(var name in new[]{"First","Second"}){
            var definition=new Definition(name,"Plans",DefinitionKind.Query,"QueryCases."+name,"Run",source);
            var selection=global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,name,compiled.Result.Plans);
            var plan=Assert.Single(selection.Plans);
            Assert.Equal("QueryCases."+name+".Sum(int[])",plan.MethodKey);
            Assert.Null(selection.UnavailableReason);
        }
    }

    [Fact]public void APlanFromAnotherSourceRevisionIsNotAttached(){
        var source=Source();using var compiled=new RuntimeCompiler().Compile(source);
        var changed=source with{Files=[source.Files[0] with{Text=source.Files[0].Text.Replace("value>0","value>10",StringComparison.Ordinal)}]};
        var definition=new Definition("first","Plans",DefinitionKind.Query,"QueryCases.First","Run",changed);
        var selection=global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,"first",compiled.Result.Plans);
        Assert.Empty(selection.Plans);Assert.NotNull(selection.UnavailableReason);
    }

    [Fact]public void TablesAreNotQueryPlanTargets(){
        var definition=global::Seamless.Management.DatabaseDesigner.Apply(global::Seamless.Management.DatabaseDesigner.Empty("plans","Plans"),new("Items","",[new("Id","int",true)],[]));
        var error=Assert.Throws<ManagementException>(()=>global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,"Items",[]));
        Assert.Equal("InvalidRequest",error.Code);
    }

    [Fact]public void ViewAndFunctionTargetsRetainTheirScope(){
        var definition=global::Seamless.Management.DatabaseDesigner.AddSchema(global::Seamless.Management.DatabaseDesigner.Empty("plans","Plans"),"dbo0");
        foreach(var schema in new[]{"","dbo0"}){
            definition=global::Seamless.Management.DatabaseDesigner.ApplyQuery(definition,new("Values",schema,DatabaseQueryKind.View,[new("Id","int",false)],[],"","return [];"));
            definition=global::Seamless.Management.DatabaseDesigner.ApplyQuery(definition,new("Amount",schema,DatabaseQueryKind.ScalarFunction,[],[],"int","return 1;"));
        }
        foreach(var id in new[]{"Values","dbo0.Values","Amount","dbo0.Amount"}){
            var selection=global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,id,[]);
            Assert.Equal(id,selection.ObjectId);Assert.NotEmpty(selection.Methods);Assert.Empty(selection.Plans);
        }
        Assert.NotEqual(global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,"Values",[]).Methods[0],global::Seamless.Management.DatabaseDesigner.SelectQueryPlans(definition,"dbo0.Values",[]).Methods[0]);
    }
}
