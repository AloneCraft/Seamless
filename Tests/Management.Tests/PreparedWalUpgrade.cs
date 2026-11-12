using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class PreparedWalUpgrade{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"pw-"+Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UpgradePreservesInstanceRowsDraftChunksAuditAndAccounts(){
        var root=Root();Guid instance;Draft draft;var text=new string('x',200_000);
        var user=new UserAccount("admin","",[new("*",ManagementPermission.All)]);
        var at=new DateTimeOffset(2026,11,9,0,0,0,TimeSpan.Zero);
        var audit=new AuditEvent(Guid.NewGuid(),null,at,at,Guid.NewGuid(),"user","Native","db",AuditAction.Read,AuditOutcome.Succeeded,null,null,null,"Preserved");
        var summary=new AuditReadKey(at,at.AddMinutes(1),audit.AuthorityId,"user","Native","query","code",null);
        using(var store=ManagementStore.InitializeReadAuditLegacy(root,"admin","TestPassword-123"))
        using(var service=new Service(store,null,startAuditWorker:false)){
            instance=store.InstanceId;
            NativeWorkflow.Publish(service,user,NativeWorkflow.Database());
            await service.CommitRowsAsync(user,new("Native",1,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=9}))]));
            var definition=NativeWorkflow.Database();draft=service.SaveDraft(user,new(definition with{Source=definition.Source with{Revision="pending"}},1,1));
            service.ChangeUser(user,new("disabled","DisabledPassword-123",[new("Native",ManagementPermission.View)],true));
            store.Write(()=>store.Put("probe","source",new SourceFile("keep.cs",text)));
            store.AppendAudit(audit);store.ApplyReadSummaryBatch(Guid.NewGuid(),[new(summary,7)]);
        }
        ManagementStoreUpgrade.UpgradePreparedWal(root,root+"-backup");
        using(var store=new ManagementStore(root))
        using(var service=new Service(store,null,startAuditWorker:false)){
            Assert.Equal(6,store.Format);Assert.Equal(instance,store.InstanceId);
            Assert.Equal(text,store.Read<SourceFile>("probe","source")!.Text);
            Assert.Equal("admin",service.Login(new("admin","TestPassword-123")).User.Name);
            Assert.True(store.Read<UserAccount>("user","DISABLED")!.Disabled);
            Assert.Equal(draft.Definition.Hash(),service.GetDraft(user,"db").Definition.Hash());
            Assert.Equal(draft.Revision,service.GetDraft(user,"db").Revision);
            Assert.Equal(9,Assert.Single((await service.ReadRowsAsync(user,new("Native","Rows"))).Rows).Values.GetProperty("value").GetInt32());
            Assert.Contains(store.AuditEvents(new()).Items,item=>item.EventId==audit.EventId);
            Assert.Equal(7,store.ReadSummary(summary)!.Count);
        }
        using var backup=new ManagementStore(root+"-backup");
        Assert.Equal(5,backup.Format);Assert.Equal(instance,backup.InstanceId);
        Assert.Equal(text,backup.Read<SourceFile>("probe","source")!.Text);
    }

    [Theory]
    [InlineData("Marked")]
    [InlineData("OldMoved")]
    [InlineData("NewInstalled")]
    [InlineData("Committed")]
    public void AFailedSwitchRestoresTheSingleFrameStore(string phase){
        var root=Root();Guid instance;
        using(var store=ManagementStore.InitializeReadAuditLegacy(root,"admin","TestPassword-123")){
            instance=store.InstanceId;store.Write(()=>store.Put("probe","value",new SourceFile("keep.cs","preserved")));
        }
        ManagementStoreUpgrade.Checkpoint=current=>{if(current==phase){throw new IOException("injected switch failure");}};
        try{Assert.Throws<IOException>(()=>ManagementStoreUpgrade.UpgradePreparedWal(root,root+"-backup"));}
        finally{ManagementStoreUpgrade.Checkpoint=null;}
        using var restored=new ManagementStore(root);
        Assert.Equal(5,restored.Format);Assert.Equal(instance,restored.InstanceId);
        Assert.Equal("preserved",restored.Read<SourceFile>("probe","value")!.Text);
        Assert.False(File.Exists(Path.Combine(root,"management.prepared-wal-upgrade.json")));
    }

    [Fact]
    public void CancellationBeforeSwitchAndALiveStoreLeaveTheSourceUsable(){
        var root=Root();
        using(var store=ManagementStore.InitializeReadAuditLegacy(root,"admin","TestPassword-123")){
            Assert.Throws<IOException>(()=>ManagementStoreUpgrade.UpgradePreparedWal(root,root+"-live-backup"));
        }
        using var cancellation=new CancellationTokenSource();
        ManagementStoreUpgrade.Checkpoint=phase=>{if(phase=="Staged"){cancellation.Cancel();}};
        try{Assert.Throws<OperationCanceledException>(()=>ManagementStoreUpgrade.UpgradePreparedWal(root,root+"-backup",cancellation.Token));}
        finally{ManagementStoreUpgrade.Checkpoint=null;}
        using var restored=new ManagementStore(root);Assert.Equal(5,restored.Format);
        Assert.False(File.Exists(Path.Combine(root,"management.prepared-wal-upgrade.json")));
    }
}
