using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection(SourceBuildBudgetCollection.Name)]
public sealed class ManagementSystemViewsTests{
    [Fact]
    public async Task SavedLegacyDraftIsMaterializedWithoutChangingItsOwnerOrRevision(){
        var work=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"legacy-draft-"+Guid.NewGuid().ToString("N"));
        var definition=global::Seamless.Management.DatabaseDesigner.EmptyManagedDatabase("legacy","Legacy");
        var draft=new Draft(definition,"admin",4,0,DateTimeOffset.UtcNow){DatabaseChange=new(null)};
        using(var store=ManagementStore.Initialize(work,"admin","TestPassword-123")){
            store.Write(()=>store.Put("draft",ManagementStore.Normalize(draft.Owner)+":"+definition.Id,draft));
        }
        Assert.False(Directory.Exists(Path.Combine(work,"drafts")));
        using var reopened=new ManagementStore(work);
        using var service=new global::Seamless.Management.Server.ManagementService(reopened);
        var admin=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        var view=await service.ReadRowsAsync(admin,new("system","Draft"));
        var row=Assert.Single(view.Rows).Values;
        Assert.Equal("admin",row.GetProperty("owner").GetString());
        Assert.Equal(4,row.GetProperty("draftRevision").GetInt64());
        Assert.Single(Directory.GetFiles(Path.Combine(work,"drafts"),"*.json"));
        Assert.Equal(draft.Definition.Hash(),service.GetDraft(admin,definition.Id).Definition.Hash());
    }

    [Fact]
    public async Task LinkedDatabasePublishesAndRestoresARecordProperty(){
        var suffix="system-views-"+Guid.NewGuid().ToString("N");
        var work=Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,suffix));
        var external=Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TABLE_STORAGE_TEST_ROOT")??work,suffix));
        var root=Path.Combine(work,"management");
        var backup=Path.Combine(work,"backup");
        var restored=Path.Combine(work,"restored");
        var databasePath=Path.Combine(external,"Reports");
        Directory.CreateDirectory(databasePath);
        var definition=global::Seamless.Management.DatabaseDesigner.EmptyManagedDatabase("reports","Reports");
        definition=global::Seamless.Management.DatabaseDesigner.Apply(definition,new("Rows","",[new("Id","int",true)],[]));
        definition=global::Seamless.Management.DatabaseDesigner.ApplyQuery(definition,new("Info","",DatabaseQueryKind.Property,[new("Count","int",false)],[],"int","return new(42);"));
        var item=Assert.Single(global::Seamless.Management.DatabaseDesigner.Objects(definition),row=>row.Kind=="Property");
        var design=global::Seamless.Management.DatabaseDesigner.ReadObjectDefinition(definition,item);
        var column=Assert.Single(design.Columns);
        var edited=global::Seamless.Management.DatabaseDesigner.ApplyObjectStructure(definition,design,[column with{Type="long"}],[],design.ReturnType);
        Assert.True(edited.Succeeded,string.Join("\n",edited.Diagnostics.Select(error=>error.Message)));
        definition=edited.Definition!;
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new global::Seamless.Management.Server.ManagementService(store){UseSdkBuilds=true}){
            var admin=Admin(service);
            var drafts=await service.ReadRowsAsync(admin,new("system","Draft"));
            Assert.Empty(drafts.Rows);
            var draft=service.SaveDraft(admin,new(definition,0,0){DatabaseChange=new(null)});
            Assert.Single((await service.ReadRowsAsync(admin,new("system","Draft"))).Rows);
            Assert.Single(Directory.GetFiles(Path.Combine(root,"drafts"),"*.json"));
            Directory.CreateDirectory(Path.Combine(root,"generations"));
            ManagementPlacement.CreateDirectoryLink(Path.Combine(root,"generations","Reports"),databasePath);
            var preview=service.Preview(admin,new(definition.Id,draft.Revision));
            Assert.True(preview.CanApply,string.Join("\n",preview.Diagnostics.Select(error=>error.Message)));
            var applied=service.Apply(admin,new(definition.Id,draft.Revision,preview.Token,Guid.NewGuid()){BundleHash=preview.BundleHash});
            Assert.True(applied.State==OperationState.Succeeded,applied.Error);
            Assert.Empty((await service.ReadRowsAsync(admin,new("system","Draft"))).Rows);
            await service.CommitRowsAsync(admin,new("Reports",1,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=9}))]));
        }
        await Verify(root);
        Program.CopyOffline(root,backup);
        Directory.Move(databasePath,databasePath+"-offline");
        Program.CopyOffline(backup,restored);
        await Verify(restored);
        Directory.Move(databasePath+"-offline",databasePath);

        static UserAccount Admin(global::Seamless.Management.Server.ManagementService service)=>service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        static async Task Verify(string path){
            using var store=new ManagementStore(path);
            using var service=new global::Seamless.Management.Server.ManagementService(store);
            var admin=Admin(service);
            Assert.Single((await service.ReadRowsAsync(admin,new("system","Databases"))).Rows);
            Assert.NotEmpty((await service.ReadRowsAsync(admin,new("system","StorageLocation"))).Rows);
            var objects=await service.ReadRowsAsync(admin,new("Reports","DatabaseObjects"));
            Assert.Contains(objects.Rows,row=>row.Values.GetProperty("kind").GetString()=="Property");
            var value=await service.ExecuteObjectAsync(admin,new("Reports","Info",[]));
            Assert.Equal(42L,Assert.Single(value.Rows).GetProperty("count").GetInt64());
            Assert.Equal(9,Assert.Single((await service.ReadRowsAsync(admin,new("Reports","Rows"))).Rows).Values.GetProperty("id").GetInt32());
        }
    }
}
