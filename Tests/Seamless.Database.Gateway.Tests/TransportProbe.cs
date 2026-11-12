using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using MemoryPack;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;

namespace Seamless.Database.Gateway.Tests;

// 正式NativeQueryServer/Clientを別プロセスで比較する。独立echoサーバーの数値ではない。
internal static partial class TransportProbe{
    private const string Fingerprint="transport-probe-v1";
    private sealed record Snapshot(long PrivateBytes,long WorkingSet,long Allocated,long HeapBytes,int Gen0,int Gen1,int Gen2,double CpuMs,int Threads,
        int? AuthenticatedSessions=null,int? DeniedPrivateQueries=null,int? ActiveAuthenticatedSessions=null,
        int? MembershipRefreshes=null,int? IssuerMembershipChecks=null,int? SharedConnections=null,int? SessionsRefreshedTwice=null);
    private sealed record ConnectFailure(int Attempt,int Established,DateTime Utc,double ElapsedMilliseconds,string Stage,string ExceptionType,int? NativeErrorCode,string? SocketError,string Message);
    private sealed class Module:INativeQueryModule{
        private readonly byte[] large=MemoryPackSerializer.Serialize(new byte[32768]);
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            var count=MemoryPackSerializer.Deserialize<int>(arguments.Span);
            if(queryId=="small"){yield return MemoryPackSerializer.Serialize(count+1);yield break;}
            if(queryId!="large"){throw new InvalidOperationException("Unknown probe query.");}
            for(var index=0;index<count;index++){context.CancellationToken.ThrowIfCancellationRequested();yield return this.large;}
        }
    }
    internal static async Task<int> RunAsync(string[] args){
        if(args.Length>1&&args[1]=="server"){return await ServeAsync(null);}
        if(args.Length==3&&args[1]=="server-mixed"&&int.TryParse(args[2],out var mixedLimit)&&mixedLimit is >=1 and <=10000){
            return await ServeAsync(mixedLimit);
        }
        var results=new List<object>();var incomplete=false;
        try{
            var parallelism=64;
            var legacyMixed=args.Length==4&&args[2]=="mixed-legacy";
            var legacyTarget=0;
            var explicitBind=args.Length==3&&args[2]=="idle-bind";
            var idleOnly=args.Length==3&&(args[2]=="idle"||explicitBind);
            var churnInterval=args.Length==6?args[2] switch{"auth-churn"=>0,"auth-churn-long"=>20,_=>(int?)null}:null;
            var churn=churnInterval.HasValue;
            var refreshCycles=args.Length==6?args[2] switch{"auth-refresh"=>1,"auth-refresh-twice"=>2,_=>0}:0;
            var refresh=refreshCycles>0;
            var mixedLong=args.Length==6&&args[2]=="auth-https-tds-long";
            var mixedTds=mixedLong||args.Length==6&&args[2]=="auth-https-tds";
            var mixed=mixedTds||args.Length==6&&args[2]=="auth-https";
            var multiIdentity=churn||refresh||mixed||args.Length==6&&args[2]=="auth-identities";
            var sustained=args.Length==6&&args[2]=="auth-identities"||args.Length==5&&args[2]=="auth-rate";
            var capacity=churn||refresh||mixed||sustained||args.Length==5&&args[2]=="auth-capacity";
            var authenticated=capacity||args.Length==4&&args[2]=="policy-auth";
            var policyOnly=authenticated||args.Length==3&&args[2]=="policy";
            var capacityTarget=0;
            var identityCount=1;
            if((args.Length>3&&!authenticated&&!legacyMixed)||(args.Length==3&&!idleOnly&&!policyOnly)||(args.Length>=2&&(!int.TryParse(args[1],out parallelism)||parallelism is<1 or>64))||
                legacyMixed&&(parallelism>16||!int.TryParse(args[3],out legacyTarget)||legacyTarget is <1 or >10000)||
                policyOnly&&!capacity&&parallelism>8||capacity&&(parallelism>16||!int.TryParse(args[4],out capacityTarget)||capacityTarget is <1 or >10000)||
                multiIdentity&&(!int.TryParse(args[5],out identityCount)||identityCount is <2 or >100||identityCount>capacityTarget)){
                throw new ArgumentException("Usage: --transport-probe <1..16> mixed-legacy <1..10000 connections>, auth-identities|auth-churn|auth-churn-long|auth-refresh|auth-refresh-twice|auth-https|auth-https-tds|auth-https-tds-long <Management.Tests.dll> <1..10000 connections> <2..100 identities>, or an existing transport probe mode.");
            }
            var executable=Environment.ProcessPath!;
            using var process=new Process{StartInfo=new(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
            var dotnet=string.Equals(Path.GetFileNameWithoutExtension(executable),"dotnet",StringComparison.OrdinalIgnoreCase);
            if(authenticated){
                if(!dotnet||!Path.IsPathFullyQualified(args[3])||!File.Exists(args[3])){throw new ArgumentException("Authenticated comparison requires dotnet and an absolute test server assembly path.");}
                process.StartInfo.ArgumentList.Add(args[3]);process.StartInfo.ArgumentList.Add("--native-policy-server");
                if(capacity){process.StartInfo.ArgumentList.Add(args[4]);}
                if(multiIdentity){process.StartInfo.ArgumentList.Add(args[5]);}
                if(mixed){process.StartInfo.ArgumentList.Add(mixedTds?"https-tds":"https");}
            }else{
                if(dotnet){process.StartInfo.ArgumentList.Add(typeof(TransportProbe).Assembly.Location);}
                process.StartInfo.ArgumentList.Add("--transport-probe");
                process.StartInfo.ArgumentList.Add(legacyMixed?"server-mixed":"server");
                if(legacyMixed){process.StartInfo.ArgumentList.Add(args[3]);}
            }
            process.Start();var errors=process.StandardError.ReadToEndAsync();
            try{
                using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(4+refreshCycles*5+(churnInterval>0?10:0)+(mixedLong?6:0)));var token=timeout.Token;
                var ready=JsonSerializer.Deserialize<JsonElement>(await process.StandardOutput.ReadLineAsync(token)??throw new IOException("Probe server did not start."));var port=ready.GetProperty("Port").GetInt32();
                async Task<Snapshot> Capture(){await process.StandardInput.WriteLineAsync("snapshot");await process.StandardInput.FlushAsync(token);return JsonSerializer.Deserialize<Snapshot>(await process.StandardOutput.ReadLineAsync(token)??throw new IOException("Probe server exited."))!;}

                if(legacyMixed){
                    incomplete=!await MeasureLegacyMixedAsync(port,ready.GetProperty("TdsPort").GetInt32(),ready.GetProperty("HttpPort").GetInt32(),
                        legacyTarget,parallelism,Capture,results,token);
                }else if(policyOnly){
                    if(authenticated){
                        using var certificate=X509CertificateLoader.LoadCertificate(ready.GetProperty("Certificate").GetBytesFromBase64());
                        var authority=ready.GetProperty("AuthorityId").GetGuid();
                        var identities=ready.GetProperty("Credentials").EnumerateArray().Select(credential=>{
                            var principal=new NativeAuthenticatedPrincipal(authority,credential.GetProperty("Subject").GetString()!);
                            var accessToken=credential.GetProperty("Token").GetString()!;
                            return new PolicyProbeIdentity(principal,_=>ValueTask.FromResult(accessToken),credential.GetProperty("Query").GetString()!);
                        }).ToArray();
                        if(identities.Length!=identityCount||identities.Select(static identity=>identity.Principal).Distinct().Count()!=identityCount||
                            identities.Select(static identity=>identity.Query).Distinct(StringComparer.Ordinal).Count()!=identityCount){
                            throw new InvalidDataException("Unexpected probe identity setup.");
                        }
                        var auth=new PolicyProbeAuth(certificate,ready.GetProperty("Fingerprint").GetString()!,identities);
                        if(capacity){
                            if(ready.GetProperty("MaximumConnections").GetInt32()!=capacityTarget){throw new InvalidDataException("The server connection limit does not match the probe target.");}
                            var httpsPort=0;
                            if(mixed&&(!ready.TryGetProperty("HttpsPort",out var https)||!https.TryGetInt32(out httpsPort)||httpsPort<=0||httpsPort==port)){
                                throw new InvalidDataException("The protected HTTPS listener did not start.");
                            }
                            var tdsPort=0;
                            if(mixedTds&&(!ready.TryGetProperty("TdsPort",out var tds)||!tds.TryGetInt32(out tdsPort)||tdsPort<=0||tdsPort==port||tdsPort==httpsPort)){
                                throw new InvalidDataException("The protected TDS listener did not start.");
                            }
                            incomplete=!await MeasureAuthenticatedCapacityAsync(port,parallelism,capacityTarget,Capture,results,auth,sustained,churnInterval,refreshCycles,httpsPort,tdsPort,mixedLong,token);
                        }else{await MeasurePoliciesAsync(port,parallelism,Capture,results,token,auth);}
                    }else{await MeasurePoliciesAsync(port,parallelism,Capture,results,token);}
                }else{
                if(!idleOnly){
                    await using(var client=new NativeQueryClient("127.0.0.1",port)){
                        for(var index=0;index<100;index++){await client.ExecuteScalarAsync<int,int>("probe","small","fixture",Fingerprint,index,token);}
                        var before=await Capture();var elapsed=new double[2000];var clock=Stopwatch.StartNew();
                        for(var index=0;index<elapsed.Length;index++){var start=Stopwatch.GetTimestamp();var value=await client.ExecuteScalarAsync<int,int>("probe","small","fixture",Fingerprint,index,token);if(value!=index+1){throw new InvalidDataException("Probe result mismatch.");}elapsed[index]=Stopwatch.GetElapsedTime(start).TotalMicroseconds;}
                        clock.Stop();var after=await Capture();Array.Sort(elapsed);results.Add(new{Case="small-persistent",Count=elapsed.Length,Seconds=clock.Elapsed.TotalSeconds,RequestsPerSecond=elapsed.Length/clock.Elapsed.TotalSeconds,P50Us=elapsed[999],P95Us=elapsed[1899],P99Us=elapsed[1979],Before=before,After=after});
                        before=await Capture();clock.Restart();long bytes=0;await foreach(var row in client.QueryAsync<int,byte[]>("probe","large","fixture",Fingerprint,1024,token)){bytes+=row.Length;}clock.Stop();after=await Capture();results.Add(new{Case="large-stream",Bytes=bytes,Seconds=clock.Elapsed.TotalSeconds,MiBPerSecond=bytes/(1024.0*1024)/clock.Elapsed.TotalSeconds,Before=before,After=after});
                    }
                    {var before=await Capture();var elapsed=new double[200];var clock=Stopwatch.StartNew();for(var index=0;index<elapsed.Length;index++){var start=Stopwatch.GetTimestamp();await using var client=new NativeQueryClient("127.0.0.1",port);await client.ExecuteScalarAsync<int,int>("probe","small","fixture",Fingerprint,index,token);elapsed[index]=Stopwatch.GetElapsedTime(start).TotalMicroseconds;}clock.Stop();var after=await Capture();Array.Sort(elapsed);results.Add(new{Case="reconnect-sequential",Count=elapsed.Length,Seconds=clock.Elapsed.TotalSeconds,P50Us=elapsed[99],P95Us=elapsed[189],P99Us=elapsed[197],Before=before,After=after});}
                }else{
                }
                var idle=new List<TcpClient>();try{
                    foreach(var target in new[]{100,1000,10000}){
                        var before=await Capture();var failures=new List<ConnectFailure>();var elapsed=Stopwatch.StartNew();var pending=new List<TcpClient>();var gate=new object();
                        try{await Parallel.ForEachAsync(Enumerable.Range(idle.Count+1,target-idle.Count),new ParallelOptions{MaxDegreeOfParallelism=parallelism,CancellationToken=token},async(attempt,cancellation)=>{
                            var socket=new TcpClient();
                            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                            deadline.CancelAfter(TimeSpan.FromSeconds(5));
                            var stage="connect";
                            try{
                                if(explicitBind){
                                    stage="bind";
                                    socket.Client.Bind(new IPEndPoint(IPAddress.Loopback,0));
                                    stage="connect";
                                }else{
                                }
                                await socket.ConnectAsync(IPAddress.Loopback,port,deadline.Token);
                                lock(gate){pending.Add(socket);}
                            }catch(Exception error){
                                socket.Dispose();
                                var socketError=error as SocketException;
                                lock(gate){failures.Add(new(attempt,idle.Count+pending.Count,DateTime.UtcNow,elapsed.Elapsed.TotalMilliseconds,stage,error.GetType().Name,socketError?.NativeErrorCode,socketError?.SocketErrorCode.ToString(),error.Message));}
                            }
                        });}finally{idle.AddRange(pending);}
                        elapsed.Stop();var after=await Capture();await Task.Delay(1000,token);var idleAfter=await Capture();
                        results.Add(new{Case="idle-connect-burst",Target=target,Established=idle.Count,Failures=failures.Count,FailureDetails=failures,Seconds=elapsed.Elapsed.TotalSeconds,Before=before,After=after,IdleAfter=idleAfter});
                        if(failures.Count!=0){incomplete=true;break;}
                    }
                }finally{foreach(var socket in idle){socket.Dispose();}}
                }
                await process.StandardInput.WriteLineAsync("stop");await process.StandardInput.FlushAsync(token);await process.WaitForExitAsync(token);var finalErrors=await errors;
                Console.WriteLine(JsonSerializer.Serialize(new{Scope="loopback, separate client/server, native RPC",ServerPid=process.Id,ServerCreatedUtc=process.StartTime.ToUniversalTime(),ServerExitCode=process.ExitCode,ServerError=finalErrors,Runtime=Environment.Version.ToString(),ProcessorCount=Environment.ProcessorCount,ConnectParallelism=parallelism,IdleOnly=idleOnly,PolicyOnly=policyOnly&&!capacity,Authenticated=authenticated,CapacityTarget=capacityTarget,IdentityCount=identityCount,ExplicitBind=explicitBind,AllConnectionsEstablished=!incomplete,Results=results}));if(incomplete){return 1;}return process.ExitCode;
            }finally{if(!process.HasExited){await process.StandardInput.WriteLineAsync("stop");await process.StandardInput.FlushAsync();await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));}}
        }catch(Exception error){Console.Error.WriteLine(error);Console.WriteLine(JsonSerializer.Serialize(new{AllConnectionsEstablished=false,Error=error.ToString(),Results=results}));return 1;}
    }
    private static async Task<int> ServeAsync(int? mixedLimit){
        using var database=new RuntimeSqlGeneratedRoot(new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});var databases=new DatabaseRegistry();databases.Register("fixture",new InProcessDatabaseAdapter(database),schemaFingerprint:Fingerprint);
        using var gateway=mixedLimit is{} maximum?new GatewayServer(databases,new(){
            NativePort=0,TdsPort=0,HttpPort=0,MaximumConnections=maximum+2,Http=new(){ListenAddress=IPAddress.Loopback}
        }):null;
        using var standalone=gateway is null?new NativeQueryServer(databases,0):null;
        var server=gateway?.Native??standalone!;
        var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];server.Register(new("probe","","","",version,[new("small","fixture",Fingerprint,"int","int"),new("large","fixture",Fingerprint,"int","byte[]")]),new Module(),version);
        if(gateway is null){server.Start();}
        else{gateway.Start();}
        Console.WriteLine(JsonSerializer.Serialize(new{server.Port,TdsPort=gateway?.TdsPort??0,HttpPort=gateway?.HttpPort??0}));
        while(await Console.In.ReadLineAsync() is{} command){if(command=="stop"){break;}if(command!="snapshot"){continue;}using var process=Process.GetCurrentProcess();Console.WriteLine(JsonSerializer.Serialize(new Snapshot(process.PrivateMemorySize64,process.WorkingSet64,GC.GetTotalAllocatedBytes(),GC.GetTotalMemory(false),GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2),process.TotalProcessorTime.TotalMilliseconds,process.Threads.Count,SharedConnections:gateway?.SharedConnectionCount)));}
        if(gateway is null){await server.StopAsync();}
        else{await gateway.StopAsync();}
        return 0;
    }
}
