using System.Diagnostics;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class ManagementTypedUpgradeCrash{
    [Theory][InlineData("OldMoved",false)][InlineData("Committed",false)][InlineData("Published",true)]
    public async Task KilledUpgradeRequiresExplicitRollbackOrKeepsThePublishedTypedStore(string phase,bool published){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"typed-crash-"+Guid.NewGuid().ToString("N"));Guid instance;
        using(var source=ManagementStore.Initialize(root,"admin","TestPassword-123")){instance=source.InstanceId;source.Write(()=>source.Put("serverSettings","active",new StoredServerSettings(3,new(){ManagerPort=12345})));}
        var start=new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
        foreach(var argument in new[]{typeof(CrashProbe).Assembly.Location,"--managed-types-upgrade-probe",root,root+"-backup",phase}){start.ArgumentList.Add(argument);}
        using var child=Process.Start(start)!;using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));
        try{
            while(!File.Exists(root+".ready")){Assert.False(child.HasExited,"移行試験プロセスが停止点の前に終了しました。");await Task.Delay(25,timeout.Token);}
            var identity=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(root+".ready"));Assert.Equal(child.Id,identity.GetProperty("Pid").GetInt32());
            Assert.True(global::Seamless.Management.LocalProcessIdentity.Matches(child.Id,identity.GetProperty("CreationUtc").GetDateTime(),identity.GetProperty("ProcessIdentity").GetString()));Assert.Equal(255,identity.GetProperty("Affinity").GetInt64());
            child.Kill(entireProcessTree:true);Assert.True(child.WaitForExit(60000));
        }finally{if(!child.HasExited){child.Kill(entireProcessTree:true);Assert.True(child.WaitForExit(60000));}}
        if(published){Assert.Equal("NoPendingUpgrade",Assert.Throws<ManagementException>(()=>ManagementStoreUpgrade.RollbackManagedTypes(root)).Code);}
        else{Assert.Equal("RecoveryRequired",Assert.Throws<ManagementException>(()=>new ManagementStore(root)).Code);ManagementStoreUpgrade.RollbackManagedTypes(root);}
        var expected=6;if(published){expected=7;}
        using var restored=new ManagementStore(root);Assert.Equal(expected,restored.Format);Assert.Equal(instance,restored.InstanceId);Assert.NotNull(restored.Read<UserAccount>("user","ADMIN"));Assert.Equal(3,restored.Read<StoredServerSettings>("serverSettings","active")!.Version);
        using var backup=new ManagementStore(root+"-backup");Assert.Equal(6,backup.Format);Assert.Equal(instance,backup.InstanceId);
    }
}
