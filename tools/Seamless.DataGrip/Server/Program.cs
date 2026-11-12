using System.Diagnostics;
using System.Text.Json;
using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;

namespace DataGrip.Stage1.Server;

internal static class Program{
    private static async Task<int> Main(string[]args){
        try{
            await RunAsync(args);
            return 0;
        }catch(Exception error){
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static async Task RunAsync(string[]args){
        if(args.Length==1&&args[0]=="--wire-survey"){WireSurvey.Run();return;}
        if(args.Length<3||args.Length>5){throw new ArgumentException("Usage: DataGrip.Stage1.Server <D: run directory> <F: cache directory> <generator.dll> [port] [--reopen]");}
        var reopen=args.Length==5&&args[4]=="--reopen";
        if(args.Length==5&&!reopen){throw new ArgumentException("Unknown fixture option.");}
        var data=Path.GetFullPath(args[0]);var cache=Path.GetFullPath(args[1]);
        if(!data.StartsWith("D:\\CodexTemp\\datagrip-stage1-",StringComparison.OrdinalIgnoreCase)||!cache.StartsWith("F:\\CodexTemp\\datagrip-stage1-",StringComparison.OrdinalIgnoreCase)){
            throw new ArgumentException("Use isolated DataGrip stage1 roots on D and F.");
        }
        Directory.CreateDirectory(data);Directory.CreateDirectory(cache);
        using var ownership=new FileStream(Path.Combine(data,".owner.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var owner=Path.Combine(data,"owner.json");
        if(File.Exists(owner)){
            if(!reopen){throw new InvalidOperationException("Refuse to reuse a previous run without explicit --reopen.");}
            using var previous=JsonDocument.Parse(await File.ReadAllTextAsync(owner));
            if(!Path.GetFullPath(previous.RootElement.GetProperty("data").GetString()!).Equals(data,StringComparison.OrdinalIgnoreCase)){
                throw new InvalidDataException("Fixture owner directory mismatch.");
            }
            File.Copy(owner,Path.Combine(data,"owner-history-"+Guid.NewGuid().ToString("N")+".json"),false);
            File.Delete(Path.Combine(data,"stop"));
        }else if(reopen){throw new InvalidOperationException("Explicit reopen requires an existing fixture owner.");}
        await File.WriteAllTextAsync(owner,JsonSerializer.Serialize(new{pid=Environment.ProcessId,creationUtc=Process.GetCurrentProcess().StartTime.ToUniversalTime(),data,cache}));
        var references=Directory.EnumerateFiles(AppContext.BaseDirectory,"*.dll").Where(static path=>Path.GetFileName(path).StartsWith("Seamless.",StringComparison.Ordinal)&&!Path.GetFileName(path).Contains("Generator",StringComparison.Ordinal)||Path.GetFileName(path).StartsWith("MemoryPack",StringComparison.Ordinal)).ToArray();
        var builder=new GeneratedAssemblyBuilder(new(Path.Combine(data,"generated"),Path.Combine(cache,"generated"),Environment.ProcessPath!,Path.GetFullPath(args[2]),references));
        var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(data,"tmp")};
        var catalog=new DatabaseCatalog("DataGripStage1",[new SchemaCatalog("dbo",[])]);
        using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(data,"database"),builder,options,[catalog]);
        var engine=new SqlExecutionEngine(host.Databases);using var seed=new SessionContext(1,"DataGripStage1");
        if(!reopen){
            foreach(var result in engine.ExecuteBatch("CREATE TABLE dbo.Stage1Items(Id int NOT NULL PRIMARY KEY,Name nvarchar(80) NULL,Amount decimal(18,4) NULL,Enabled bit NOT NULL,Payload varbinary(16) NULL,At datetime2(7) NULL); INSERT dbo.Stage1Items(Id,Name,Amount,Enabled,Payload,At) VALUES(1,N'日本語',123.4567,1,0x0001FF,'2026-11-02T12:34:56.1234567'),(2,NULL,NULL,0,NULL,NULL)",seed)){
                foreach(var row in result.Rows){}
            }
        }
        var port=0;if(args.Length>=4){port=int.Parse(args[3],global::System.Globalization.CultureInfo.InvariantCulture);}
        using var server=new NativeToolsServer(host.Databases,port);server.Start();
        var ready=new{pid=Environment.ProcessId,creationUtc=Process.GetCurrentProcess().StartTime.ToUniversalTime(),port=server.Port,url="jdbc:seamless://127.0.0.1:"+server.Port+"/DataGripStage1"};
        await File.WriteAllTextAsync(Path.Combine(data,"ready.json"),JsonSerializer.Serialize(ready));Console.WriteLine(JsonSerializer.Serialize(ready));
        while(!File.Exists(Path.Combine(data,"stop"))){await Task.Delay(250);}
        await server.StopAsync();
    }
}
