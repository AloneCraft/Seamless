using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Seamless.Database.Gateway;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Server;

public sealed record GatewayStorageConfiguration(string StorageDirectory,string WorkingDirectory,string CacheDirectory,string GeneratorAssemblyPath){
    public string DotNetExecutable{get;init;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
    public IReadOnlyList<DatabaseCatalog>? InitialCatalogs{get;init;}
    public DatabaseContainerOptions DatabaseOptions{get;init;}=new();
    public GatewayServerOptions Endpoints{get;init;}=new();
    public string? HttpCertificatePath{get;init;}
    public string? NativeCertificatePath{get;init;}
    public string? TdsCertificatePath{get;init;}
    public GatewayHttpAuthorizationConfiguration? HttpAuthorization{get;init;}
    public GatewayAuthorizationConfiguration? Authorization{get;init;}
    public IReadOnlyList<string> NativeAnalyzerPaths{get;init;}=[];
    public IReadOnlyList<string> InitialNativeProjects{get;init;}=[];
}

public static partial class Program{
    public static async Task ServeGatewayAsync(string[] args){
        if(args.Length<2||args[0]!="--gateway"){throw new ArgumentException("Usage: --gateway <configuration.json> [--stop-file <path>] [--ready-file <path>]");}
        var json=new JsonSerializerOptions{PropertyNameCaseInsensitive=true,Converters={new JsonStringEnumConverter(),new ListenAddressConverter()}};
        var configurationPath=Path.GetFullPath(args[1]);
        var configuration=JsonSerializer.Deserialize<GatewayStorageConfiguration>(await File.ReadAllTextAsync(configurationPath),json)??throw new InvalidDataException("Missing Gateway storage configuration.");
        string? stop=null,ready=null;for(var index=2;index<args.Length;index+=2){if(index+1>=args.Length){throw new ArgumentException("Missing option value.");}if(args[index]=="--stop-file"){stop=Path.GetFullPath(args[index+1]);}else if(args[index]=="--ready-file"){ready=Path.GetFullPath(args[index+1]);}else{throw new ArgumentException("Unknown Gateway server option: "+args[index]);}}
        var configurationDirectory=Path.GetDirectoryName(configurationPath)!;
        var authorization=configuration.Authorization??configuration.HttpAuthorization;
        var protectTds=authorization?.ProtectTds==true;
        using var httpCertificate=LoadHttpCertificate(configuration,configurationDirectory);
        using var nativeCertificate=LoadNativeCertificate(configuration,configurationDirectory);
        using var tdsCertificate=LoadTdsCertificate(configuration,configurationDirectory,protectTds);
        if(httpCertificate is not null){configuration.Endpoints.Http=configuration.Endpoints.Http with{Certificate=httpCertificate};}
        if(nativeCertificate is not null){configuration.Endpoints.NativeCertificate=nativeCertificate;}
        configuration.Endpoints.NativeAuthenticatedLimits=authorization?.Native?.Limits;
        configuration.Endpoints.ValidateNativeTransport(authorization?.Native is not null);
        using var protection=GatewayProtection.Open(configuration,configurationDirectory);
        var references=Directory.EnumerateFiles(AppContext.BaseDirectory,"*.dll").Where(static path=>{
            var name=Path.GetFileName(path);
            return name.StartsWith("MemoryPack",StringComparison.Ordinal)||name.StartsWith("Seamless.",StringComparison.Ordinal)
                &&!name.Contains("Generator",StringComparison.Ordinal)&&!name.StartsWith("Seamless.Management",StringComparison.Ordinal);
        }).ToArray();
        var compiler=new GeneratedAssemblyBuilder(new(configuration.WorkingDirectory,configuration.CacheDirectory,configuration.DotNetExecutable,configuration.GeneratorAssemblyPath,references){NativeAnalyzerPaths=configuration.NativeAnalyzerPaths});
        using var host=await GatewayDatabaseHost.OpenAsync(configuration.StorageDirectory,compiler,configuration.DatabaseOptions,protection is null?configuration.InitialCatalogs:null);
        var protectHttp=authorization?.ProtectHttp==true;
        var sqlAuthorization=protectHttp||protectTds?protection!.Bind(host):null;
        var httpAuthorization=protectHttp?sqlAuthorization:null;
        if(protectTds){configuration.Endpoints.TdsAuthorization=protection!.BindTds(tdsCertificate!,sqlAuthorization!);}
        foreach(var path in configuration.InitialNativeProjects){
            var project=JsonSerializer.Deserialize<NativeQuerySourceProject>(await File.ReadAllTextAsync(path),json)??throw new InvalidDataException("Missing initial Native project: "+path);
            if(!host.Transactions.NativeQueries.Any(query=>query.ModuleId==project.ModuleId)){await host.Transactions.InstallNativeQueryAsync(project);}
        }
        configuration.Endpoints.NativeAuthorization=protection?.BindNative(host,configurationDirectory);
        if(httpAuthorization is not null||configuration.Endpoints.NativeAuthorization is not null||protectTds){
            if(!protectTds){configuration.Endpoints.TdsEnabled=false;}
            if(configuration.Endpoints.NativeAuthorization is null){configuration.Endpoints.NativeEnabled=false;}
            if(httpAuthorization is null){configuration.Endpoints.HttpEnabled=false;}
        }
        using var gateway=new GatewayServer(host.Databases,configuration.Endpoints,httpAuthorization);
        gateway.Start();
        await using var authorizationServer=protection?.CreateManagementServer(httpCertificate??nativeCertificate??tdsCertificate);authorizationServer?.Start();
        var nativeAddress=configuration.Endpoints.NativeEnabled?gateway.Native.ListenAddress:null;
        if(ready is not null){
            using var process=global::System.Diagnostics.Process.GetCurrentProcess();
            var temporary=ready+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{
                var databases=host.Databases.Databases.Select(static entry=>(entry.Name,Profile:(entry.Adapter as InProcessDatabaseAdapter)?.Database.Options.GatewayProfile))
                    .Where(static entry=>entry.Profile is not null).Select(static entry=>new{entry.Name,entry.Profile!.DatabaseId,entry.Profile.GenerationId}).ToArray();
                await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(new{Pid=process.Id,CreatedUtc=process.StartTime.ToUniversalTime(),Storage=configuration.StorageDirectory,GatewayWalId=host.Transactions.WalId,Databases=databases,TdsPort=configuration.Endpoints.TdsEnabled?(int?)gateway.TdsPort:null,TdsAuthenticated=protectTds,HttpPort=configuration.Endpoints.HttpEnabled?(int?)gateway.HttpPort:null,NativeListenAddress=nativeAddress?.ToString(),NativePort=configuration.Endpoints.NativeEnabled?(int?)gateway.NativePort:null,NativeTls=nativeAddress is not null&&configuration.Endpoints.NativeCertificate is not null,NativeAuthenticated=configuration.Endpoints.NativeAuthorization is not null,AuthorizationManagementPort=authorizationServer?.Port}));
                File.Move(temporary,ready,overwrite:true);
            }finally{File.Delete(temporary);}
        }
        var httpProtocol="HTTP";
        if(configuration.Endpoints.Http.Certificate is not null){httpProtocol="HTTPS";}
        var nativeProtocol=configuration.Endpoints.NativeCertificate is null?"Native":"Native TLS";
        if(configuration.Endpoints.NativeAuthorization is not null){nativeProtocol="Authenticated Native TLS";}
        var tdsProtocol=protectTds?"Authenticated TDS 8.0 TLS":"TDS";
        var endpoints=new List<string>();if(configuration.Endpoints.TdsEnabled){endpoints.Add($"{tdsProtocol} localhost,{gateway.TdsPort}");}if(configuration.Endpoints.HttpEnabled){endpoints.Add($"{httpProtocol} localhost:{gateway.HttpPort}");}if(nativeAddress is not null){endpoints.Add($"{nativeProtocol} {new IPEndPoint(nativeAddress,gateway.NativePort)}");}
        Console.WriteLine("Gateway: "+string.Join("; ",endpoints));
        var completion=new TaskCompletionSource();ConsoleCancelEventHandler cancel=(_,arguments)=>{arguments.Cancel=true;completion.TrySetResult();};Console.CancelKeyPress+=cancel;
        using var approvalCancellation=new CancellationTokenSource();
        var approvalUpdates=protection?.WatchNativeApprovalsAsync(approvalCancellation.Token)??Task.CompletedTask;
        try{if(stop is null){await completion.Task;}else{while(!File.Exists(stop)&&!completion.Task.IsCompleted){await Task.WhenAny(completion.Task,Task.Delay(250));}}}
        finally{
            Console.CancelKeyPress-=cancel;
            approvalCancellation.Cancel();
            try{await approvalUpdates;}finally{await gateway.StopAsync();}
        }
    }

    private static X509Certificate2? LoadHttpCertificate(GatewayStorageConfiguration configuration,string configurationDirectory){
        if(configuration.HttpCertificatePath is not{} path){return null;}
        if(string.IsNullOrWhiteSpace(path)){throw new InvalidDataException("HttpCertificatePath must name a PFX file.");}
        if(configuration.Endpoints.Http.Certificate is not null){throw new InvalidDataException("Specify the HTTP certificate only once.");}
        var certificate=X509CertificateLoader.LoadPkcs12FromFile(Path.GetFullPath(path,configurationDirectory),Environment.GetEnvironmentVariable("SEAMLESS_GATEWAY_HTTP_CERT_PASSWORD"));
        if(certificate.HasPrivateKey){return certificate;}
        certificate.Dispose();
        throw new InvalidDataException("HttpCertificatePath must name a PFX with a private key.");
    }

    private static X509Certificate2? LoadNativeCertificate(GatewayStorageConfiguration configuration,string configurationDirectory){
        if(configuration.NativeCertificatePath is not{} path){return null;}
        if(string.IsNullOrWhiteSpace(path)){throw new InvalidDataException("NativeCertificatePath must name a PFX file.");}
        if(configuration.Endpoints.NativeCertificate is not null){throw new InvalidDataException("Specify the native certificate only once.");}
        var certificate=X509CertificateLoader.LoadPkcs12FromFile(Path.GetFullPath(path,configurationDirectory),Environment.GetEnvironmentVariable("SEAMLESS_GATEWAY_NATIVE_CERT_PASSWORD"));
        if(certificate.HasPrivateKey){return certificate;}
        certificate.Dispose();
        throw new InvalidDataException("NativeCertificatePath must name a PFX with a private key.");
    }

    private static X509Certificate2? LoadTdsCertificate(GatewayStorageConfiguration configuration,string configurationDirectory,bool protectTds){
        var path=configuration.TdsCertificatePath;
        if(!protectTds){
            if(path is not null){throw new InvalidDataException("TdsCertificatePath requires Authorization.ProtectTds.");}
            return null;
        }
        if(string.IsNullOrWhiteSpace(path)){throw new InvalidDataException("Protected TDS requires TdsCertificatePath.");}
        if(!configuration.Endpoints.TdsEnabled||configuration.Endpoints.TdsPort is <0 or >65535||configuration.Endpoints.RequestLogPath is not null){
            throw new InvalidDataException("Protected TDS requires an enabled endpoint, a valid port and no request log.");
        }
        var certificate=X509CertificateLoader.LoadPkcs12FromFile(Path.GetFullPath(path,configurationDirectory),Environment.GetEnvironmentVariable("SEAMLESS_GATEWAY_TDS_CERT_PASSWORD"));
        if(certificate.HasPrivateKey){return certificate;}
        certificate.Dispose();
        throw new InvalidDataException("TdsCertificatePath must name a PFX with a private key.");
    }

    private sealed class ListenAddressConverter:JsonConverter<IPAddress>{
        public override bool HandleNull=>true;
        public override IPAddress Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options){
            if(reader.TokenType==JsonTokenType.String&&IPAddress.TryParse(reader.GetString(),out var address)){
                return address;
            }
            throw new JsonException("ListenAddress must be an IPv4 or IPv6 address string.");
        }
        public override void Write(Utf8JsonWriter writer,IPAddress value,JsonSerializerOptions options)=>writer.WriteStringValue(value.ToString());
    }
}
