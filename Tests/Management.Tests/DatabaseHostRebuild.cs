using Seamless.Management;
using Seamless.Management.Server;
using HostService=Seamless.Management.Server.ManagementService;

namespace Management.Tests;

public sealed class DatabaseHostRebuild{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReopenChecksAllRowsBeforePublishing(bool damageTransfer){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        var definition=NativeWorkflow.Database();
        DefinitionVersion original;
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new HostService(store)){
            var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
            NativeWorkflow.Publish(service,user,definition);
            await service.CommitRowsAsync(user,new("Native",1,Guid.NewGuid(),[
                new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=2,Value=9})),
                new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=73}))]));
            original=service.GetDefinition(user,"db");
        }
        var transfer=Path.Combine(root,"verification-rows.bin");
        var receipt=EmptyDatabaseRebuild.Verify(root,definition.Id,original.Version,original.ArtifactHash,transfer);
        if(damageTransfer){
            using var file=File.OpenWrite(transfer);
            file.SetLength(file.Length-1);
        }
        using(var store=new ManagementStore(root))
        using(var runtime=new NativeRuntime(store,startAuditWorker:false))
        using(var rows=new HostRebuildRows(transfer,receipt)){
            var checkedAfterReopen=false;
            void Publish()=>runtime.Publish(definition,CancellationToken.None,publication=>{
                Assert.True(checkedAfterReopen);
                var candidate=publication.Primary;
                store.Write(()=>{
                    if(candidate.AuditOrigin is{} origin){store.Put("auditOrigin",origin.WalId.ToString("N"),origin);}
                    store.Put("active",definition.Id,original with{Version=2,ArtifactHash=candidate.ArtifactHash,StoragePath=candidate.StoragePath});
                });
            },fill:rows.Import,verifyReopened:(candidate,token)=>{
                rows.Verify(candidate,token);
                checkedAfterReopen=true;
            });
            if(damageTransfer){Assert.Throws<InvalidDataException>(Publish);Assert.False(checkedAfterReopen);}
            else{Publish();Assert.True(checkedAfterReopen);}
        }
        using(var store=new ManagementStore(root))
        using(var service=new HostService(store)){
            var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
            var reopened=await service.ReadRowsAsync(user,new("Native","Rows"));
            Assert.Equal(damageTransfer?1:2,reopened.Version);
            Assert.Equal(new[]{(1,73),(2,9)},reopened.Rows
                .Select(row=>(Id:row.Key.GetProperty("id").GetInt32(),Value:row.Values.GetProperty("value").GetInt32())).OrderBy(row=>row.Id));
        }
    }
}
