using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class ObjectExecution{
    [Fact]public async Task PublishedQueriesUseTheirScopeAndRespectPermissionsAndLimits(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"objects-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        var owner=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        var definition=Designer.AddSchema(Designer.Empty("objects","Objects"),"dbo0");
        foreach(var schema in new[]{"","dbo0"}){
            definition=Designer.Apply(definition,new("Items",schema,[new("Id","int",true),new("Value","decimal",false)],[]));
            var source="container.";if(schema.Length>0){source+=schema+".";}
            definition=Designer.ApplyQuery(definition,new("ItemList",schema,DatabaseQueryKind.View,[new("Id","int",false),new("Value","decimal",false)],[],"","return "+source+"Items.Select(row=>new ItemList(row.Id,row.Value));"));
            definition=Designer.ApplyQuery(definition,new("Amount",schema,DatabaseQueryKind.ScalarFunction,[],[new("id","int")],"decimal","return "+source+"Items.Where(row=>row.Id==id).Sum(row=>row.Value);"));
            definition=Designer.ApplyQuery(definition,new("Find",schema,DatabaseQueryKind.TableFunction,[new("Id","int",false)],[new("minimum","decimal")],"IEnumerable<FindResult>","return "+source+"Items.Where(row=>row.Value>=minimum).Select(row=>new FindResult(row.Id));"));
        }
        NativeWorkflow.Publish(service,owner,definition);
        await service.CommitRowsAsync(owner,new("Objects",1,Guid.NewGuid(),[
            new("Items",TableRowChangeKind.Insert,Values:JsonSerializer.SerializeToElement(new{Id=1,Value=10m})),
            new("Items",TableRowChangeKind.Insert,Values:JsonSerializer.SerializeToElement(new{Id=2,Value=15m})),
            new("dbo0.Items",TableRowChangeKind.Insert,Values:JsonSerializer.SerializeToElement(new{Id=1,Value=20m}))]));
        foreach(var schema in new[]{"","dbo0."}){
            var amount=await service.ExecuteObjectAsync(owner,new("Objects",schema+"Amount",[JsonSerializer.SerializeToElement(1)],ExpectedVersion:1));
            var expected=10m;if(schema.Length>0){expected=20m;}
            Assert.False(amount.IsSequence);Assert.Equal(expected,amount.Value!.Value.GetDecimal());
            var view=await service.ExecuteObjectAsync(owner,new("Objects",schema+"ItemList",[],Limit:1));
            Assert.True(view.IsSequence);Assert.Single(view.Rows);
            var actual=view.Rows[0].GetProperty("value").GetDecimal();
            if(schema.Length==0){Assert.Contains(actual,new[]{10m,15m});}else{Assert.Equal(expected,actual);}
            Assert.Equal(schema.Length==0,view.HasMore);
            var find=await service.ExecuteObjectAsync(owner,new("Objects",schema+"Find",[JsonSerializer.SerializeToElement(18m)]));
            var count=0;if(schema.Length>0){count=1;}Assert.Equal(count,find.Rows.Length);
        }
        var viewer=new UserAccount("viewer","",[new("Objects",ManagementPermission.View|ManagementPermission.ReadRows)]);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>service.ExecuteObjectAsync(viewer,new("Objects","Amount",[JsonSerializer.SerializeToElement(1)])))).Code);
        Assert.Equal("InvalidRequest",(await Assert.ThrowsAsync<ManagementException>(()=>service.ExecuteObjectAsync(owner,new("Objects","Amount",[])))).Code);
        Assert.Equal("NotFound",(await Assert.ThrowsAsync<ManagementException>(()=>service.ExecuteObjectAsync(owner,new("Objects","CreateView",[])))).Code);
        Assert.Equal("SchemaMismatch",(await Assert.ThrowsAsync<ManagementException>(()=>service.ExecuteObjectAsync(owner,new("Objects","ItemList",[],ExpectedVersion:2)))).Code);
    }
}
