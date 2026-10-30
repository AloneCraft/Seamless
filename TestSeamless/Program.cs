using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Rdbms;
using Seamless.Database.SqlServer;

namespace TestSeamless;

public static class Program{
    public static async Task<int> Main(string[] args){
        try{
            if(args.Length<2){throw new ArgumentException("Usage: TestSeamless <prepare|import|validate|module|serve|compare|health> <generation-directory> [product-version]");}
            if(args[0]=="sql"){if(args.Length!=4){throw new ArgumentException("Usage: sql <source|seamless> <database> <file.sql>");}await SqlConsole.RunAsync(args[1],args[2],args[3]);return 0;}
            var directory=Path.GetFullPath(args[1]);
            if(args[0] is "prepare" or "prepare-selected"){
                if(args.Length<3||args[0]=="prepare-selected"&&args.Length<4){throw new ArgumentException("Usage: prepare[-selected] <generation-directory> <version> [database ...]");}
                ComparisonGeneration generation;
                if(args[0]=="prepare-selected"){generation=await ComparisonSource.PrepareSelectedAsync(SourceConnection(),directory,args[2],args.Skip(3).ToArray());}else{generation=await ComparisonSource.PrepareAsync(SourceConnection(),directory,args[2]);}
                CatalogPreflight.Run(directory,generation);QueryModuleWriter.Write(directory,generation);return 0;
            }
            if(args[0]=="import"){await GenerationImport.RunAsync(directory,SourceConnection());return 0;}
            if(args[0]=="diagnose"){if(args.Length!=4){throw new ArgumentException("Usage: diagnose <generation-directory> <database> <schema.view>");}await Diagnostics.RunAsync(directory,args[2],args[3],SourceConnection());return 0;}
            if(args[0]=="module"){QueryModuleWriter.Deploy(directory);return 0;}
            if(args[0]=="validate"){await ComparisonRunner.ValidateAsync(directory,SourceConnection());return 0;}
            if(args[0]=="compare"){await ComparisonRunner.MeasureAsync(directory,SourceConnection());return 0;}
            if(args[0]=="serve"){
                using var host=new ComparisonHost(directory);using var gateway=new GatewayServer(host.Databases);
                gateway.Native.Load(ComparisonGeneration.ResolveManagedPath(directory,host.Generation.ModuleManifest??throw new InvalidDataException("Native module is missing.")),host.Generation.ProductVersion);gateway.Start();
                var identity=new{Generation=host.Generation.Id,Pid=Environment.ProcessId,CreatedUtc=System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()};await File.WriteAllTextAsync(Path.Combine(directory,"server-ready.json"),JsonSerializer.Serialize(identity));
                using var exit=new CancellationTokenSource();Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;exit.Cancel();};while(!exit.IsCancellationRequested&&!File.Exists(Path.Combine(directory,"server-stop"))){await Task.Delay(250);}await gateway.StopAsync();return 0;
            }
            if(args[0]=="health"){
                var expected=ComparisonGeneration.Read(directory);using var connection=new SqlConnection(TargetConnection("master"));await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText="SELECT name FROM sys.databases";using var reader=await command.ExecuteReaderAsync();var names=new HashSet<string>(StringComparer.Ordinal);while(await reader.ReadAsync()){names.Add(reader.GetString(0));}if(expected.Databases.Any(d=>!names.Contains(d.Name))){throw new InvalidDataException("The TDS database inventory does not match the generation.");}return 0;
            }
            throw new ArgumentException("Unknown comparison command.");
        }catch(Exception exception){Console.Error.WriteLine(exception);return 1;}
    }
    public static string SourceConnection(){
        var value=Environment.GetEnvironmentVariable("ConnectionStrings__Source");if(!string.IsNullOrWhiteSpace(value)){return value;}
        var secrets=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","UserSecrets","Seamless.Comparison.Local","secrets.json");
        foreach(var file in new[]{secrets,Path.Combine(AppContext.BaseDirectory,"appsettings.json")}){if(!File.Exists(file)){continue;}using var document=JsonDocument.Parse(File.ReadAllText(file));if(document.RootElement.TryGetProperty("ConnectionStrings",out var section)&&section.TryGetProperty("Source",out var source)){return source.GetString()??throw new InvalidDataException("Empty Source connection setting.");}if(document.RootElement.TryGetProperty("ConnectionStrings:Source",out source)){return source.GetString()!;}}
        throw new InvalidOperationException("Set ConnectionStrings__Source or user-secret ConnectionStrings:Source. Credentials are not written to generation files.");
    }
    public static string TargetConnection(string database){var value=Environment.GetEnvironmentVariable("ConnectionStrings__Seamless");if(string.IsNullOrWhiteSpace(value)){value="Server=127.0.0.1,14333;User ID=sa;Password=local-test;Encrypt=False;TrustServerCertificate=True";}return new SqlConnectionStringBuilder(value){InitialCatalog=database}.ConnectionString;}
}
