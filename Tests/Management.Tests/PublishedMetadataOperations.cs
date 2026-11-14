using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;
using Server=Seamless.Management.Server.ManagementServer;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class PublishedMetadataOperations{
    [Fact]public async Task MetadataReadRowsAndViewExecutionKeepIndependentPermissionsAndRecheckAfterAdmission(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");using var service=new Service(store);var admin=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        var definition=NativeWorkflow.Database();definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>file with{Text=file.Text.Replace("public Table<","// PRIVATE_METADATA_SOURCE_731\npublic Table<",StringComparison.Ordinal)+"\nnamespace Model.Views{public readonly record struct All(int Id,int Value);}namespace Model{public static class DbQueries{extension(Db.IReader q){public global::Seamless.Collections.ISet<Views.All> All=>global::Seamless.Linq.Set.Select(q.Rows,row=>new Views.All(row.Id,row.Value));}}}"}).ToArray()}};NativeWorkflow.Publish(service,admin,definition);
        await service.CommitRowsAsync(admin,new("Native",1,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=7}))]));
        UserAccount Account(string name,ManagementPermission permissions){service.ChangeUser(admin,new(name,"ViewerPassword-123",[new("Native",permissions)]));return service.Authenticate(service.Login(new(name,"ViewerPassword-123")).Token);}
        var viewer=Account("viewer",ManagementPermission.View);var reader=Account("reader",ManagementPermission.View|ManagementPermission.ReadRows);var executor=Account("executor",ManagementPermission.View|ManagementPermission.ReadRows|ManagementPermission.Execute);
        Assert.Contains(service.Objects(viewer,new("db:db")).Items,item=>item.Id=="All");var details=service.PublishedObjectDetails(viewer,new("db:db",ObjectId:"Rows"));Assert.Contains(details.Columns,column=>column.Name=="Value");Assert.Null(details.Source.Path);Assert.False(details.Source.Available);Assert.Equal(0,details.Source.Start);Assert.Equal(0,details.Source.Length);Assert.DoesNotContain("PRIVATE_METADATA_SOURCE_731",JsonSerializer.Serialize(details,ManagementJson.Options));
        Assert.Null(service.PublishedObjectDetails(admin,new("db:db",ObjectId:"Rows")).Source.Path);Assert.Equal("Permission",Assert.Throws<ManagementException>(()=>service.GetDefinition(viewer,"db")).Code);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>service.ReadRowsAsync(viewer,new("Native","Rows")))).Code);Assert.Single((await service.ReadRowsAsync(reader,new("Native","Rows"))).Rows);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>service.ExecuteObjectAsync(reader,new("Native","All",[])))).Code);Assert.Single((await service.ExecuteObjectAsync(executor,new("Native","All",[]))).Rows);
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>service.CommitRowsAsync(reader,new("Native",1,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=2,Value=8}))])))).Code);
        service.AfterRowAuthorization=()=>{service.AfterRowAuthorization=null;service.ChangeUser(admin,new("reader",null,[new("Native",ManagementPermission.View)]));};
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>service.ReadRowsAsync(reader,new("Native","Rows")))).Code);
        service.AfterRowAuthorization=()=>{service.AfterRowAuthorization=null;service.ChangeUser(admin,new("executor",null,[new("Native",ManagementPermission.View|ManagementPermission.ReadRows)]));};
        Assert.Equal("Permission",(await Assert.ThrowsAsync<ManagementException>(()=>service.ExecuteObjectAsync(executor,new("Native","All",[])))).Code);
        using var key=RSA.Create(2048);var certificateRequest=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);using var certificate=certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));await using var server=new Server(service,certificate,new(IPAddress.Loopback,0),"viewer");server.Start();var client=ManagementClient.ForLocalPipe(server.PipeName!);Assert.Equal("viewer",(await client.LocalIdentityAsync()).Name);
        var remote=await client.GetObjectDetailsAsync(new("db:db",ObjectId:"Rows"));Assert.Null(remote.Source.Path);Assert.False(remote.Source.Available);Assert.Contains((await client.ObjectsAsync(new("db:db"))).Items,item=>item.Id=="All");_ =await client.SchemasAsync(new("db:db"));
    }
}
