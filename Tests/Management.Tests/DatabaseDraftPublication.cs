using Seamless.Management;
using Seamless.Management.Server;
using HostService=Seamless.Management.Server.ManagementService;

namespace Management.Tests;

public sealed class DatabaseDraftPublication{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
    private static UserAccount Admin(HostService service)=>service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
    private static ApplyRequest Request(ChangePreview preview,bool confirm=false)=>new(preview.Id,preview.DraftRevision,preview.Token,Guid.NewGuid(),confirm);

    [Fact]public async Task PrivateNewDatabaseDraftSurvivesRestartAndPublishesItsFirstTableTogether(){
        var root=Root();
        var definition=NativeWorkflow.Database();
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new HostService(store)){
            var user=Admin(service);
            service.ChangeUser(user,new("other","TestPassword-123",[new("*",ManagementPermission.All)]));
            var other=service.Authenticate(service.Login(new("other","TestPassword-123")).Token);
            var draft=service.SaveDraft(user,new(definition,0,0,0){DatabaseChange=new(null)});
            Assert.Equal(0,draft.BaseVersion);
            Assert.Empty(service.UnpublishedDatabases(other).Items);
            Assert.DoesNotContain(service.Databases(user).Items,item=>!item.IsSystem);
            Assert.False(Directory.Exists(Path.Combine(root,"generations")));
        }
        using(var store=new ManagementStore(root))
        using(var service=new HostService(store)){
            var user=Admin(service);
            Assert.Equal(definition.Id,Assert.Single(service.UnpublishedDatabases(user).Items).Definition.Id);
            var draft=service.GetDraft(user,definition.Id);
            var preview=service.Preview(user,new(definition.Id,draft.Revision));
            Assert.True(preview.CanApply);
            var request=Request(preview);
            var applied=service.Apply(user,request);
            Assert.Equal(OperationState.Succeeded,applied.State);
            Assert.Equal(1,applied.PublishedVersion);
            Assert.Equal(applied,service.Apply(user,request));
            Assert.Empty(service.UnpublishedDatabases(user).Items);
            Assert.Equal(1,(await service.ReadRowsAsync(user,new("Native","Rows"))).Version);
        }
    }

    [Fact]public async Task RenameAndSchemaPublishAtomicallyAndKeepTheDraftAfterFailure(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");
        using var service=new HostService(store);var user=Admin(service);
        var definition=NativeWorkflow.Database();
        NativeWorkflow.Publish(service,user,definition);
        await service.CommitRowsAsync(user,new("Native",1,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=42}))]));
        service.ChangeUser(user,new("reader","TestPassword-123",[new("Native",ManagementPermission.View|ManagementPermission.ReadRows)]));
        var source=definition.Source with{Revision="2",Files=definition.Source.Files.Select(file=>file with{
            Text=file.Text.Replace("public Table<Keys.Row,Tables.Row> Rows=>","public Table<Keys.Row,Tables.Row> Other=>this.Table<Keys.Row,Tables.Row>(row=>new(row.Id)); public Table<Keys.Row,Tables.Row> Rows=>",StringComparison.Ordinal)
        }).ToArray()};
        var draft=service.SaveDraft(user,new(definition with{Source=source},1,1,1){DatabaseChange=new("Native","Renamed")});
        var preview=service.Preview(user,new(definition.Id,draft.Revision));Assert.True(preview.CanApply);
        Assert.Contains(preview.Changes,change=>change.Contains("Native → Renamed",StringComparison.Ordinal));
        Assert.Equal("Native",Assert.Single(service.Databases(user).Items,item=>!item.IsSystem).Name);
        service.RenameCheckpoint=()=>throw new IOException("publication interrupted");
        var failed=service.Apply(user,Request(preview));
        Assert.Equal(OperationState.Failed,failed.State);
        Assert.Null(failed.PublishedVersion);
        Assert.Equal("Native",Assert.Single(service.Databases(user).Items,item=>!item.IsSystem).Name);
        Assert.Equal(1,service.GetDefinition(user,definition.Id).Version);
        Assert.Equal("Renamed",service.GetDraft(user,definition.Id).DatabaseChange?.NewName);
        service.RenameCheckpoint=null;
        Assert.Equal(OperationState.Succeeded,service.Apply(user,Request(preview)).State);
        var reader=service.Authenticate(service.Login(new("reader","TestPassword-123")).Token);
        Assert.Equal("Renamed",Assert.Single(service.Databases(reader).Items,item=>!item.IsSystem).Name);
        Assert.Equal(42,Assert.Single((await service.ReadRowsAsync(reader,new("Renamed","Rows"))).Rows).Values.GetProperty("value").GetInt32());
        Assert.Empty((await service.ReadRowsAsync(reader,new("Renamed","Other"))).Rows);
    }

    [Fact]public async Task DeletionRequiresConfirmationAndACompleteBackupAndCanBeRestored(){
        var root=Root();var definition=NativeWorkflow.Database();Guid deletion;
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new HostService(store)){
            var admin=Admin(service);NativeWorkflow.Publish(service,admin,definition);
            await service.CommitRowsAsync(admin,new("Native",1,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=81}))]));
            service.ChangeUser(admin,new("operator","TestPassword-123",[new("Native",ManagementPermission.All)]));
            var user=service.Authenticate(service.Login(new("operator","TestPassword-123")).Token);
            var draft=service.SaveDraft(user,new(definition,0,1,1){DatabaseChange=new("Native",Delete:true)});
            draft=service.SaveDraft(user,new(definition,draft.Revision,1));
            Assert.True(draft.DatabaseChange?.Delete);
            draft=service.SaveDraft(user,new(definition,draft.Revision,1){ClearDatabaseChange=true});
            Assert.Null(draft.DatabaseChange);
            draft=service.SaveDraft(user,new(definition,draft.Revision,1){DatabaseChange=new("Native",Delete:true)});
            var preview=service.Preview(user,new(definition.Id,draft.Revision));
            Assert.True(preview.CanApply);Assert.True(preview.RequiresBackup);
            Assert.Equal("BackupRequired",Assert.Throws<ManagementException>(()=>service.Apply(user,Request(preview))).Code);
            var blocked=Request(preview,true);
            Directory.CreateDirectory(Path.Combine(root,"backups",blocked.OperationId.ToString("N")));
            Assert.Equal(OperationState.Failed,service.Apply(user,blocked).State);
            Assert.NotNull(store.FindDatabase("Native"));
            var request=Request(preview,true);deletion=request.OperationId;
            var deleted=service.Apply(user,request);
            Assert.Equal(OperationState.Succeeded,deleted.State);
            var refreshedUser=service.Authenticate(service.Login(new("operator","TestPassword-123")).Token);
            Assert.Empty(refreshedUser.Grants);
            Assert.Equal(deleted,service.Apply(refreshedUser,request));
            Assert.Equal(deleted,service.GetOperation(refreshedUser,deletion));
            Assert.True(File.Exists(Path.Combine(deleted.BackupPath!,"database","definition-version.json")));
            Assert.DoesNotContain(service.Databases(user).Items,item=>!item.IsSystem);
            Assert.Empty(service.UnpublishedDatabases(user).Items);
        }
        var restored=RestoreDeletedDatabase.Run(root,deletion,CancellationToken.None);
        Assert.Equal(definition.Id,restored.Definition.Id);Assert.Equal(2,restored.Version);
        using(var store=new ManagementStore(root))
        using(var service=new HostService(store)){
            var user=service.Authenticate(service.Login(new("operator","TestPassword-123")).Token);
            var page=await service.ReadRowsAsync(user,new("Native","Rows"));
            Assert.Equal(81,Assert.Single(page.Rows).Values.GetProperty("value").GetInt32());
        }
    }
}
