using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class CommonFunctions{
    [Fact]public async Task DatabaseMembersAndRegisteredEntriesShareMetadataAndTheOwnerPipe(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);var owner=Owner();
        var database=NativeWorkflow.Database();
        // 関数は生成される入口(Db.IReader)への拡張メンバー。
        database=database with{Source=database.Source with{Files=database.Source.Files.Select(file=>file with{Text=file.Text+"\nnamespace Model{public static class DbQueries{extension(Db.IReader q){public int TimesTen(int value)=>value*10;}}}"}).ToArray()}};
        NativeWorkflow.Publish(service,owner,database);
        NativeWorkflow.Publish(service,owner,Query("calculate","int","return value+context.GetDatabase<Db>().Rows.Count;",false,",int value"));
        NativeWorkflow.Publish(service,owner,Query("numbers","System.Collections.Generic.IEnumerable<int>","return System.Linq.Enumerable.Range(1,count);",false,",int count"));
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();using var client=ManagementClient.ForLocalPipe(server.PipeName!);
        Assert.Contains("CommonFunctionsV1",(await client.InfoAsync()).Capabilities);
        var page=await client.FunctionsAsync(new("db:db"));
        var member=Assert.Single(page.Items);Assert.Equal(FunctionOrigin.DatabaseMember,member.Target.Origin);Assert.Equal("TimesTen",member.Name);Assert.True(page.HasMore);
        var registered=await client.FunctionsAsync(new("db:db",page.VersionToken,page.NextCursor));Assert.False(registered.HasMore);
        Assert.Equal(new[]{"calculate","numbers"},registered.Items.Select(item=>item.Name).Order(StringComparer.Ordinal));
        var details=await client.GetFunctionDetailsAsync(registered.Items.Single(item=>item.Name=="calculate").Target);
        Assert.Equal("value",Assert.Single(details.Parameters).Name);Assert.Equal("System.Int32",details.ReturnType);Assert.False(details.IsSequence);Assert.Equal("Run",details.Function.EntryMember);
        var scalar=await client.ExecuteFunctionAsync(new(details.Function.Target,[JsonSerializer.SerializeToElement(7)],ExpectedVersion:details.Function.Version,ExpectedArtifactHash:details.Function.ArtifactHash){MeasurementMode=ObjectExecutionMeasurementMode.Summary});
        Assert.Equal(7,scalar.Value!.Value.GetInt32());Assert.False(scalar.IsSequence);Assert.Equal("calculate",scalar.Statistics!.DefinitionId);Assert.Equal("calculate",scalar.Statistics.ObjectId);
        var memberResult=await client.ExecuteFunctionAsync(new(member.Target,[JsonSerializer.SerializeToElement(3)]));Assert.Equal(30,memberResult.Value!.Value.GetInt32());
        var streamDetails=await client.GetFunctionDetailsAsync(registered.Items.Single(item=>item.Name=="numbers").Target);Assert.True(streamDetails.IsSequence);Assert.Equal("System.Int32",streamDetails.ElementType);
        var stream=await client.ExecuteFunctionAsync(new(streamDetails.Function.Target,[JsonSerializer.SerializeToElement(3)],Limit:2){MeasurementMode=ObjectExecutionMeasurementMode.Summary,TraceOptions=new(QueryTraceRecordMode.BoundaryEvents,QueryTraceReliability.Complete)});
        Assert.Equal(new[]{1,2},stream.Rows.Select(row=>row.GetInt32()));Assert.True(stream.HasMore);Assert.Equal(3,stream.Statistics!.ObservedOutputCount);Assert.Equal(2,stream.Statistics.ReturnedOutputCount);Assert.Equal(stream.Statistics.ExecutionId,stream.TraceExecutionId);
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));QueryTracePage? trace=null;
        try{
            do{
                trace=await service.QueryTraceAsync(owner,new("numbers","numbers",stream.TraceExecutionId),deadline.Token);
                if(trace.InvalidFiles>0){
                    var directory=Path.Combine(store.Root,"query-trace-tsavorite-v1");var periods=new ExecutionHistoryPeriods(directory,store.ResourceBudget,"Trace");
                    foreach(var period in Directory.EnumerateDirectories(directory,"period-*")){_=periods.ReadRows(period,deadline.Token);}
                }
                if(!trace.Frames.Any(frame=>frame.Kind=="Completed")){await Task.Delay(25,deadline.Token);}
            }while(!trace.Frames.Any(frame=>frame.Kind=="Completed"));
        }catch(OperationCanceledException)when(deadline.IsCancellationRequested){Assert.Fail("Live trace did not reach Completed. Last page: "+JsonSerializer.Serialize(trace,ManagementJson.Options));}
        Assert.NotNull(trace);
        Assert.All(trace.Frames,frame=>{Assert.Equal("numbers",frame.DefinitionId);Assert.Equal("numbers",frame.ObjectId);Assert.Equal(stream.TraceExecutionId,frame.ExecutionId);});
        Assert.Equal("Succeeded",trace.Frames.Single(frame=>frame.Kind=="Completed").Outcome);
        var wrong=await Assert.ThrowsAsync<ManagementException>(()=>client.ExecuteFunctionAsync(new(details.Function.Target,[JsonSerializer.SerializeToElement(1)],ExpectedVersion:2)));Assert.Equal("CatalogChanged",wrong.Code);
        Assert.Equal("InvalidRequest",(await Assert.ThrowsAsync<ManagementException>(()=>client.ExecuteFunctionAsync(new(details.Function.Target,[])))).Code);
        var viewer=new UserAccount("viewer","",[new("Native",ManagementPermission.View)]);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>service.ExecuteFunctionAsync(viewer,new(details.Function.Target,[JsonSerializer.SerializeToElement(1)])))).Code);
    }

    [Fact]public async Task RegisteredUpdatesDrainTheIteratorAndRollbackOnLimitFailureAndResponseBudget(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);var owner=Owner();
        NativeWorkflow.Publish(service,owner,NativeWorkflow.Database());
        NativeWorkflow.Publish(service,owner,Query("change","object?","var db=context.GetDatabase<Db>();db.Rows.Add(new Records.Row(id,id));if(mode==1){return new[]{1,2};}if(mode==2){throw new System.InvalidOperationException(\"rollback\");}if(mode==3){return new string('x',4*1024*1024);}return System.Array.Empty<int>();",true,",int id,int mode"));
        NativeWorkflow.Publish(service,owner,Query("count","int","return context.GetDatabase<Db>().Rows.Count;"));
        var target=new FunctionTarget(FunctionOrigin.RegisteredEntry,"db:db","change","change");
        FunctionExecutionRequest Request(int id,int mode)=>new(target,[JsonSerializer.SerializeToElement(id),JsonSerializer.SerializeToElement(mode)],Limit:1){MeasurementMode=ObjectExecutionMeasurementMode.Summary};
        var limit=await Assert.ThrowsAsync<ManagementException>(()=>service.ExecuteFunctionAsync(owner,Request(1,1)));Assert.Equal("ResultLimit",limit.Code);
        var failure=await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ExecuteFunctionAsync(owner,Request(2,2)));Assert.Equal("rollback",failure.Message);
        var budget=await Assert.ThrowsAsync<ManagementException>(()=>service.ExecuteFunctionAsync(owner,Request(3,3)));Assert.Equal("Limit",budget.Code);
        var counter=new FunctionTarget(FunctionOrigin.RegisteredEntry,"db:db","count","count");
        Assert.Equal(0,(await service.ExecuteFunctionAsync(owner,new(counter,[]))).Value!.Value.GetInt32());
        var committed=await service.ExecuteFunctionAsync(owner,Request(4,0));Assert.True(committed.IsSequence);Assert.Empty(committed.Rows);Assert.False(committed.HasMore);Assert.Equal(0,committed.Statistics!.ReturnedOutputCount);
        Assert.Equal(1,(await service.ExecuteFunctionAsync(owner,new(counter,[]))).Value!.Value.GetInt32());
    }

    [Fact]public async Task FunctionDetailsInspectActualTypesWithoutInvokingTheEntryAndRejectWrongOrigins(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);var owner=Owner();
        NativeWorkflow.Publish(service,owner,NativeWorkflow.Database());
        NativeWorkflow.Publish(service,owner,Query("metadata","decimal?","throw new System.InvalidOperationException(\"invoked\");",false,",System.Collections.Generic.Dictionary<string,decimal[]> input"));
        var target=new FunctionTarget(FunctionOrigin.RegisteredEntry,"db:db","metadata","metadata");var details=service.GetFunctionDetails(owner,target);
        Assert.Contains("Nullable",details.ReturnType);Assert.Contains("Dictionary",Assert.Single(details.Parameters).Type);Assert.False(details.IsSequence);
        Assert.Equal("InvalidRequest",Assert.Throws<ManagementException>(()=>service.GetFunctionDetails(owner,target with{DefinitionId="db",ObjectId="db"})).Code);
        Assert.Equal("InvalidRequest",Assert.Throws<ManagementException>(()=>service.GetFunctionDetails(owner,new(FunctionOrigin.DatabaseMember,"db:db","other","Rows"))).Code);
        var argument=JsonSerializer.SerializeToElement(new Dictionary<string,decimal[]>{{"value",[1m]}});
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ExecuteFunctionAsync(owner,new(target,[argument])));Assert.Equal("invoked",error.Message);
    }

    [Fact]public void UnifiedTargetsRejectInvalidIdentityAndFreezeMutableArguments(){
        var target=new FunctionTarget(FunctionOrigin.RegisteredEntry,"db:db","query","query");var arguments=new[]{JsonSerializer.SerializeToElement(1)};
        var request=new FunctionExecutionRequest(target,arguments).Freeze();arguments[0]=JsonSerializer.SerializeToElement(99);Assert.Equal(1,request.Arguments[0].GetInt32());
        Assert.Equal(target,ManagementJson.Read<FunctionTarget>(ManagementJson.Element(target)));
        Assert.Throws<ManagementException>(()=>(target with{ObjectId="other"}).Validate());Assert.Throws<ManagementException>(()=>(target with{Origin=(FunctionOrigin)99}).Validate());Assert.Throws<ManagementException>(()=>(target with{DatabaseId="db:"}).Validate());
    }
    [Fact]public async Task PublicationBetweenMetadataAndGateAdmissionRejectsTheOldSignatureIdentity(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);var owner=Owner();
        NativeWorkflow.Publish(service,owner,NativeWorkflow.Database());
        var query=Query("versioned","int","return value;",false,",int value");NativeWorkflow.Publish(service,owner,query);
        var target=new FunctionTarget(FunctionOrigin.RegisteredEntry,"db:db","versioned","versioned");var details=service.GetFunctionDetails(owner,target);
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        service.BeforeQueryAdmission=()=>{entered.Set();if(!release.Wait(TimeSpan.FromSeconds(20))){throw new TimeoutException("admission release");}};
        var pending=Task.Run(()=>service.ExecuteFunctionAsync(owner,new(target,[JsonSerializer.SerializeToElement(1)],ExpectedVersion:details.Function.Version,ExpectedArtifactHash:details.Function.ArtifactHash)));
        try{
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            query=query with{Source=query.Source with{Revision="2",Files=query.Source.Files.Select(file=>file with{Text=file.Text.Replace("return value;","return value+100;",StringComparison.Ordinal)}).ToArray()}};
            NativeWorkflow.Publish(service,owner,query,1,1);
        }finally{release.Set();service.BeforeQueryAdmission=null;}
        var error=await Assert.ThrowsAsync<ManagementException>(async()=>await pending);Assert.Equal("CatalogChanged",error.Code);
        Assert.Equal(101,(await service.ExecuteFunctionAsync(owner,new(target,[JsonSerializer.SerializeToElement(1)]))).Value!.Value.GetInt32());
    }
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"functions-"+Guid.NewGuid().ToString("N"));
    private static UserAccount Owner()=>new("admin","",[new("*",ManagementPermission.All)]);
    private static Definition Query(string id,string result,string body,bool writes=false,string parameters="")=>new(id,"Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","using Seamless.Management.Server;namespace Model;public static class Query{public static "+result+" Run(ManagementExecutionContext context"+parameters+"){"+body+"}}")],[]),writes);
}
