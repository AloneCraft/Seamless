using System.Diagnostics;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;
using Service=Seamless.Management.Server.ManagementService;

namespace Management.Tests;

public sealed class ExternalTableStorage{
    [Fact]
    public async Task NewTablePlacementSurvivesWalRecoveryAndPortableBackup(){
        var name="table-storage-"+Guid.NewGuid().ToString("N");
        var work=Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,name));
        var separate=Environment.GetEnvironmentVariable("SEAMLESS_TABLE_STORAGE_TEST_ROOT");
        var secondary=separate is null?work:Path.GetFullPath(Path.Combine(separate,name));
        if(separate is not null){Assert.NotEqual(Path.GetPathRoot(work),Path.GetPathRoot(secondary));}
        var root=Path.Combine(work,"management");
        var external=Path.Combine(secondary,"tables","Orders");
        var ordinary=NativeWorkflow.Database();
        using(var store=ManagementStore.Initialize(root,"admin","TestPassword-123"))
        using(var service=new Service(store)){
            NativeWorkflow.Publish(service,User(service),ordinary);
        }

        Configure(root,("Rows",external));
        using(var store=new ManagementStore(root))
        using(var service=new Service(store)){
            var user=User(service);
            var draft=service.SaveDraft(user,new(ordinary,1,1));
            var preview=service.Preview(user,new(ordinary.Id,draft.Revision));
            Assert.False(preview.CanApply);
            Assert.Contains(preview.Diagnostics,item=>item.Code=="TableStorage"&&item.Message.Contains("Rows",StringComparison.Ordinal));
            Assert.False(Directory.Exists(external));
        }

        Configure(root,("Sales.Orders",external));
        var expanded=WithSchema(ordinary);
        string generation;
        using(var store=new ManagementStore(root))
        using(var service=new Service(store)){
            var user=User(service);
            NativeWorkflow.Publish(service,user,expanded,2,1);
            var published=service.GetDefinition(user,expanded.Id);
            generation=Path.Combine(root,published.StoragePath);
            var layout=Assert.IsType<TableStorageLayout>(TableStorage.Read(generation));
            Assert.False(layout.Bundled);
            Assert.Equal("Sales.Orders",Assert.Single(layout.Locations).Table);
            Assert.Equal(external,Assert.Single(layout.Locations).Root);
            Assert.True(Directory.Exists(Path.Combine(generation,"data","wal-v2")));
        }

        await RunCrashProbe(root);
        using(var store=new ManagementStore(root))
        using(var service=new Service(store)){
            var user=User(service);
            await AssertRows(service,user);
            await Assert.ThrowsAnyAsync<Exception>(()=>service.CommitRowsAsync(user,new("Native",2,Guid.NewGuid(),[
                Insert("Sales.Orders",2,202),Insert("Rows",1,99)])));
            await AssertRows(service,user);
        }
        Assert.True(Directory.Exists(Path.Combine(generation,"data","store-v2","data","Rows")));
        Assert.False(Directory.Exists(Path.Combine(generation,"data","store-v2","data","Sales.Orders")));
        Assert.NotEmpty(Directory.EnumerateFiles(external,"*",SearchOption.AllDirectories));

        var backup=Path.Combine(secondary,"backup");
        var restored=Path.Combine(secondary,"restored");
        Seamless.Management.Server.Program.CopyOffline(root,backup);
        Seamless.Management.Server.Program.CopyOffline(backup,restored);
        var deletion=Guid.NewGuid();
        using(var store=new ManagementStore(root))
        using(var service=new Service(store)){
            var user=User(service);
            var draft=service.SaveDraft(user,new(expanded,service.GetDraft(user,expanded.Id).Revision,2,2){DatabaseChange=new("Native",Delete:true)});
            var preview=service.Preview(user,new(expanded.Id,draft.Revision));
            Assert.True(preview.CanApply);
            var outcome=service.Apply(user,new(expanded.Id,draft.Revision,preview.Token,deletion,true));
            Assert.True(outcome.State==OperationState.Succeeded,outcome.Error);
            Assert.True(TableStorage.Read(Path.Combine(outcome.BackupPath!,"database"))!.Bundled);
        }
        Directory.Move(external,Path.Combine(secondary,"original-external-storage"));
        var failure=Assert.Throws<ManagementException>(()=>TableStorage.OpenDirectories(generation,TableStorage.Read(generation)!.Id));
        Assert.Contains("Sales.Orders",failure.Message);
        Assert.Contains(external,failure.Message);
        using(var store=new ManagementStore(restored))
        using(var service=new Service(store)){
            var user=User(service);
            var published=service.GetDefinition(user,expanded.Id);
            Assert.True(TableStorage.Read(Path.Combine(restored,published.StoragePath))!.Bundled);
            await AssertRows(service,user);
            var next=expanded with{Source=expanded.Source with{Revision="3"}};
            NativeWorkflow.Publish(service,user,next,service.GetDraft(user,expanded.Id).Revision,2);
            var republished=service.GetDefinition(user,expanded.Id);
            var layout=TableStorage.Read(Path.Combine(restored,republished.StoragePath))!;
            Assert.False(layout.Bundled);
            Assert.Equal(external,Assert.Single(layout.Locations).Root);
            await AssertRows(service,user);
        }
        Assert.Equal(3,RestoreDeletedDatabase.Run(root,deletion,CancellationToken.None).Version);
        using(var store=new ManagementStore(root))
        using(var service=new Service(store)){
            await AssertRows(service,User(service));
        }
    }

    private static Definition WithSchema(Definition original){
        var source=original.Source.Files[0];
        var text=source.Text.Replace("public Table<Keys.Row,Tables.Row> Rows","public SalesSchema Sales=>new(this);public Table<Keys.Row,Tables.Row> Rows",StringComparison.Ordinal)
            .Replace("namespace Tables{","public sealed class SalesSchema(DatabaseContainer db):DatabaseSchema(db,\"Sales\"){public Table<Keys.Row,Tables.Row> Orders=>this.Table<Keys.Row,Tables.Row>(row=>new(row.Id));}\nnamespace Tables{",StringComparison.Ordinal);
        return original with{Source=original.Source with{Revision="2",Files=[source with{Text=text}]}};
    }

    private static void Configure(string root,params (string Table,string Directory)[] tables)=>
        File.WriteAllText(Path.Combine(root,"table-storage.json"),JsonSerializer.Serialize(new{Native=tables.ToDictionary(item=>item.Table,item=>item.Directory,StringComparer.Ordinal)}));

    private static UserAccount User(Service service)=>service.Authenticate(service.Login(new("admin","TestPassword-123")).Token);
    private static TableRowChange Insert(string table,int id,int value)=>new(table,TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=id,Value=value}));

    private static async Task AssertRows(Service service,UserAccount user){
        Assert.Equal(10,Assert.Single((await service.ReadRowsAsync(user,new("Native","Rows"))).Rows).Values.GetProperty("value").GetInt32());
        Assert.Equal(20,Assert.Single((await service.ReadRowsAsync(user,new("Native","Sales.Orders"))).Rows).Values.GetProperty("value").GetInt32());
    }

    private static async Task RunCrashProbe(string root){
        var start=new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet"){
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true
        };
        foreach(var argument in new[]{typeof(CrashProbe).Assembly.Location,"--external-table-storage-crash",root}){start.ArgumentList.Add(argument);}
        using var process=Process.Start(start)!;
        var stdout=process.StandardOutput.ReadToEndAsync();
        var stderr=process.StandardError.ReadToEndAsync();
        using var limit=new CancellationTokenSource(TimeSpan.FromMinutes(1));
        try{await process.WaitForExitAsync(limit.Token);}
        finally{if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}}
        Assert.True(process.ExitCode==137,await stderr);
        Assert.Contains("transaction committed",await stdout);
    }

    internal static async Task<int> CrashAfterCommit(string root){
        using var store=new ManagementStore(root);
        using var service=new Service(store);
        var result=await service.CommitRowsAsync(User(service),new("Native",2,Guid.NewGuid(),[Insert("Rows",1,10),Insert("Sales.Orders",1,20)]));
        if(result.ChangedRows!=2){return 3;}
        Console.WriteLine("transaction committed");
        Console.Out.Flush();
        Environment.Exit(137);
        return 137;
    }
}
