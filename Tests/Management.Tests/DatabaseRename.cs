using System.Text.Json;
using System.Reflection;
using Seamless.Management;
using Seamless.Management.Components;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;
using Designer=Seamless.Management.DatabaseDesigner;
using Document=Seamless.Management.Components.文書状態;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class DatabaseRename{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"rename-"+Guid.NewGuid().ToString("N"));
    private static UserAccount Admin()=>new("admin","",[new("*",ManagementPermission.All)]);
    private static DefinitionVersion Add(ManagementStore store,string id="db",string name="Before"){
        var version=new DefinitionVersion(Designer.Empty(id,name),1,"admin",DateTimeOffset.UtcNow,"immutable-artifact","generations/immutable");
        store.Write(()=>{store.Put("active",id,version);store.Put("version",id+":1",version);});return version;
    }
    private static RenameDatabaseRequest Request(string name="After")=>new("db:db","Before",name,Guid.NewGuid());
    [Fact]public void RenamePreservesTheArtifactAndMovesPermissionsAndPendingQueries(){
        var root=Root();var request=Request(" After ");
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new Service(store,null,startAuditWorker:false)){
            var before=Add(store);var user=Admin();service.ChangeUser(user,new("editor","EditorPassword-123",[new("Before",ManagementPermission.View|ManagementPermission.Edit)]));
            var pending=service.SaveDraft(user,new(new("pending","Before",DefinitionKind.Query,"Query","Run",new("1",[new("query.cs","public static class Query{}")],[])),0,0));
            var oldToken=service.Databases(user).VersionToken;
            var result=service.RenameDatabase(user,request);
            Assert.Equal("After",result.Name);Assert.Equal(before.Definition.Id,result.DefinitionId);
            var after=service.GetDefinition(user,"db");Assert.Equal("After",after.Database);Assert.Equal(before.Definition.Hash(),after.Definition.Hash());Assert.Equal(before.ArtifactHash,after.ArtifactHash);Assert.Equal(before.StoragePath,after.StoragePath);
            Assert.NotEqual(oldToken,service.Databases(user).VersionToken);
            Assert.Equal(result,service.RenameDatabase(user,request));
            Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>service.RenameDatabase(user,request with{Name="Other"})).Code);
            var editor=service.Authenticate(service.Login(new("editor","EditorPassword-123")).Token);
            Assert.Equal("After",Assert.Single(service.Databases(editor).Items).Name);Assert.Equal("After",Assert.Single(editor.Grants).Database);
            var changed=service.GetDraft(user,pending.Definition.Id);Assert.Equal(pending.Revision+1,changed.Revision);Assert.Equal("After",changed.Definition.Database);
            Add(store,"reused","Before");Assert.Equal(new[]{"After","Before"},service.Databases(user).Items.Where(item=>!item.IsSystem).Select(item=>item.Name).Order().ToArray());
        }
        using(var store=new ManagementStore(root))using(var service=new Service(store,null,startAuditWorker:false)){
            Assert.Equal("After",service.GetDefinition(Admin(),"db").Database);Assert.Equal("After",service.RenameDatabase(Admin(),request).Name);
        }
    }
    [Fact]public void ConflictPermissionReservedNameAndCancellationLeaveTheCatalogUnchanged(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);Add(store);Add(store,"other","Taken");
        var user=Admin();var token=service.Databases(user).VersionToken;
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>service.RenameDatabase(user,Request("Taken"))).Code);
        Assert.Equal("NotFound",Assert.Throws<ManagementException>(()=>service.RenameDatabase(user,Request() with{DatabaseId="db:missing"})).Code);
        foreach(var name in new[]{"system","SYSTEM","*","bad\nname"}){Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>service.RenameDatabase(user,Request(name))).Code);}
        Assert.Equal("InvalidRequest",Assert.Throws<ManagementException>(()=>service.RenameDatabase(user,Request("  "))).Code);
        Assert.Equal("Permission",Assert.Throws<ManagementException>(()=>service.RenameDatabase(new("viewer","",[new("Before",ManagementPermission.View)]),Request())).Code);
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>service.RenameDatabase(user,Request() with{ExpectedName="stale"})).Code);
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();Assert.Throws<OperationCanceledException>(()=>service.RenameDatabase(user,Request(),cancelled.Token));
        Assert.Equal(token,service.Databases(user).VersionToken);Assert.Equal("Before",service.GetDefinition(user,"db").Database);
        service.ChangeUser(user,new("future","FuturePassword-123",[new("Future",ManagementPermission.View)]));
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>service.RenameDatabase(user,Request("Future"))).Code);
    }
    [Fact]public void AFailedTransactionRollsBackEveryChangedRecordAndCanBeRetried(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);Add(store);
        service.ChangeUser(Admin(),new("reader","ReaderPassword-123",[new("Before",ManagementPermission.View)]));
        var token=service.Databases(Admin()).VersionToken;var request=Request();service.RenameCheckpoint=()=>throw new IOException("injected before commit");
        Assert.Throws<IOException>(()=>service.RenameDatabase(Admin(),request));Assert.Equal("Before",service.GetDefinition(Admin(),"db").Database);
        Assert.Equal("Before",Assert.Single(store.Read<UserAccount>("user","READER")?.Grants??store.Read<UserAccount>("user","reader")!.Grants).Database);
        Assert.Equal(token,service.Databases(Admin()).VersionToken);
        service.RenameCheckpoint=null;Assert.Equal("After",service.RenameDatabase(Admin(),request).Name);
    }
    [Fact]public async Task ConcurrentRenamesUseTheExpectedNameAsACompareAndSwap(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);Add(store);
        async Task<string> Rename(string name)=>await Task.Run(()=>{try{return service.RenameDatabase(Admin(),Request(name)).Name;}catch(ManagementException error){return error.Code;}});
        var results=await Task.WhenAll(Rename("First"),Rename("Second"));Assert.Single(results,result=>result=="Conflict");
        Assert.Contains(service.GetDefinition(Admin(),"db").Database,new[]{"First","Second"});
    }
    [Fact]public void ReusingAnOldNameDoesNotTransferThePreviousDatabasesHistoryPermissions(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);Add(store);
        store.Write(()=>store.AddHistory("admin","Before","Applied","db",1));
        var original=Assert.Single(service.History(Admin()).Items);
        store.Write(()=>store.Put("history",original.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),original with{DatabaseDefinitionId=null}));
        service.ChangeUser(Admin(),new("reader","ReaderPassword-123",[new("Before",ManagementPermission.View)]));
        service.RenameDatabase(Admin(),Request());Add(store,"replacement","Before");
        var reader=service.Authenticate(service.Login(new("reader","ReaderPassword-123")).Token);
        Assert.Contains(service.History(reader).Items,entry=>entry.Sequence==original.Sequence);
        var other=new UserAccount("other","",[new("Before",ManagementPermission.View)]);
        Assert.DoesNotContain(service.History(other).Items,entry=>entry.Sequence==original.Sequence);
    }
    [Fact]public async Task RowsQueriesAndLaterPublicationUseTheRenamedDatabaseAfterRestart(){
        var root=Root();
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))using(var service=new Service(store,null,startAuditWorker:false)){
            var user=Admin();NativeWorkflow.Publish(service,user,NativeWorkflow.Database());
            var query=new Definition("sum","Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","using System.Linq;using Seamless.Management.Server;namespace Model;public static class Query{public static int Run(ManagementExecutionContext context)=>context.GetDatabase<Db>().Rows.Sum(row=>row.Value);}")],[]));
            NativeWorkflow.Publish(service,user,query);
            await service.CommitRowsAsync(user,new("Native",1,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=7}))]));
            var before=service.GetDefinition(user,"db");service.RenameDatabase(user,new("db:db","Native","Renamed",Guid.NewGuid()));
            Assert.Equal(before.ArtifactHash,service.GetDefinition(user,"db").ArtifactHash);
            Assert.Single((await service.ReadRowsAsync(user,new("Renamed","Rows"))).Rows);
            Assert.Equal("NotFound",(await Assert.ThrowsAsync<ManagementException>(()=>service.ReadRowsAsync(user,new("Native","Rows")))).Code);
            Assert.Equal(7,Assert.Single(service.Execute(user,new("sum",null,ManagementJson.Element(new{})),CancellationToken.None)).Value.GetInt32());
            NativeWorkflow.Publish(service,user,before.Definition with{Source=before.Definition.Source with{Revision="2"}},1,1);
            Assert.Equal("Renamed",service.GetDefinition(user,"db").Database);Assert.Equal("Renamed",service.GetDefinition(user,"sum").Database);
            NativeWorkflow.Publish(service,user,NativeWorkflow.Database() with{Id="new-db"});
            Assert.Empty((await service.ReadRowsAsync(user,new("Native","Rows"))).Rows);Assert.Single((await service.ReadRowsAsync(user,new("Renamed","Rows"))).Rows);
        }
        using(var store=new ManagementStore(root))using(var service=new Service(store,null,startAuditWorker:false)){
            Assert.Single((await service.ReadRowsAsync(Admin(),new("Renamed","Rows"))).Rows);
            Assert.Equal(7,Assert.Single(service.Execute(Admin(),new("sum",null,ManagementJson.Element(new{})),CancellationToken.None)).Value.GetInt32());
        }
    }
    [Fact]public async Task OwnerPipeCarriesTheRenameOperation(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);Add(store);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);server.Start();using var client=ManagementClient.ForLocalPipe(server.PipeName!);
        var request=Request();Assert.Equal("After",(await client.RenameDatabaseAsync(request)).Name);Assert.Equal("After",(await client.RenameDatabaseAsync(request)).Name);
    }
    [Fact]public async Task ReusingANameAfterAuthorizationCannotRedirectARequestToAnotherDatabase(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);Add(store);
        service.ChangeUser(Admin(),new("reader","ReaderPassword-123",[new("Before",ManagementPermission.View|ManagementPermission.ReadRows)]));
        var reader=service.Authenticate(service.Login(new("reader","ReaderPassword-123")).Token);
        service.AfterRowAuthorization=()=>{service.RenameDatabase(Admin(),Request());Add(store,"replacement","Before");};
        Assert.Equal("Conflict",(await Assert.ThrowsAsync<ManagementException>(()=>service.ReadRowsAsync(reader,new("Before","Rows")))).Code);
        Assert.Equal("After",Assert.Single(store.Read<UserAccount>("user","READER")!.Grants).Database);
    }
    [Fact]public void OlderVersionJsonUsesItsArtifactDatabaseAsTheCurrentName(){
        var definition=Designer.Empty("legacy","Legacy");
        var json=JsonSerializer.Serialize(new{definition,version=1,owner="admin",createdAt=DateTimeOffset.UtcNow},ManagementJson.Options);
        Assert.Equal("Legacy",JsonSerializer.Deserialize<DefinitionVersion>(json,ManagementJson.Options)!.Database);
    }
    [Fact]public void TrustedInProcessOwnerIsIndependentOfAnApplicationAccountWithTheSameName(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);Add(store);
        service.ChangeUser(Admin(),new("owner","OwnerPassword-123",[new("Before",ManagementPermission.View)],true));
        var owner=new UserAccount("owner","",[new("*",ManagementPermission.All)]);
        Assert.Equal("After",service.RenameDatabase(owner,Request()).Name);
    }
    [Fact]public async Task InProcessSynchronousAndAsyncOperationsShareTheRenameContract(){
        using var host=InProcessManagementHost.Create(Root());
        host.Invoke((service,owner)=>NativeWorkflow.Publish(service,owner,Designer.Empty("db","Before")));
        Assert.Equal("After",host.RenameDatabase(Request()).Name);
        Assert.Equal("Final",(await host.RenameDatabaseAsync(new("db:db","After","Final",Guid.NewGuid()))).Name);
        Assert.Equal("Final",Assert.Single(host.Databases().Items,item=>!item.IsSystem).Name);
    }
    [Fact]public void EnablingUpdateAuditPreservesTheCurrentNameOfTheDatabaseAndQueries(){
        var root=Root();
        using(var store=ManagementStore.InitializeLegacy(root,"admin","TestPassword-123"))using(var service=new Service(store,null,startAuditWorker:false)){
            NativeWorkflow.Publish(service,Admin(),NativeWorkflow.Database());
            var query=new Definition("sum","Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","using System.Linq;using Seamless.Management.Server;namespace Model;public static class Query{public static int Run(ManagementExecutionContext context)=>context.GetDatabase<Db>().Rows.Sum(row=>row.Value);}")],[]));
            NativeWorkflow.Publish(service,Admin(),query);service.RenameDatabase(Admin(),new("db:db","Native","Renamed",Guid.NewGuid()));
        }
        Seamless.Management.Server.ManagementStoreUpgrade.Upgrade(root,root+"-system-backup");
        var result=Seamless.Management.Server.UpdateAuditUpgrade.Enable(root,"Renamed",root+"-audit-backup");Assert.Equal(OperationState.Succeeded,result.State);
        using(var store=new ManagementStore(root))using(var service=new Service(store,null,startAuditWorker:false)){
            Assert.Equal("Renamed",service.GetDefinition(Admin(),"db").Database);Assert.Equal("Renamed",service.GetDefinition(Admin(),"sum").Database);
            Assert.Equal(0,Assert.Single(service.Execute(Admin(),new("sum",null,ManagementJson.Element(new{})),CancellationToken.None)).Value.GetInt32());
        }
    }
    [Fact]public async Task F2OpensTheDatabaseNameInputAndKeepsItEditableAfterAConflict(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Service(store,null,startAuditWorker:false);Add(store);Add(store,"other","Taken");
        await using var operations=new ConcurrentManagementOperations(service);await using var component=new 管理ワークスペース();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        component.GetType().GetProperty("管理操作",flags)!.SetValue(component,operations);
        component.GetType().GetProperty("利用者",flags)!.SetValue(component,new UserInfo("admin",[new("*",ManagementPermission.All)],false));
        Task Invoke(string method,params object?[] values)=>(Task)component.GetType().GetMethod(method,flags)!.Invoke(component,values)!;
        object? Field(string name)=>component.GetType().GetField(name,flags)!.GetValue(component);
        await Invoke("OnInitializedAsync");
        var database=service.Databases(Admin()).Items.Single(item=>item.Id=="db:db");
        var row=new 対象操作ホスト();
        row.GetType().GetProperty("名前変更時")!.SetValue(row,Microsoft.AspNetCore.Components.EventCallback.Factory.Create(new object(),
            ()=>component.GetType().GetMethod("DB名前変更開始時",flags)!.Invoke(component,[database])));
        await(Task)row.GetType().GetMethod("キー押下時Async",flags)!.Invoke(row,[new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs{Key="F2"}])!;
        Assert.Equal("db:db",Field("DB名前変更ID"));
        component.GetType().GetMethod("DB名前変更取消時",flags)!.Invoke(component,null);Assert.Null(Field("DB名前変更ID"));
        component.GetType().GetMethod("DB名前変更開始時",flags)!.Invoke(component,[database]);
        await Invoke("DB名前変更確定時Async","Taken");Assert.Equal("db:db",Field("DB名前変更ID"));Assert.True((bool)Field("エラーか")!);
        await Invoke("DB名前変更確定時Async","After");Assert.Null(Field("DB名前変更ID"));
        var catalog=Assert.Single((CatalogDatabase[])Field("DB一覧")!,item=>item.Id=="db:db");
        Assert.Equal("Before",catalog.Name);
        Assert.Equal("After",component.GetType().GetMethod("DB表示名",flags)!.Invoke(component,[catalog]));
        var documents=(Dictionary<string,Document>)Field("文書一覧")!;
        Assert.Equal(new DatabaseDraftChange("Before","After"),documents["db:db"].DB変更);
        Assert.Equal("Before",Assert.Single(service.Databases(Admin()).Items,item=>item.Id=="db:db").Name);
    }
}
