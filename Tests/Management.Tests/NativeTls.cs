using System.Net.Security;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using MemoryPack;
using Seamless.Database;
using Seamless.Database.Client;
using Seamless.Database.Gateway;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using DatabaseCatalog=Seamless.Database.Rdbms.DatabaseCatalog;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed partial class NativeTls{
    private const string Fingerprint="native-tls-v1";

    [Fact]
    public Task RpcAndTransactionKeepTheirWireContracts()=>ManagementTransport.WithServerCertificate(async certificate=>{
        using var fixture=new Fixture(certificate);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client=NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate);
        Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
        await using(var transaction=await client.BeginTransactionAsync("fixture",timeout.Token)){
            await foreach(var result in transaction.QueryAsync<int,int>("tls","insert",Fingerprint,7,timeout.Token)){Assert.Equal(1,result);}
            await transaction.RollbackAsync(timeout.Token);
        }
        Assert.Equal(0,await client.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,7,timeout.Token));
        await using(var transaction=await client.BeginTransactionAsync("fixture",timeout.Token)){
            await foreach(var result in transaction.QueryAsync<int,int>("tls","insert",Fingerprint,7,timeout.Token)){Assert.Equal(1,result);}
            await transaction.CommitAsync(timeout.Token);
        }
        Assert.Equal(1,await client.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,7,timeout.Token));
    });

    [Theory][InlineData("untrusted")][InlineData("hostname")][InlineData("other-root")][InlineData("expired")]
    public Task InvalidPeerIsRejectedAndDoesNotRunTheModule(string kind)=>ManagementTransport.WithServerCertificate(async certificate=>{
        using var fixture=new Fixture(certificate);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        async Task Reject(X509Certificate2? root){
            var host=kind=="hostname"?"127.0.0.1":"localhost";
            await using var client=NativeQueryClient.ForTls(host,fixture.Server.Port,root);
            await Assert.ThrowsAnyAsync<AuthenticationException>(()=>client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
        }
        if(kind=="other-root"){await ManagementTransport.WithServerCertificate(Reject);}else{await Reject(kind is "hostname" or "expired"?certificate:null);}
        Assert.Equal(0,fixture.Module.Calls);
        if(kind=="expired"){return;}
        await using var valid=NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate);
        Assert.Equal(8,await valid.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
    },expired:kind=="expired");

    [Theory][InlineData(false)][InlineData(true)]
    public Task TransportMismatchNeverFallsBack(bool serverTls)=>ManagementTransport.WithServerCertificate(async certificate=>{
        using var fixture=new Fixture(serverTls?certificate:null);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var client=serverTls?new NativeQueryClient("localhost",fixture.Server.Port):NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate);
        var failure=await Record.ExceptionAsync(()=>client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
        Assert.True(failure is IOException or SocketException or AuthenticationException or InvalidDataException,failure?.ToString());
        Assert.False(timeout.IsCancellationRequested);
        Assert.Equal(0,fixture.Module.Calls);
    });

    [Fact]
    public Task AbruptTlsDisconnectRollsBackAndReleasesTheKey()=>ManagementTransport.WithServerCertificate(async certificate=>{
        using var fixture=new Fixture(certificate);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using(var socket=new TcpClient()){
            await socket.ConnectAsync("localhost",fixture.Server.Port,timeout.Token);
            using var stream=new SslStream(socket.GetStream());
            var policy=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.Online};policy.CustomTrustStore.Add(certificate);
            await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost="localhost",CertificateChainPolicy=policy},timeout.Token);
            var transaction=Guid.NewGuid();
            async Task Send(NativeQueryRequest request,bool row){
                await NativeFrame.WriteAsync(stream,NativeFrame.Request,MemoryPackSerializer.Serialize(request),timeout.Token);
                if(row){var result=await NativeFrame.ReadAsync(stream,timeout.Token);Assert.Equal(NativeFrame.Row,result!.Value.Kind);}
                var completed=await NativeFrame.ReadAsync(stream,timeout.Token);Assert.Equal(NativeFrame.Completed,completed!.Value.Kind);
            }
            await Send(new("$transaction","begin","fixture","",[],transaction),false);
            await Send(new("tls","insert","fixture",Fingerprint,MemoryPackSerializer.Serialize(7),transaction,NativeWireContract.For<int>(),NativeWireContract.For<int>()),true);
            // 取引の確定・取消を送らず、TLSの下のソケットを切断する。
            socket.Dispose();
        }
        await using var client=NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate);
        Assert.Equal(0,await client.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,7,timeout.Token));
        Assert.Equal(1,await client.ExecuteScalarAsync<int,int>("tls","insert","fixture",Fingerprint,7,timeout.Token));
        Assert.Equal(1,await client.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,7,timeout.Token));
        await fixture.Server.StopAsync();fixture.Server.Start();
        await using var restarted=NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate);
        Assert.Equal(1,await restarted.ExecuteScalarAsync<int,int>("tls","exists","fixture",Fingerprint,7,timeout.Token));
        Assert.True(certificate.HasPrivateKey);
    });

    [Fact]
    public Task StopCancelsAnIncompleteHandshakeAndAllowsRestart()=>ManagementTransport.WithServerCertificate(async certificate=>{
        using var fixture=new Fixture(certificate,maximumConnections:1);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using(var socket=new TcpClient()){
            await socket.ConnectAsync("localhost",fixture.Server.Port,timeout.Token);
            await socket.GetStream().WriteAsync(new byte[]{0x16,0x03},timeout.Token);
            await fixture.Server.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        fixture.Server.Start();
        await using var client=NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate);
        Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
    });

    [Fact]
    public Task PartialResultDisposalReleasesTheServerEnumeratorAndReconnectsWithTls()=>ManagementTransport.WithServerCertificate(async certificate=>{
        using var fixture=new Fixture(certificate);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var client=NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate);
        await foreach(var row in client.QueryAsync<int,int>("tls","stream","fixture",Fingerprint,0,timeout.Token)){Assert.Equal(0,row);break;}
        await fixture.Module.StreamDisposed.Task.WaitAsync(timeout.Token);
        Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
    });

    [Fact]
    public Task GatewayOptionsWireTheNativeTlsCertificate()=>ManagementTransport.WithServerCertificate(async certificate=>{
        using var fixture=new Fixture(null,start:false);
        using var gateway=new GatewayServer(fixture.Registry,new(){TdsPort=0,HttpPort=0,NativePort=0,NativeCertificate=certificate});
        gateway.Native.Register(fixture.Manifest,fixture.Module,fixture.Version);gateway.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var client=NativeQueryClient.ForTls("localhost",gateway.NativePort,certificate);
        Assert.Equal(8,await client.ExecuteScalarAsync<int,int>("tls","value","fixture",Fingerprint,7,timeout.Token));
    });

    [Fact]
    public Task TlsRequiresAPrivateKeyBeforeListening()=>ManagementTransport.WithServerCertificate(certificate=>{
        using var publicOnly=X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
        Assert.Throws<ArgumentException>(()=>NativeQueryServer.CreateTls(new(),publicOnly,0));
        Assert.Throws<ArgumentNullException>(()=>NativeQueryServer.CreateTls(new(),null!,0));
        return Task.CompletedTask;
    });

    [Theory][InlineData("empty")][InlineData("missing")][InlineData("public-only")][InlineData("invalid")]
    public Task InvalidCliCertificateIsRejectedBeforeOpeningStorage(string kind)=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var storage=Path.Combine(root,"db");var certificatePath=kind=="empty"?"":"native.pfx";
        if(kind=="public-only"){
            using var publicOnly=X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
            await File.WriteAllBytesAsync(Path.Combine(root,certificatePath),publicOnly.Export(X509ContentType.Pfx));
        }else if(kind=="invalid"){await File.WriteAllTextAsync(Path.Combine(root,certificatePath),"not a PFX");}
        var configuration=Path.Combine(root,"gateway.json");
        await File.WriteAllTextAsync(configuration,JsonSerializer.Serialize(new{StorageDirectory=storage,WorkingDirectory=Path.Combine(root,"work"),CacheDirectory=Path.Combine(root,"cache"),GeneratorAssemblyPath="not-needed-before-certificate-validation",NativeCertificatePath=certificatePath}));
        var failure=await Record.ExceptionAsync(()=>Seamless.Database.Server.Program.ServeGatewayAsync(["--gateway",configuration]));
        if(kind is "empty" or "public-only"){Assert.Contains("NativeCertificatePath",Assert.IsType<InvalidDataException>(failure).Message);}
        else if(kind=="missing"){Assert.True(failure is FileNotFoundException or System.Security.Cryptography.CryptographicException,failure?.ToString());}
        else{Assert.IsAssignableFrom<System.Security.Cryptography.CryptographicException>(failure);}
        Assert.False(Directory.Exists(storage));Assert.False(Directory.Exists(Path.Combine(root,"work")));Assert.False(Directory.Exists(Path.Combine(root,"cache")));
    });

    [Fact]
    public Task GatewayCommandPublishesTlsAndReopensTheSameDatabase()=>ManagementTransport.WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var password=Guid.NewGuid().ToString("N");var pfx=Path.Combine(root,"native.pfx");
        await File.WriteAllBytesAsync(pfx,certificate.Export(X509ContentType.Pfx,password));
        try{
            var dotnet=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
            var configuration=Path.Combine(root,"gateway.json");var ready=Path.Combine(root,"ready.json");var stop=Path.Combine(root,"stop");
            var config=new{
                StorageDirectory=Path.Combine(root,"db"),WorkingDirectory=Path.Combine(root,"w"),CacheDirectory=Path.Combine(root,"c"),DotNetExecutable=dotnet,
                GeneratorAssemblyPath=typeof(Seamless.Database.Generator.DatabaseSchemaGenerator).Assembly.Location,
                InitialCatalogs=new[]{new DatabaseCatalog("native_tls_lab",[new("dbo",[])])},NativeCertificatePath="native.pfx",
                DatabaseOptions=new{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20},
                FutureMetadata=new{Preserve="unknown-setting",Data=new[]{2,4,8}},
                Endpoints=new{
                    TdsPort=0,HttpPort=0,NativePort=0,Http=new{ListenAddress="127.0.0.1"},
                    NativeRecommendedConnectionPolicy=new{Lifetime="ReleaseWhenIdle",IdleTimeout=(string?)"00:00:05"},
                    NativeConnectionPolicyProfiles=new{batch=new{Lifetime="ReleaseAfterOperation",IdleTimeout=(string?)null}},
                    FutureEndpointSetting=true
                }
            };
            Guid? wal=null;
            var manager=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_UI_TEST_EXE");
            for(var attempt=0;attempt<2;attempt++){
                File.Delete(ready);File.Delete(stop);
                var expectedPolicy=new NativeConnectionPolicy(NativeConnectionLifetime.ReleaseWhenIdle,TimeSpan.FromSeconds(5));
                var expectedBatch=new NativeConnectionPolicy(NativeConnectionLifetime.ReleaseAfterOperation);
                var currentConfig=config;
                if(attempt==1){
                    expectedPolicy=new(NativeConnectionLifetime.ReleaseAfterOperation);
                    expectedBatch=new(NativeConnectionLifetime.KeepAlive);
                    currentConfig=config with{Endpoints=config.Endpoints with{
                        NativeRecommendedConnectionPolicy=new{Lifetime="ReleaseAfterOperation",IdleTimeout=(string?)null},
                        NativeConnectionPolicyProfiles=new{batch=new{Lifetime="KeepAlive",IdleTimeout=(string?)null}}
                    }};
                }
                if(attempt==0||manager is null){await File.WriteAllTextAsync(configuration,JsonSerializer.Serialize(currentConfig));}
                var start=new ProcessStartInfo(dotnet){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};
                start.Environment["SEAMLESS_GATEWAY_NATIVE_CERT_PASSWORD"]=password;
                if(Environment.GetEnvironmentVariable("SEAMLESS_TEST_PACKAGED_GATEWAY") is{} packaged){Assert.True(Path.IsPathFullyQualified(packaged)&&File.Exists(packaged));start.ArgumentList.Add(packaged);}
                else{start.ArgumentList.Add(typeof(CrashProbe).Assembly.Location);start.ArgumentList.Add("--gateway-cli");}
                foreach(var argument in new[]{"--gateway",configuration,"--ready-file",ready,"--stop-file",stop}){start.ArgumentList.Add(argument);}
                using var process=Process.Start(start)!;var created=process.StartTime.ToUniversalTime();var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(110));
                try{
                    while(!File.Exists(ready)&&!process.HasExited){await Task.Delay(50,timeout.Token);}
                    if(process.HasExited){Assert.Fail((await output)+(await error));}
                    using var record=JsonDocument.Parse(await File.ReadAllTextAsync(ready,timeout.Token));var notification=record.RootElement;
                    Assert.Equal(process.Id,notification.GetProperty("Pid").GetInt32());Assert.False(process.HasExited);
                    var publishedCreation=notification.GetProperty("CreatedUtc").GetDateTime();Assert.Equal(DateTimeKind.Utc,publishedCreation.Kind);
                    // LinuxのUTC換算は時刻補正の影響を受ける。親が保持する生存中の子プロセスとPIDで所属を照合する。
                    if(!OperatingSystem.IsLinux()){Assert.Equal(created,publishedCreation);}
                    Assert.True(notification.GetProperty("NativeTls").GetBoolean());
                    var currentWal=notification.GetProperty("GatewayWalId").GetGuid();Assert.NotEqual(Guid.Empty,currentWal);if(wal.HasValue){Assert.Equal(wal.Value,currentWal);}wal=currentWal;
                    var port=notification.GetProperty("NativePort").GetInt32();
                    await using(var client=NativeQueryClient.ForTlsWithServerConnectionPolicy("localhost",port,certificate)){
                        await client.ConnectAsync(timeout.Token);
                        Assert.Equal(expectedPolicy,client.ServerRecommendedConnectionPolicy);
                        Assert.Equal(expectedPolicy,client.ConnectionPolicy);
                        await using var transaction=await client.BeginTransactionAsync("native_tls_lab",timeout.Token);await transaction.RollbackAsync(timeout.Token);
                    }
                    await using(var client=NativeQueryClient.ForTls("localhost",port,certificate,new(NativeConnectionLifetime.KeepAlive))){
                        await client.ConnectAsync(timeout.Token);
                        Assert.Null(client.ServerRecommendedConnectionPolicy);
                        Assert.Equal(NativeConnectionLifetime.KeepAlive,client.ConnectionPolicy.Lifetime);
                        await using var transaction=await client.BeginTransactionAsync("native_tls_lab",timeout.Token);await transaction.RollbackAsync(timeout.Token);
                    }
                    using(var pool=NativeConnectionPool.ForTls("localhost",port,new(){UseServerConnectionPolicy=true,ServerConnectionPolicyProfile="batch"},certificate)){
                        await using var client=await pool.RentAsync(timeout.Token);
                        await client.ConnectAsync(timeout.Token);
                        Assert.Equal(expectedBatch,client.ServerRecommendedConnectionPolicy);
                        await using var transaction=await client.BeginTransactionAsync("native_tls_lab",timeout.Token);
                        await transaction.RollbackAsync(timeout.Token);
                    }
                    if(manager is not null){
                        if(attempt==0){
                            await VerifyManagerAsync(manager,timeout.Token);
                            await using var stillRunning=NativeQueryClient.ForTlsWithServerConnectionPolicy("localhost",port,certificate);
                            await stillRunning.ConnectAsync(timeout.Token);
                            Assert.Equal(expectedPolicy,stillRunning.ServerRecommendedConnectionPolicy);
                        }else{
                            using var pool=NativeConnectionPool.ForTls("localhost",port,new(){UseServerConnectionPolicy=true,ServerConnectionPolicyProfile="interactive"},certificate);
                            await using var client=await pool.RentAsync(timeout.Token);
                            await client.ConnectAsync(timeout.Token);
                            Assert.Equal(new NativeConnectionPolicy(NativeConnectionLifetime.ReleaseWhenIdle,TimeSpan.FromSeconds(15)),client.ServerRecommendedConnectionPolicy);
                        }
                    }
                    await File.WriteAllTextAsync(stop,"stop",timeout.Token);await process.WaitForExitAsync(timeout.Token);
                    var waitReturned=Stopwatch.GetTimestamp();var nativeSignal=RecoveryLockDiagnostic.ProcessSignal(process);
                    await File.WriteAllTextAsync(Path.Combine(root,$"server-{attempt}.handoff.json"),JsonSerializer.Serialize(new{Attempt=attempt,ChildPid=process.Id,ChildCreatedUtc=created,Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,process.ExitCode,NativeSignalAfterAsync=nativeSignal,StopwatchFrequency=Stopwatch.Frequency,WaitReturned=waitReturned,GatewayWalId=currentWal}),timeout.Token);
                    Assert.True(process.ExitCode==0,(await output)+(await error));
                }finally{
                    if(!process.HasExited&&process.StartTime.ToUniversalTime()==created){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}
                    await File.WriteAllTextAsync(Path.Combine(root,$"server-{attempt}.stdout.log"),await output);await File.WriteAllTextAsync(Path.Combine(root,$"server-{attempt}.stderr.log"),await error);
                }
            }

            async Task VerifyManagerAsync(string executable,CancellationToken token){
                var before=await File.ReadAllTextAsync(configuration,token);
                var request=Path.Combine(root,"gateway-policy-ui.json");
                var logs=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_LOG")??throw new InvalidOperationException("Missing Manager evidence directory.");
                var image=Path.Combine(logs,"gateway-native-policy-settings.png");
                await File.WriteAllTextAsync(request,JsonSerializer.Serialize(new{
                    ホスト="localhost",ポート=0,利用者="",証明書="",定義ID="",Gateway起動設定=configuration
                }),token);
                var launch=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
                launch.Environment["SEAMLESS_MANAGEMENT_HOME"]=Path.Combine(root,"ui");
                launch.Environment["SEAMLESS_MANAGEMENT_ROOT"]=root;
                foreach(var argument in new[]{"--smoke",request,image}){launch.ArgumentList.Add(argument);}
                using var process=Process.Start(launch)!;
                try{await process.WaitForExitAsync(token);}
                finally{if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}}
                Assert.True(process.ExitCode==0,File.Exists(image+".error.txt")?File.ReadAllText(image+".error.txt"):"Manager exit="+process.ExitCode);
                Assert.True(new FileInfo(image).Length>0);
                using var evidence=JsonDocument.Parse(await File.ReadAllTextAsync(image+".result.json",token));
                var result=evidence.RootElement;
                Assert.Equal(process.Id,result.GetProperty("pid").GetInt32());
                Assert.Equal(Path.GetFullPath(executable),result.GetProperty("実行ファイル").GetString(),ignoreCase:true);
                foreach(var key in new[]{"保存","競合拒否","無効入力拒否"}){Assert.True(result.GetProperty(key).GetBoolean());}
                var after=await File.ReadAllTextAsync(configuration,token);
                var baseline=WithoutPolicy(before);
                var actual=WithoutPolicy(after);
                Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(baseline,actual));
            }

            static System.Text.Json.Nodes.JsonObject WithoutPolicy(string json){
                var document=System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
                var endpoints=document["Endpoints"]!.AsObject();
                endpoints.Remove("NativeRecommendedConnectionPolicy");
                endpoints.Remove("NativeConnectionPolicyProfiles");
                return document;
            }
        }finally{File.Delete(pfx);}
    });

    [Theory]
    [InlineData("Unknown",null,typeof(JsonException))]
    [InlineData("ReleaseWhenIdle","00:00:00",typeof(ArgumentOutOfRangeException))]
    [InlineData("KeepAlive","00:00:05",typeof(ArgumentException))]
    public async Task InvalidCliConnectionPolicyIsRejectedBeforeOpeningStorage(string lifetime,string? idleTimeout,Type errorType){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var storage=Path.Combine(root,"db");var work=Path.Combine(root,"work");var cache=Path.Combine(root,"cache");
        var configuration=Path.Combine(root,"gateway.json");
        await File.WriteAllTextAsync(configuration,JsonSerializer.Serialize(new{
            StorageDirectory=storage,WorkingDirectory=work,CacheDirectory=cache,GeneratorAssemblyPath="not-needed-before-options-validation",
            Endpoints=new{NativeRecommendedConnectionPolicy=new{Lifetime=lifetime,IdleTimeout=idleTimeout}}
        }));
        await Assert.ThrowsAsync(errorType,()=>Seamless.Database.Server.Program.ServeGatewayAsync(["--gateway",configuration]));
        Assert.False(Directory.Exists(storage));
        Assert.False(Directory.Exists(work));
        Assert.False(Directory.Exists(cache));
    }

    private sealed class Fixture:IDisposable{
        private readonly AssemblyLoadContext context=new("NativeTls_"+Guid.NewGuid(),true);
        private readonly DatabaseContainer database;
        public Module Module{get;}=new();
        public DatabaseRegistry Registry{get;}=new();
        public NativeQueryServer Server{get;}
        public string Version{get;}=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        public NativeModuleManifest Manifest{get;}
        public Fixture(X509Certificate2? certificate,bool start=true,int? maximumConnections=null,NativeGatewayAuthorization? authorization=null,IPEndPoint? authenticatedEndpoint=null,NativeServerLimits? authenticatedLimits=null){
            var compiled=new RuntimeCompiler().Compile(NativeWorkflow.Database().Source);
            var assembly=this.context.LoadFromStream(new MemoryStream(compiled.AssemblyBytes));
            var options=DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"))};
            this.database=(DatabaseContainer)Activator.CreateInstance(assembly.GetType("Model.Db")!,options)!;
            this.Registry.Register("fixture",new InProcessDatabaseAdapter(this.database),schemaFingerprint:Fingerprint);
            if(authorization is not null){
                if(authenticatedEndpoint is not null){this.Server=NativeQueryServer.CreateAuthenticated(this.Registry,authenticatedEndpoint,certificate!,authorization,authenticatedLimits??new());}
                else{this.Server=NativeQueryServer.CreateAuthenticated(this.Registry,certificate!,authorization,authenticatedLimits??new(),default);}
            }else{
                if(authenticatedEndpoint is not null){throw new ArgumentException("An explicit fixture endpoint requires authorization.");}
                this.Server=certificate is null?new(this.Registry,0,maximumConnections):NativeQueryServer.CreateTls(this.Registry,certificate,0,maximumConnections);
            }
            this.Manifest=new("tls","","","",this.Version,[Definition("value"),Definition("insert",false),Definition("exists"),Definition("stream")]);
            this.Server.Register(this.Manifest,this.Module,this.Version);
            if(start){this.Server.Start();}
        }
        private static NativeQueryDefinition Definition(string id,bool readOnly=true)=>new(id,"fixture",Fingerprint,"int","int",readOnly){ArgumentsContract=NativeWireContract.For<int>(),ResultContract=NativeWireContract.For<int>()};
        public void Dispose(){this.Server.Dispose();this.database.Dispose();this.context.Unload();}
    }
    private sealed class Module:INativeQueryModule{
        public int Calls;
        public TaskCompletionSource StreamDisposed{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            Interlocked.Increment(ref this.Calls);
            var id=MemoryPackSerializer.Deserialize<int>(arguments.Span);
            if(queryId=="value"){yield return MemoryPackSerializer.Serialize(id+1);yield break;}
            if(queryId=="stream"){
                try{for(var index=0;index<1_000_000;index++){context.CancellationToken.ThrowIfCancellationRequested();yield return MemoryPackSerializer.Serialize(index);}}
                finally{this.StreamDisposed.TrySetResult();}
                yield break;
            }
            dynamic database=context.Database;
            if(queryId=="insert"){
                dynamic row=Activator.CreateInstance(context.Database.GetType().Assembly.GetType("Model.Tables.Row")!,Activator.CreateInstance(context.Database.GetType().Assembly.GetType("Model.Keys.Row")!,id),9)!;
                if(!(bool)database.Rows.Add(row)){throw new InvalidOperationException("The previous transaction did not release its key.");}
                yield return MemoryPackSerializer.Serialize(1);yield break;
            }
            if(queryId!="exists"){throw new InvalidOperationException("Unknown TLS fixture query.");}
            dynamic key=Activator.CreateInstance(context.Database.GetType().Assembly.GetType("Model.Keys.Row")!,id)!;
            var result=(bool)database.Rows.ContainsKey(key)?1:0;yield return MemoryPackSerializer.Serialize(result);
        }
    }
}
