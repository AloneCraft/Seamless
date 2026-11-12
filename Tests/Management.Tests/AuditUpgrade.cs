using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class AuditUpgrade{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"audit-upgrade-"+Guid.NewGuid().ToString("N"));
    [Fact]public void UpgradePreservesInstanceUsersChunkedDataAndHistory(){
        var root=Root();var backup=root+"-backup";Guid instance;var text=new string('x',150000);
        using(var store=ManagementStore.InitializeLegacy(root,"admin","TestPassword-123")){instance=store.InstanceId;store.Write(()=>{store.Put("probe","source",new SourceFile("Definition.cs",text));store.AddHistory("admin","sales","DraftSaved","d1",7);});}
        ManagementStoreUpgrade.Upgrade(root,backup);
        using(var store=new ManagementStore(root))using(var service=new Seamless.Management.Server.ManagementService(store)){Assert.Equal(5,store.Format);Assert.Equal(instance,store.InstanceId);Assert.Equal(text,store.Read<SourceFile>("probe","source")!.Text);Assert.Equal("admin",service.Login(new("admin","TestPassword-123")).User.Name);Assert.Contains(store.AuditEvents(new()).Items,item=>item.ResultCode=="DraftSaved");}
        using var original=new ManagementStore(backup);Assert.Equal(3,original.Format);Assert.Equal(instance,original.InstanceId);Assert.Equal(text,original.Read<SourceFile>("probe","source")!.Text);
    }
    [Fact]public void LiveServerAndFailedBackupDoNotChangeTheSource(){
        var root=Root();using(var store=ManagementStore.InitializeLegacy(root,"admin","TestPassword-123")){Assert.Throws<IOException>(()=>ManagementStoreUpgrade.Upgrade(root,root+"-backup"));Assert.Equal(3,store.Format);}
        var backup=root+"-invalid";File.WriteAllText(backup,"existing file");Assert.Throws<IOException>(()=>ManagementStoreUpgrade.Upgrade(root,backup));using var reopened=new ManagementStore(root);Assert.Equal(3,reopened.Format);
    }
    [Fact]public void UpgradeKeepsPublishedDatabaseDataDraftsAndDisabledAccounts(){
        var root=Root();Draft draft;DatabaseGrant[] grants=[new("Native",ManagementPermission.View|ManagementPermission.Execute)];
        using(var store=ManagementStore.InitializeLegacy(root,"admin","TestPassword-123"))using(var service=new Seamless.Management.Server.ManagementService(store)){
            var user=service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);NativeWorkflow.Publish(service,user,NativeWorkflow.Database());
            Definition Query(string id,string body,bool writes=false)=>new(id,"Native",DefinitionKind.Query,"Model.Query","Run",new("1",[new("query.cs","using System.Linq;using Seamless.Management.Server;namespace Model;public static class Query{public static int Run(ManagementExecutionContext context){"+body+"}}")],[]),writes);
            NativeWorkflow.Publish(service,user,Query("insert","context.GetDatabase<Db>().Rows.Add(new Records.Row(1,9));return 9;",true));NativeWorkflow.Publish(service,user,Query("sum","return context.GetDatabase<Db>().Rows.Sum(row=>row.Value);"));
            Assert.Equal(9,Assert.Single(service.Execute(user,new("insert",null,ManagementJson.Element(new{})),CancellationToken.None)).Value.GetInt32());
            draft=service.SaveDraft(user,new(NativeWorkflow.Database() with{Source=NativeWorkflow.Database().Source with{Revision="unapplied"}},1,1));service.ChangeUser(user,new("disabled","DisabledPassword-123",grants,true));
        }
        ManagementStoreUpgrade.Upgrade(root,root+"-backup");
        using var reopened=new ManagementStore(root);using var current=new Seamless.Management.Server.ManagementService(reopened);var admin=current.Authenticate(current.Login(new("admin","TestPassword-123")).Token);
        Assert.Equal(9,Assert.Single(current.Execute(admin,new("sum",null,ManagementJson.Element(new{})),CancellationToken.None)).Value.GetInt32());var saved=current.GetDraft(admin,"db");Assert.Equal(draft.Revision,saved.Revision);Assert.Equal(draft.Definition.Hash(),saved.Definition.Hash());Assert.Equal(1,current.GetDefinition(admin,"db").Version);
        var disabled=reopened.Read<UserAccount>("user","DISABLED")!;Assert.True(disabled.Disabled);Assert.Equal(grants,disabled.Grants);Assert.Throws<ManagementException>(()=>current.Login(new("disabled","DisabledPassword-123")));
    }
    [Fact]public void AFailedSwitchRestoresTheOldDatabase(){
        var root=Root();Guid instance;using(var store=ManagementStore.InitializeLegacy(root,"admin","TestPassword-123")){instance=store.InstanceId;}
        ManagementStoreUpgrade.Checkpoint=phase=>{if(phase=="OldMoved"){throw new IOException("injected failure");}};
        try{Assert.Throws<IOException>(()=>ManagementStoreUpgrade.Upgrade(root,root+"-backup"));}finally{ManagementStoreUpgrade.Checkpoint=null;}
        using var reopened=new ManagementStore(root);Assert.Equal(3,reopened.Format);Assert.Equal(instance,reopened.InstanceId);Assert.False(File.Exists(Path.Combine(root,"management.audit-upgrade.json")));
    }
}
