using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class ObjectExecutionProfiling{
    [Fact]public async Task DatabaseAndRegisteredFunctionsReturnBoundProfileOnlyWhenExplicitlyRequestedAndThroughThePipe(){
        using var fixture=new Fixture();
        var plain=await fixture.Service.ExecuteObjectAsync(fixture.Owner,new("Native","Amount",[]));
        var summary=await fixture.Service.ExecuteObjectAsync(fixture.Owner,new("Native","Amount",[]){MeasurementMode=ObjectExecutionMeasurementMode.Summary});
        Assert.Null(plain.Profile);Assert.Null(summary.Profile);Assert.Equal(5,plain.Value!.Value.GetInt32());
        var profiled=await fixture.Service.ExecuteObjectAsync(fixture.Owner,new("Native","Amount",[]){MeasurementMode=ObjectExecutionMeasurementMode.Profiled});
        Check(profiled,"db","Amount");Assert.Equal(5,profiled.Value!.Value.GetInt32());
        await using var server=global::Seamless.Management.Server.ManagementServer.ForLocalOwner(fixture.Service);server.Start();using var client=ManagementClient.ForLocalPipe(server.PipeName!);
        var target=new FunctionTarget(FunctionOrigin.RegisteredEntry,"db:db","profile-entry","profile-entry");
        var result=await client.ExecuteFunctionAsync(new(target,[]){MeasurementMode=ObjectExecutionMeasurementMode.Profiled});
        Check(result,"profile-entry","profile-entry");Assert.Equal(5,result.Value!.Value.GetInt32());
        var json=JsonSerializer.Serialize(new ObjectExecutionResult(1,false,null,[],[],false),ManagementJson.Options);Assert.DoesNotContain("profile",json,StringComparison.OrdinalIgnoreCase);
        var request=new FunctionExecutionRequest(target,[]){MeasurementMode=ObjectExecutionMeasurementMode.Profiled};Assert.Equal(ObjectExecutionMeasurementMode.Profiled,JsonSerializer.Deserialize<FunctionExecutionRequest>(JsonSerializer.Serialize(request,ManagementJson.Options),ManagementJson.Options)!.MeasurementMode);
    }
    [Fact]public async Task ProfileMetadataNeedsSourcePermissionBeforeInvocationAndAfterPermissionRevocation(){
        using var fixture=new Fixture(sequence:true);var permissions=ManagementPermission.View|ManagementPermission.Execute|ManagementPermission.ReadRows;
        fixture.Service.ChangeUser(fixture.Owner,new("profile-reader","TestPassword-456",[new("Native",permissions)]));var reader=fixture.Service.Authenticate(fixture.Service.Login(new("profile-reader","TestPassword-456")).Token);
        Assert.Equal(5,(await fixture.Service.ExecuteObjectAsync(reader,new("Native","Amount",[]))).Value!.Value.GetInt32());
        var denied=await Assert.ThrowsAsync<ManagementException>(()=>fixture.Service.ExecuteObjectAsync(reader,new("Native","Amount",[]){MeasurementMode=ObjectExecutionMeasurementMode.Profiled}));Assert.Equal("Permission",denied.Code);
        fixture.Service.ChangeUser(fixture.Owner,new("profile-reader",null,[new("Native",permissions|ManagementPermission.Edit)]));
        fixture.Service.AfterRowAuthorization=()=>fixture.Service.ChangeUser(fixture.Owner,new("profile-reader",null,[new("Native",permissions)]));
        try{var revoked=await Assert.ThrowsAsync<ManagementException>(()=>fixture.Service.ExecuteObjectAsync(reader,new("Native","Amount",[]){MeasurementMode=ObjectExecutionMeasurementMode.Profiled}));Assert.Equal("Permission",revoked.Code);}
        finally{fixture.Service.AfterRowAuthorization=null;}
        fixture.Service.ChangeUser(fixture.Owner,new("profile-reader",null,[new("Native",permissions|ManagementPermission.Edit)]));
        var revokedAfterOutput=false;
        fixture.Service.AfterQueryRow=()=>{revokedAfterOutput=true;fixture.Service.ChangeUser(fixture.Owner,new("profile-reader",null,[new("Native",permissions)]));};
        try{var target=new FunctionTarget(FunctionOrigin.RegisteredEntry,"db:db","profile-entry","profile-entry");var revoked=await Assert.ThrowsAsync<ManagementException>(()=>fixture.Service.ExecuteFunctionAsync(reader,new(target,[]){MeasurementMode=ObjectExecutionMeasurementMode.Profiled}));Assert.True(revokedAfterOutput);Assert.Equal("Permission",revoked.Code);}
        finally{fixture.Service.AfterQueryRow=null;}
        Assert.Equal(5,(await fixture.Service.ExecuteObjectAsync(fixture.Owner,new("Native","Amount",[]))).Value!.Value.GetInt32());Assert.Null(Seamless.Execution.QueryProfiler.Current);
    }
    private static void Check(ObjectExecutionResult result,string definitionId,string objectId){
        var profile=Assert.IsType<ObjectExecutionProfile>(result.Profile);var stats=Assert.IsType<ObjectExecutionStatistics>(result.Statistics);
        Assert.Equal(stats.ExecutionId,profile.ExecutionId);Assert.Equal(definitionId,profile.DefinitionId);Assert.Equal(objectId,profile.ObjectId);Assert.Equal(stats.Version,profile.Version);Assert.Equal(stats.ArtifactHash,profile.ArtifactHash);
        Assert.True(profile.QueryStartedTimestamp<=profile.StartedTimestamp);var region=Assert.Single(profile.Regions);Assert.Equal(2,region.InputCount);Assert.Equal(1,region.OutputCount);Assert.True(region.InputCountKnown);Assert.Equal("FusedPipelineInclusive",region.Scope);Assert.Equal("Succeeded",region.Outcome);
        Assert.Contains("NodeProfileNotPersisted",profile.Diagnostics);Assert.Contains("WorkerNotCollected",profile.Diagnostics);
    }
    private sealed class Fixture:IDisposable{
        private readonly ManagementStore store;
        internal Service Service{get;}
        internal UserAccount Owner{get;}=new("admin","",[new("*",ManagementPermission.All)]);
        internal Fixture(bool sequence=false){
            var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"profile-api-"+Guid.NewGuid().ToString("N"));
            this.store=ManagementStore.Initialize(root,"admin","TestPassword-123");this.Service=new(this.store,null,startAuditWorker:false);
            try{
                var database=NativeWorkflow.Database();database=database with{Source=database.Source with{Files=database.Source.Files.Select(file=>file with{Text="using System.Linq;using Seamless.FLinq;"+file.Text.Replace("public Table<","public ScalarFunction<int> Amount()=>this.ScalarFunction(()=>this.ProfileSum(new[]{1,2,3}));[LinqIntercept(true)] private int ProfileSum(int[] values)=>values.Where(value=>value>1).Sum();public Table<",StringComparison.Ordinal)}).ToArray()}};
                NativeWorkflow.Publish(this.Service,this.Owner,database);
                var entry="public static int Run(ManagementExecutionContext context)=>Compute(new[]{1,2,3});";
                if(sequence){entry="public static System.Collections.Generic.IEnumerable<int> Run(ManagementExecutionContext context){yield return Compute(new[]{1,2,3});}";}
                var query=new Definition("profile-entry","Native",DefinitionKind.Query,"Model.Registered","Run",new("1",[new("profile-entry.cs","using System.Linq;using Seamless.FLinq;using Seamless.Management.Server;namespace Model;public sealed class Registered{"+entry+"[LinqIntercept(true)] private static int Compute(int[] values)=>values.Where(value=>value>1).Sum();}")],[]));
                NativeWorkflow.Publish(this.Service,this.Owner,query);
            }catch{this.Service.Dispose();this.store.Dispose();throw;}
        }
        public void Dispose(){this.Service.Dispose();this.store.Dispose();}
    }
}
