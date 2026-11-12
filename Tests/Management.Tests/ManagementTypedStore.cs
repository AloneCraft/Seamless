using System.Reflection;
using System.Text.Json;
using System.Security.Cryptography;
using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class ManagementTypedStore{
    [Fact]public void ExplicitUpgradePreservesAccountsSettingsCatalogStampsChunksAuditAndBackup(){
        var root=Root();var backup=root+"-backup";Guid instance;string stamp;long catalog;
        var large=new string('x',32767)+"😀"+new string('y',70000);var password=new string('s',100000);
        var legacy=new UserAccount("legacy",password,[new(large,ManagementPermission.View),new("second",ManagementPermission.Execute),null!],true);
        var settings=new StoredServerSettings(9,new(){ManagerPort=23456,ResponseWriteTimeout=TimeSpan.FromSeconds(3),MaximumPendingWriteBytes=1048576,ReadAuditSaveSeconds=0.25});
        using(var source=ManagementStore.Initialize(root,"admin","TestPassword-123"))using(var service=new Service(source,null,startAuditWorker:false)){
            instance=source.InstanceId;stamp=Assert.Single(service.UserProfiles(Admin()).Items).Stamp;
            source.Write(()=>{source.Put("user","LEGACY",legacy);source.Put("user","EMPTY",legacy with{Name="empty",Grants=[]});source.Put("user","NULL",legacy with{Name="null",Grants=null!});source.Put("serverSettings","active",settings);source.Put("probe","unchanged",new SourceFile("keep.cs","preserved"));});catalog=source.CatalogRevision;
        }
        ManagementStoreUpgrade.UpgradeManagedTypes(root,backup);
        using(var target=new ManagementStore(root))using(var service=new Service(target,null,startAuditWorker:false)){
            Assert.Equal(7,target.Format);Assert.Equal(instance,target.InstanceId);Assert.Equal(catalog,target.CatalogRevision);
            Assert.NotNull(service.Login(new("admin","TestPassword-123")));Assert.Equal(stamp,Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(target.Read<UserAccount>("user","ADMIN"),ManagementJson.Options))));
            var restored=target.Read<UserAccount>("user","LEGACY")!;Assert.Equal(legacy.PasswordHash,restored.PasswordHash);Assert.Equal(legacy.Grants,restored.Grants);Assert.True(restored.Disabled);
            Assert.Empty(target.Read<UserAccount>("user","EMPTY")!.Grants);Assert.Null(target.Read<UserAccount>("user","NULL")!.Grants);
            Assert.Equal(settings,target.Read<StoredServerSettings>("serverSettings","active"));Assert.Equal("preserved",target.Read<SourceFile>("probe","unchanged")!.Text);
            var json=target.Read<JsonElement>("user","LEGACY");Assert.Equal(large,json.GetProperty("grants")[0].GetProperty("database").GetString());Assert.Equal(JsonValueKind.Null,json.GetProperty("grants")[2].ValueKind);
            var data=Database(target);Assert.Equal(4,data.Users.Count());Assert.Equal(4,data.UserGrants.Count());Assert.True(data.ManagedTextParts.Any());
            Assert.DoesNotContain(data.Entries,row=>row.Kind is "user" or "serverSettings"||row.Kind=="chunk"&&(row.Subject.StartsWith("user\0",StringComparison.Ordinal)||row.Subject.StartsWith("serverSettings\0",StringComparison.Ordinal)));
            Assert.Contains(service.History(Admin()).Items,entry=>entry.Action=="ManagedTypesUpgraded");Assert.NotEmpty(target.AuditEvents(new()).Items);
        }
        using var original=new ManagementStore(backup);Assert.Equal(6,original.Format);Assert.Equal(legacy.Grants,original.Read<UserAccount>("user","LEGACY")!.Grants);Assert.Equal(settings,original.Read<StoredServerSettings>("serverSettings","active"));
    }
    [Fact]public void TypedUpdatesCopyGrantsPreserveCasAndRollbackUsersSettingsHeaderAndAuditTogether(){
        var root=Root();using var store=ManagementStore.CreateUpgradeStage(root,7);using var service=new Service(store,null,startAuditWorker:false);
        var unsupported=Path.Combine(root,"incomplete.transfer");Assert.Throws<NotSupportedException>(()=>store.ExportRaw(unsupported));Assert.False(File.Exists(unsupported));
        service.AttachServerSettings(new(new(),new(),[],"certificate.pfx",null,14336,"http://127.0.0.1:24503/"));
        var grants=new[]{new DatabaseGrant("first",ManagementPermission.View),new DatabaseGrant("second",ManagementPermission.Execute)};
        var account=new UserAccount("reader","original-hash",grants);store.Write(()=>store.Put("user","READER",account));grants[0]=new("mutated",ManagementPermission.All);
        var read=store.Read<UserAccount>("user","READER")!;Assert.Equal("first",read.Grants[0].Database);read.Grants[0]=grants[0];Assert.Equal("first",store.Read<UserAccount>("user","READER")!.Grants[0].Database);
        var profile=Assert.Single(service.UserProfiles(Admin()).Items);var changed=service.EditUserProfile(Admin(),new(new("reader",null,[new("third",ManagementPermission.View)]),profile.Stamp));
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>service.EditUserProfile(Admin(),new(new("reader",null,[]),profile.Stamp))).Code);
        var saved=service.SaveServerSettings(Admin(),new(0,new(){ManagerPort=12345}));Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>service.SaveServerSettings(Admin(),new(0,new()))).Code);
        var revision=store.Revision;var catalog=store.CatalogRevision;var audit=store.AuditEvents(new()).Items.Length;var history=store.History(0,100,static _=>true).Items.Length;
        Assert.Throws<IOException>(()=>store.Write(()=>{store.Put("user","READER",account with{Grants=[]});store.Put("serverSettings","active",new StoredServerSettings(99,new()));store.AddHistory("admin","*","injected","",0);throw new IOException("before commit");}));
        Assert.Equal(revision,store.Revision);Assert.Equal(catalog,store.CatalogRevision);Assert.Equal(changed.Stamp,Assert.Single(service.UserProfiles(Admin()).Items).Stamp);Assert.Equal(saved.Version,Service.SavedServerSettings(store)!.Version);Assert.Equal(audit,store.AuditEvents(new()).Items.Length);Assert.Equal(history,store.History(0,100,static _=>true).Items.Length);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void MissingOrAlteredTypedTextPartsFailExplicitlyWithoutLargeAllocation(bool alter){
        using var store=ManagementStore.CreateUpgradeStage(Root(),7);var account=new UserAccount("reader","hash",[new(new string('x',100000),ManagementPermission.View)]);store.Write(()=>store.Put("user","READER",account));
        var data=Database(store);var part=data.ManagedTextParts.First();
        store.Write(()=>{if(alter){var bytes=part.Text.ToArray();bytes[0]='z';data.ManagedTextParts[new(part.Id)]=part with{Text=bytes};}else{data.ManagedTextParts.Remove(new ManagementKey(part.Id));}});
        var error=Assert.Throws<InvalidDataException>(()=>store.Read<UserAccount>("user","READER"));Assert.Contains(alter?"hash":"分割行",error.Message);
        store.Write(()=>{var user=data.Users.First();data.Users[new(user.Id)]=user with{GrantCount=int.MaxValue};});Assert.Throws<InvalidDataException>(()=>store.Read<UserAccount>("user","READER"));
    }
    [Theory][InlineData("Marked")][InlineData("OldMoved")][InlineData("NewInstalled")][InlineData("Committed")]
    public void FailedInstallRestoresTheOriginalFormatSixStore(string phase){
        var root=Root();Guid instance;
        using(var source=ManagementStore.Initialize(root,"admin","TestPassword-123")){instance=source.InstanceId;source.Write(()=>source.Put("serverSettings","active",new StoredServerSettings(3,new(){ManagerPort=12345})));}
        ManagementStoreUpgrade.Checkpoint=current=>{if(current==phase){throw new IOException("injected switch failure");}};
        try{Assert.Throws<IOException>(()=>ManagementStoreUpgrade.UpgradeManagedTypes(root,root+"-backup"));}finally{ManagementStoreUpgrade.Checkpoint=null;}
        using var restored=new ManagementStore(root);Assert.Equal(6,restored.Format);Assert.Equal(instance,restored.InstanceId);Assert.Equal(3,Service.SavedServerSettings(restored)!.Version);Assert.NotNull(restored.Read<UserAccount>("user","ADMIN"));Assert.False(File.Exists(Path.Combine(root,"management.typed-upgrade.json")));
    }
    [Fact]public void CancellationAndActiveOwnerKeepTheOriginalStoreUsable(){
        var root=Root();using(var source=ManagementStore.Initialize(root,"admin","TestPassword-123")){Assert.Throws<IOException>(()=>ManagementStoreUpgrade.UpgradeManagedTypes(root,root+"-live-backup"));Assert.False(Directory.Exists(root+"-live-backup"));}
        using var cancellation=new CancellationTokenSource();ManagementStoreUpgrade.Checkpoint=phase=>{if(phase=="Staged"){cancellation.Cancel();}};
        try{Assert.Throws<OperationCanceledException>(()=>ManagementStoreUpgrade.UpgradeManagedTypes(root,root+"-backup",cancellation.Token));}finally{ManagementStoreUpgrade.Checkpoint=null;}
        using var restored=new ManagementStore(root);Assert.Equal(6,restored.Format);Assert.NotNull(restored.Read<UserAccount>("user","ADMIN"));Assert.False(File.Exists(Path.Combine(root,"management.typed-upgrade.json")));
    }
    [Fact]public void SparseMigrationCatalogChecksCancellationBetweenOrdinals(){
        using var source=ManagementStore.Initialize(Root(),"admin","TestPassword-123");source.Write(()=>source.Put("catalogHeader","user",new CatalogCounter(long.MaxValue)));
        using var cancellation=new CancellationTokenSource();
        var values=(IEnumerable<string>)typeof(ManagementStore).GetMethod("CatalogSubjects",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(source,["user",cancellation.Token])!;
        using var iterator=values.GetEnumerator();Assert.True(iterator.MoveNext());Assert.Equal("ADMIN",iterator.Current);cancellation.Cancel();Assert.Throws<OperationCanceledException>(()=>iterator.MoveNext());
    }
    [Fact]public void OfflineCopiesAndRepeatedUpgradePreserveCanonicalSettingsAndTypedRows(){
        var root=Root();var backup=root+"-backup";var copy=root+"-copy";var restored=root+"-restored";Guid instance;StoredServerSettings expected;
        var zeros=new string('0',1100000);var request=new ManagementServerSettings{Address=zeros+"127.0.0.1",ExternalManagerAddress=zeros+"127.0.0.1",ExternalManagerOrigin="https://example.test:"+zeros+"443/"};
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))using(var service=new Service(store,null,startAuditWorker:false)){
            instance=store.InstanceId;service.AttachServerSettings(new(new(),new(),[],"certificate.pfx",null,14336,"http://127.0.0.1:24503/"));
            var saved=service.SaveServerSettings(Admin(),new(0,request));Assert.Equal(request.Validate(),saved.Next);Assert.True(saved.Next.Address.Length<64);Assert.Equal("https://example.test/",saved.Next.ExternalManagerOrigin);
            service.ChangeUser(Admin(),new("reader","TestPassword-123",[new(new string('x',100000),ManagementPermission.View)]));expected=Service.SavedServerSettings(store)!;
        }
        ManagementStoreUpgrade.UpgradeManagedTypes(root,backup);ManagementStoreUpgrade.UpgradeManagedTypes(root,root+"-unused-backup");Assert.False(Directory.Exists(root+"-unused-backup"));
        Seamless.Management.Server.Program.CopyOffline(root,copy);Seamless.Management.Server.Program.CopyOffline(copy,restored);
        foreach(var path in new[]{root,copy,restored}){
            using var store=new ManagementStore(path);using var service=new Service(store,null,startAuditWorker:false);
            Assert.Equal(7,store.Format);Assert.Equal(instance,store.InstanceId);Assert.Equal(expected,Service.SavedServerSettings(store));Assert.Equal(2,service.UserProfiles(Admin()).Items.Length);Assert.Equal(100000,store.Read<UserAccount>("user","READER")!.Grants[0].Database.Length);Assert.NotNull(service.Login(new("reader","TestPassword-123")));
        }
        using var original=new ManagementStore(backup);Assert.Equal(6,original.Format);Assert.Equal(expected,Service.SavedServerSettings(original));
    }
    [Fact]public void UnknownFormatIsRejectedBeforeOpeningOrCreatingSystemStorage(){
        var root=Root();Directory.CreateDirectory(root);var marker=Path.Combine(root,"management.format");File.WriteAllText(marker,"8");
        Assert.Equal("InitializationRequired",Assert.Throws<ManagementException>(()=>new ManagementStore(root)).Code);Assert.Equal("8",File.ReadAllText(marker));Assert.False(Directory.Exists(Path.Combine(root,"system")));
        Assert.Equal("MigrationRequired",Assert.Throws<ManagementException>(()=>ManagementStoreUpgrade.UpgradeManagedTypes(root,root+"-backup")).Code);Assert.False(Directory.Exists(root+"-backup"));
    }
    [Theory][InlineData("user")][InlineData("serverSettings")]
    public void MissingCatalogRowCannotSilentlyDiscardAStoredManagedRecord(string kind){
        var root=Root();
        using(var source=ManagementStore.Initialize(root,"admin","TestPassword-123")){
            source.Write(()=>{source.Put("serverSettings","active",new StoredServerSettings(3,new()));var database=(ManagementDatabase)typeof(ManagementStore).GetField("database",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(source)!;database.Entries.Remove(new ManagementKey(ManagementStore.Key("catalog",kind+":1")));});
        }
        var error=Assert.Throws<InvalidDataException>(()=>ManagementStoreUpgrade.UpgradeManagedTypes(root,root+"-backup"));Assert.Contains("カタログ",error.Message);
        using var restored=new ManagementStore(root);Assert.Equal(6,restored.Format);Assert.NotNull(restored.Read<UserAccount>("user","ADMIN"));Assert.Equal(3,Service.SavedServerSettings(restored)!.Version);Assert.False(File.Exists(Path.Combine(root,"management.typed-upgrade.json")));
    }
    private static ManagementTypedDatabase Database(ManagementStore store)=>(ManagementTypedDatabase)typeof(ManagementStore).GetField("database",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(store)!;
    private static UserAccount Admin()=>new("admin","",[new("*",ManagementPermission.All)]);
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"typed-management-"+Guid.NewGuid().ToString("N"));
}
