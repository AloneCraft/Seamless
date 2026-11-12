using System.Text.Json;
using Seamless.Database;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed class BatchSchema{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
    [Fact]public void DestructiveChangeRequiresBackupAndFailureKeepsPublishedVersion(){
        var root=Root();using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);var original=NativeWorkflow.Database();NativeWorkflow.Publish(service,user,original);
        var design=Seamless.Management.DatabaseDesigner.Read(original,"Rows");var changed=Seamless.Management.DatabaseDesigner.Apply(original,design with{Columns=design.Columns.Where(column=>column.Name!="Value").ToArray()});
        var draft=service.SaveDraft(user,new(changed,1,1));_ = service.Build(user,new("db",draft.Revision));Assert.Equal(1,service.GetDefinition(user,"db").Version);
        var preview=service.Preview(user,new("db",draft.Revision));Assert.True(preview.CanApply);Assert.True(preview.RequiresBackup);var id=Guid.NewGuid();
        Assert.Equal("BackupRequired",Assert.Throws<ManagementException>(()=>service.Apply(user,new("db",draft.Revision,preview.Token,id))).Code);
        Directory.CreateDirectory(Path.Combine(root,"backups",id.ToString("N")));var failed=service.Apply(user,new("db",draft.Revision,preview.Token,id,true));Assert.Equal(OperationState.Failed,failed.State);Assert.Equal(1,service.GetDefinition(user,"db").Version);
        var success=service.Apply(user,new("db",draft.Revision,preview.Token,Guid.NewGuid(),true));Assert.Equal(OperationState.Succeeded,success.State);Assert.Equal(2,success.PublishedVersion);Assert.True(File.Exists(Path.Combine(success.BackupPath!,"definition-version.json")));
        var replay=service.Apply(user,new("db",draft.Revision,preview.Token,success.Id,true));Assert.Equal(success,replay);
    }
    [Fact]public void ValidationSeedBatchSupportsCyclesAndRejectsOrphans(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");using var service=new Seamless.Management.Server.ManagementService(store);var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
        var definition=new Definition("relations","Relations",DefinitionKind.Database,"Relations.Db","",new("1",[new("relations.cs",NativeRelations.Source)],[]));var draft=service.SaveDraft(user,new(definition,0,0));var validation=service.CreateValidationDraft(user,new("relations",draft.Revision));
        service.SeedBatch(user,new(validation.Id,[new("As",[ManagementJson.Element(new{Id=1,Other=2})]),new("Bs",[ManagementJson.Element(new{Id=2,Other=1})])]));
        Assert.Throws<ForeignKeyViolationException>(()=>service.SeedBatch(user,new(validation.Id,[new("As",[ManagementJson.Element(new{Id=3,Other=99})])])));
        service.SeedBatch(user,new(validation.Id,[new("As",[ManagementJson.Element(new{Id=3,Other=4})]),new("Bs",[ManagementJson.Element(new{Id=4,Other=3})])]));
        var preview=service.Preview(user,new("relations",draft.Revision));Assert.True(preview.CanApply);NativeWorkflow.Publish(service,user,definition,1,0);
        var catalog=service.Databases(user).Items.Single(item=>!item.IsSystem);var details=service.ObjectDetails(user,new(catalog.Id,ObjectId:"As"));Assert.Equal("Bs",Assert.Single(details.ForeignKeys!).ReferencedTable);
    }
    [Fact]public void OptionalMigrationFieldsPreserveExistingHashesAndRequests(){
        var definition=NativeWorkflow.Database();var legacy=new{definition.Id,definition.Database,definition.Kind,definition.EntryType,definition.EntryMember,definition.Source,definition.WritesData};var bytes=JsonSerializer.SerializeToUtf8Bytes(legacy,ManagementJson.Options);Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),definition.Hash());
        var request=new ApplyRequest("db",1,"token",Guid.NewGuid());var old=new{request.Id,request.DraftRevision,request.PreviewToken,request.OperationId};Assert.Equal(JsonSerializer.Serialize(old,ManagementJson.Options),JsonSerializer.Serialize(request,ManagementJson.Options));
    }
}
