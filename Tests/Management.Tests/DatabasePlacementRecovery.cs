using System.Diagnostics;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection(SourceBuildBudgetCollection.Name)]
public sealed class DatabasePlacementRecovery{
    [Fact]
    public async Task LegacyPlacementSurvivesProcessStopAndManualRenameAcrossDrives(){
        var suffix="database-placement-"+Guid.NewGuid().ToString("N");
        var work=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,suffix);
        var external=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TABLE_STORAGE_TEST_ROOT")??work,suffix);
        var root=Path.Combine(work,"management");
        var backup=Path.Combine(external,"before-migration");
        var original=NativeWorkflow.Database();
        DefinitionVersion initial;
        Draft saved;
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new Seamless.Management.Server.ManagementService(store)){
            var admin=Admin(service);
            NativeWorkflow.Publish(service,admin,original);
            await service.CommitRowsAsync(admin,new("Native",1,Guid.NewGuid(),[
                new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=7,Value=35}))]));
            service.ChangeUser(admin,new("editor","EditorPassword-123",[new("Native",ManagementPermission.View|ManagementPermission.Edit|ManagementPermission.ReadRows)]));
            service.ChangeUser(admin,new("other","OtherPassword-123",[new("Orders",ManagementPermission.View)]));
            initial=service.GetDefinition(admin,"db");
            var absent=initial with{
                Definition=initial.Definition with{Id="removed-db",Database="Removed"},
                Database="Removed",
                StoragePath=Path.Combine("generations",Guid.NewGuid().ToString("N"))
            };
            store.Write(()=>store.Put("active",absent.Definition.Id,absent));
            var editor=Login(service,"editor","EditorPassword-123");
            saved=service.SaveDraft(editor,new(Changed("// editor"),0,1));
            _=service.SaveDraft(admin,new(Changed("// publication"),1,1));
        }
        Assert.Equal(1,DatabasePlacementMigration.Run(root,backup,CancellationToken.None));
        var entry=Path.Combine(root,"generations","Native");
        var v1=Path.Combine(entry,"v000001");
        Assert.True(File.Exists(Path.Combine(v1,"module.dll")));
        Assert.True(File.Exists(Path.Combine(backup,initial.StoragePath,"module.dll")));
        var externalDatabase=Path.Combine(external,"data");
        NativeRuntime.CopyGenerationDirectory(v1,Path.Combine(externalDatabase,"v000001"),CancellationToken.None);
        Directory.Move(entry,Path.Combine(work,"before-link"));
        ManagementPlacement.CreateDirectoryLink(entry,externalDatabase);

        var beforeCommit=await StopPublication("Prepared",root,work);
        using(var store=new ManagementStore(root))
        using(var service=new Seamless.Management.Server.ManagementService(store)){
            Assert.Equal(1,service.GetDefinition(Admin(service),"db").Version);
            Assert.Equal(OperationState.OutcomeUnknown,service.GetOperation(Admin(service),beforeCommit).State);
            Assert.False(Directory.Exists(Path.Combine(externalDatabase,"v000002")));
            await CheckRow(service,Admin(service),"Native");
        }

        var afterCommit=await StopPublication("PromotionPending",root,work);
        Assert.False(Directory.Exists(Path.Combine(externalDatabase,"v000002")));
        using(var store=new ManagementStore(root))
        using(var service=new Seamless.Management.Server.ManagementService(store)){
            var admin=Admin(service);
            Assert.Equal(2,service.GetDefinition(admin,"db").Version);
            Assert.Equal(OperationState.Succeeded,service.GetOperation(admin,afterCommit).State);
            Assert.True(Directory.Exists(Path.Combine(externalDatabase,"v000002")));
            await CheckRow(service,admin,"Native");
            store.Write(()=>store.Put("active","db",store.Read<DefinitionVersion>("version","db:1")!));
        }

        Directory.Move(entry,Path.Combine(root,"generations","Orders"));
        using(var store=new ManagementStore(root))
        using(var service=new Seamless.Management.Server.ManagementService(store)){
            var editor=Login(service,"editor","EditorPassword-123");
            Assert.Equal(2,store.FindDatabase("Orders")?.Version);
            Assert.Null(store.FindDatabase("Native"));
            Assert.Contains(service.Databases(editor).Items,item=>item.Name=="Orders"&&item.DefinitionId=="db"&&item.PlacementError is null);
            await CheckRow(service,editor,"Orders");
            var draft=service.GetDraft(editor,"db");
            Assert.Equal(saved.Definition.Hash(),draft.Definition.Hash());
            Assert.Equal(saved.Revision,draft.Revision);
            Assert.Equal(saved.BaseVersion,draft.BaseVersion);
            Assert.Empty(service.Databases(Login(service,"other","OtherPassword-123")).Items);
        }

        var duplicate=Path.Combine(root,"generations","OtherOrders");
        ManagementPlacement.CreateDirectoryLink(duplicate,externalDatabase);
        using(var store=new ManagementStore(root))
        using(var service=new Seamless.Management.Server.ManagementService(store)){
            var admin=Admin(service);
            var placements=service.Databases(admin).Items.Where(item=>item.Name is "Orders" or "OtherOrders").ToArray();
            Assert.Equal(2,placements.Length);
            Assert.All(placements,item=>Assert.Contains("同じDB ID",item.PlacementError));
            var error=await Assert.ThrowsAsync<ManagementException>(()=>service.ReadRowsAsync(admin,new("Orders","Rows")));
            Assert.Equal("RecoveryRequired",error.Code);
        }
        Directory.Move(duplicate,Path.Combine(work,"duplicate-before-removal"));

        var manifest=Path.Combine(externalDatabase,"v000002","manifest.json");
        var validManifest=File.ReadAllText(manifest);
        File.WriteAllText(manifest,"{");
        using(var store=new ManagementStore(root))
        using(var service=new Seamless.Management.Server.ManagementService(store)){
            var admin=Admin(service);
            Assert.NotNull(Assert.Single(service.Databases(admin).Items,item=>item.Name=="Orders").PlacementError);
            var error=await Assert.ThrowsAsync<ManagementException>(()=>service.ReadRowsAsync(admin,new("Orders","Rows")));
            Assert.Equal("RecoveryRequired",error.Code);
            Assert.Contains("v000002",error.Message);
        }

        File.WriteAllText(manifest,validManifest);

        Definition Changed(string comment)=>original with{Source=original.Source with{Revision="2",
            Files=[original.Source.Files[0] with{Text=original.Source.Files[0].Text+"\n"+comment}]}};
    }

    private static UserAccount Admin(Seamless.Management.Server.ManagementService service)=>Login(service,"admin","TestPassword-123");
    private static UserAccount Login(Seamless.Management.Server.ManagementService service,string name,string password)=>
        service.Authenticate(service.Login(new(name,password)).Token);

    private static async Task CheckRow(Seamless.Management.Server.ManagementService service,UserAccount user,string database){
        var row=Assert.Single((await service.ReadRowsAsync(user,new(database,"Rows"))).Rows).Values;
        Assert.Equal(7,row.GetProperty("id").GetInt32());
        Assert.Equal(35,row.GetProperty("value").GetInt32());
    }

    private static async Task<Guid> StopPublication(string phase,string root,string work){
        var dotnet=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??
            (OperatingSystem.IsWindows()?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe"):"dotnet");
        var start=new ProcessStartInfo(dotnet){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
        var marker=Path.Combine(work,phase+".ready");
        foreach(var argument in new[]{typeof(CrashProbe).Assembly.Location,"--placement-publication-probe",root,phase,marker}){
            start.ArgumentList.Add(argument);
        }
        using var process=Process.Start(start)!;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try{
            while(!File.Exists(marker)){
                Assert.False(process.HasExited,"公開試験プロセスが確定点に達する前に終了しました。");
                await Task.Delay(50,timeout.Token);
            }
            return Guid.Parse(File.ReadAllText(marker));
        }finally{
            if(!process.HasExited){process.Kill(entireProcessTree:true);}
            Assert.True(process.WaitForExit(30000),"公開試験プロセスが停止しませんでした。");
        }
    }
}
