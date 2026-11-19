using Seamless.Management;
using Seamless.Management.Server;
using Designer=Seamless.Management.DatabaseDesigner;
using HostService=Seamless.Management.Server.ManagementService;

namespace Management.Tests;

public sealed class DatabaseDraftPublication{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
    private static UserAccount Admin(HostService service)=>service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
    private static ApplyRequest Request(ChangePreview preview,bool confirm=false)=>new(preview.Id,preview.DraftRevision,preview.Token,Guid.NewGuid(),confirm);

    [Fact]public void QueryRenameHistoryIsPrivateToTheDraftAndClearsOnPublication(){
        var root=Root();
        var original=Designer.ApplyQuery(Designer.Empty("query-draft","QueryDraft"),new("ById","",DatabaseQueryKind.ScalarFunction,[],[new("id","int")],"int","return default;"));
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new HostService(store)){
            var owner=Admin(service);
            NativeWorkflow.Publish(service,owner,original);
            var edited=Designer.ApplyQuery(Designer.RemoveQuery(original,"ById"),new("ByKey","",DatabaseQueryKind.ScalarFunction,[],[new("key","long")],"int","return default;"));
            var state=new Seamless.Management.Components.文書状態(new("db:query-draft","QueryDraft","query-draft",1,"",false),
                service.GetDefinition(owner,original.Id),null);
            state.定義=edited;
            state.問い合わせ改名を記録("ById","ByKey");
            var current=Assert.Single(state.オブジェクト);
            state.問い合わせ列の改名を記録(current,true,"id","key");
            var saved=service.SaveDraft(owner,new(edited,1,1,1){QueryRenames=state.保管する問い合わせ改名});
            Assert.Equal(2,saved.QueryRenames!.Length);
            Assert.Equal(1,service.GetDefinition(owner,original.Id).Version);
        }
        using(var store=new ManagementStore(root))
        using(var service=new HostService(store)){
            var owner=Admin(service);
            var draft=service.FindDraft(owner,original.Id)!;
            var published=service.GetDefinition(owner,original.Id);
            var state=new Seamless.Management.Components.文書状態(new("db:query-draft","QueryDraft","query-draft",1,"",false),published,draft);
            var current=Assert.Single(state.オブジェクト);
            Assert.Equal("ById",state.公開時の項目(current)?.Name);
            Assert.Equal("id",state.公開時の問い合わせ列(current,true,"key"));
            Assert.Empty(state.削除したオブジェクト());
            var preview=service.Preview(owner,new(original.Id,draft.Revision));
            Assert.True(preview.CanApply);
            Assert.Equal(OperationState.Succeeded,service.Apply(owner,Request(preview)).State);
            Assert.Null(service.FindDraft(owner,original.Id)?.QueryRenames);
        }
    }

    [Fact]public void ReopenedTreeUsesThePublishedNameForRenamedAndDeletedTablesUntilApply(){
        var published=Designer.Empty("db","DraftTree");
        published=Designer.Apply(published,new("First","",[new("Id","int",true)],[]));
        published=Designer.Apply(published,new("Second","",[new("Id","int",true)],[])) with{Migration=null};
        var renamed=Designer.Apply(published,Designer.Read(published,"First") with{Name="Renamed",OriginalPath="First"});
        var edited=Designer.Remove(renamed,"Second");
        var document=new Seamless.Management.Components.文書状態(new("db:db","DraftTree","db",1,"",false),
            new(published,1,"owner",DateTimeOffset.UtcNow),new(edited,"owner",1,1,DateTimeOffset.UtcNow));
        var row=Assert.Single(document.オブジェクト);
        Assert.Equal("First",document.公開時の項目(row)?.Name);
        Assert.Equal("Second",Assert.Single(document.削除したオブジェクト()).Name);
        document.公開完了(2);
        Assert.Equal("Renamed",document.公開時の項目(row)?.Name);
        Assert.Empty(document.削除したオブジェクト());
    }

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
            Assert.Equal(definition.Source.Files.Select(file=>new SourceFileChange(file.Path,null,file.Text)),preview.SourceChanges);
            Assert.DoesNotContain(preview.Changes,change=>change.Contains(definition.Source.Files[0].Text,StringComparison.Ordinal));
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
        var changedSource=Assert.Single(preview.SourceChanges);
        Assert.Equal(definition.Source.Files.Single(file=>file.Path==changedSource.Path).Text,changedSource.Before);
        Assert.Equal(source.Files.Single(file=>file.Path==changedSource.Path).Text,changedSource.After);
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
            var backup=Assert.Single(service.RecoveryGuide(admin,RecoveryScenario.RestoreFromBackup).Backups);
            Assert.Equal(new RecoveryBackupLocation("Native",deleted.StartedAt,deleted.BackupPath!),backup);
            Assert.Throws<ManagementException>(()=>service.RecoveryGuide(refreshedUser,RecoveryScenario.RestoreFromBackup));
            Assert.DoesNotContain(service.Databases(user).Items,item=>!item.IsSystem);
            Assert.Empty(service.UnpublishedDatabases(user).Items);
        }
        var restored=RestoreDeletedDatabase.Run(root,deletion,CancellationToken.None);
        Assert.Equal(definition.Id,restored.Definition.Id);Assert.Equal(2,restored.Version);
        using(var store=new ManagementStore(root))
        using(var service=new HostService(store)){
            Assert.Single(service.RecoveryGuide(Admin(service),RecoveryScenario.RestoreFromBackup).Backups);
            var user=service.Authenticate(service.Login(new("operator","TestPassword-123")).Token);
            var page=await service.ReadRowsAsync(user,new("Native","Rows"));
            Assert.Equal(81,Assert.Single(page.Rows).Values.GetProperty("value").GetInt32());
        }
    }
}
