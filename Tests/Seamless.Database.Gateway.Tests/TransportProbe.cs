using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using MemoryPack;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;

namespace Seamless.Database.Gateway.Tests;

// 正式NativeQueryServer/Clientを別プロセスで比較する。独立echoサーバーの数値ではない。
internal static class TransportProbe{
    private const string Fingerprint="transport-probe-v1";
    private sealed record Snapshot(long PrivateBytes,long WorkingSet,long Allocated,long HeapBytes,int Gen0,int Gen1,int Gen2,double CpuMs,int Threads);
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
        if(args.Length>1&&args[1]=="server"){return await ServeAsync();}
        var results=new List<object>();var incomplete=false;
        try{
            using var process=new Process{StartInfo=new(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
            if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase)){process.StartInfo.ArgumentList.Add(typeof(TransportProbe).Assembly.Location);}
            process.StartInfo.ArgumentList.Add("--transport-probe");process.StartInfo.ArgumentList.Add("server");process.Start();var errors=process.StandardError.ReadToEndAsync();
            try{
                using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(4));var token=timeout.Token;
                var ready=JsonSerializer.Deserialize<JsonElement>(await process.StandardOutput.ReadLineAsync(token)??throw new IOException("Probe server did not start."));var port=ready.GetProperty("Port").GetInt32();
                async Task<Snapshot> Capture(){await process.StandardInput.WriteLineAsync("snapshot");await process.StandardInput.FlushAsync(token);return JsonSerializer.Deserialize<Snapshot>(await process.StandardOutput.ReadLineAsync(token)??throw new IOException("Probe server exited."))!;}

                await using(var client=new NativeQueryClient("127.0.0.1",port)){
                    for(var index=0;index<100;index++){await client.ExecuteScalarAsync<int,int>("probe","small","fixture",Fingerprint,index,token);}
                    var before=await Capture();var elapsed=new double[2000];var clock=Stopwatch.StartNew();
                    for(var index=0;index<elapsed.Length;index++){var start=Stopwatch.GetTimestamp();var value=await client.ExecuteScalarAsync<int,int>("probe","small","fixture",Fingerprint,index,token);if(value!=index+1){throw new InvalidDataException("Probe result mismatch.");}elapsed[index]=Stopwatch.GetElapsedTime(start).TotalMicroseconds;}
                    clock.Stop();var after=await Capture();Array.Sort(elapsed);results.Add(new{Case="small-persistent",Count=elapsed.Length,Seconds=clock.Elapsed.TotalSeconds,RequestsPerSecond=elapsed.Length/clock.Elapsed.TotalSeconds,P50Us=elapsed[999],P95Us=elapsed[1899],P99Us=elapsed[1979],Before=before,After=after});
                    before=await Capture();clock.Restart();long bytes=0;await foreach(var row in client.QueryAsync<int,byte[]>("probe","large","fixture",Fingerprint,1024,token)){bytes+=row.Length;}clock.Stop();after=await Capture();results.Add(new{Case="large-stream",Bytes=bytes,Seconds=clock.Elapsed.TotalSeconds,MiBPerSecond=bytes/(1024.0*1024)/clock.Elapsed.TotalSeconds,Before=before,After=after});
                }
                {var before=await Capture();var elapsed=new double[200];var clock=Stopwatch.StartNew();for(var index=0;index<elapsed.Length;index++){var start=Stopwatch.GetTimestamp();await using var client=new NativeQueryClient("127.0.0.1",port);await client.ExecuteScalarAsync<int,int>("probe","small","fixture",Fingerprint,index,token);elapsed[index]=Stopwatch.GetElapsedTime(start).TotalMicroseconds;}clock.Stop();var after=await Capture();Array.Sort(elapsed);results.Add(new{Case="reconnect-sequential",Count=elapsed.Length,Seconds=clock.Elapsed.TotalSeconds,P50Us=elapsed[99],P95Us=elapsed[189],P99Us=elapsed[197],Before=before,After=after});}
                var idle=new List<TcpClient>();try{
                    foreach(var target in new[]{100,1000,10000}){
                        var before=await Capture();var failures=new List<string>();var elapsed=Stopwatch.StartNew();var pending=new List<TcpClient>();var gate=new object();
                        await Parallel.ForEachAsync(Enumerable.Range(0,target-idle.Count),new ParallelOptions{MaxDegreeOfParallelism=64,CancellationToken=token},async(_,cancellation)=>{var socket=new TcpClient();using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(5));try{await socket.ConnectAsync(IPAddress.Loopback,port,deadline.Token);lock(gate){pending.Add(socket);}}catch(Exception error){socket.Dispose();lock(gate){failures.Add(error.GetType().Name+": "+error.Message);}}});idle.AddRange(pending);elapsed.Stop();var after=await Capture();await Task.Delay(1000,token);var idleAfter=await Capture();results.Add(new{Case="idle-connect-burst",Target=target,Established=idle.Count,Failures=failures.Count,FirstFailure=failures.FirstOrDefault(),Seconds=elapsed.Elapsed.TotalSeconds,Before=before,After=after,IdleAfter=idleAfter});if(failures.Count!=0){incomplete=true;break;}
                    }
                }finally{foreach(var socket in idle){socket.Dispose();}}
                await process.StandardInput.WriteLineAsync("stop");await process.StandardInput.FlushAsync(token);await process.WaitForExitAsync(token);var finalErrors=await errors;
                Console.WriteLine(JsonSerializer.Serialize(new{Scope="loopback, separate client/server, native plaintext unchanged",ServerPid=process.Id,ServerCreatedUtc=process.StartTime.ToUniversalTime(),ServerExitCode=process.ExitCode,ServerError=finalErrors,Runtime=Environment.Version.ToString(),ProcessorCount=Environment.ProcessorCount,AllConnectionsEstablished=!incomplete,Results=results}));if(incomplete){return 1;}return process.ExitCode;
            }finally{if(!process.HasExited){await process.StandardInput.WriteLineAsync("stop");await process.StandardInput.FlushAsync();await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));}}
        }catch(Exception error){Console.Error.WriteLine(error);Console.WriteLine(JsonSerializer.Serialize(new{AllConnectionsEstablished=false,Error=error.ToString(),Results=results}));return 1;}
    }
    private static async Task<int> ServeAsync(){
        using var database=new RuntimeSqlGeneratedRoot(new(){Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});var databases=new DatabaseRegistry();databases.Register("fixture",new InProcessDatabaseAdapter(database),schemaFingerprint:Fingerprint);
        using var server=new NativeQueryServer(databases,0);var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];server.Register(new("probe","","","",version,[new("small","fixture",Fingerprint,"int","int"),new("large","fixture",Fingerprint,"int","byte[]")]),new Module(),version);server.Start();Console.WriteLine(JsonSerializer.Serialize(new{server.Port}));
        while(await Console.In.ReadLineAsync() is{} command){if(command=="stop"){break;}if(command!="snapshot"){continue;}using var process=Process.GetCurrentProcess();Console.WriteLine(JsonSerializer.Serialize(new Snapshot(process.PrivateMemorySize64,process.WorkingSet64,GC.GetTotalAllocatedBytes(),GC.GetTotalMemory(false),GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2),process.TotalProcessorTime.TotalMilliseconds,process.Threads.Count)));}
        await server.StopAsync();return 0;
    }
}
