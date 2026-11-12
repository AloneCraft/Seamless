using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Tests;

internal static partial class TransportProbe{
    private static async Task<bool> MeasureLegacyMixedAsync(int nativePort,int tdsPort,int httpPort,int target,int parallelism,
        Func<Task<Snapshot>> capture,List<object> results,CancellationToken token){
        if(nativePort<=0||tdsPort<=0||httpPort<=0){throw new InvalidDataException("The three compatibility listeners did not start.");}
        var limits=new ParallelOptions{MaxDegreeOfParallelism=parallelism,CancellationToken=token};
        var clients=new NativeQueryClient?[target];
        var tdsOptions=new SqlConnectionStringBuilder{
            DataSource=$"tcp:127.0.0.1,{tdsPort}",InitialCatalog="fixture",UserID="sa",Password="test",
            Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectRetryCount=0,ConnectTimeout=5
        };
        await using var tds=new SqlConnection(tdsOptions.ConnectionString);
        using var http=new HttpClient(new SocketsHttpHandler{UseProxy=false,MaxConnectionsPerServer=1}){
            BaseAddress=new($"http://127.0.0.1:{httpPort}"),Timeout=TimeSpan.FromSeconds(10)
        };
        await tds.OpenAsync(token);
        await ReadTdsAsync(token);
        await ReadHttpAsync(token);
        try{
            var opened=0;
            foreach(var stage in new[]{100,1000,3000,6000}.Where(count=>count<target).Append(target)){
                var started=Stopwatch.GetTimestamp();
                await Parallel.ForEachAsync(Enumerable.Range(opened,stage-opened),limits,async(index,cancellation)=>{
                    var client=new NativeQueryClient("127.0.0.1",nativePort);
                    clients[index]=client;
                    using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    deadline.CancelAfter(TimeSpan.FromSeconds(10));
                    await ReadNativeAsync(client,index,deadline.Token);
                });
                var active=CountConnections();
                results.Add(new{Case="legacy-mixed-stage",Target=stage,NativeConnections=active.Native,TdsConnections=active.Tds,
                    HttpConnections=active.Http,ConnectAndVerifySeconds=Stopwatch.GetElapsedTime(started).TotalSeconds,Server=await capture()});
                if(active!=(stage,1,1)){return false;}
                opened=stage;
            }
            var rejectedNative=await RejectNewConnectionAsync(nativePort,token);
            var rejectedTds=await RejectNewConnectionAsync(tdsPort,token);
            var rejectedHttp=await RejectNewConnectionAsync(httpPort,token);
            var nativeLatency=new double[2000];
            var tdsLatency=new double[200];
            var httpLatency=new double[200];
            var nativeDone=0;
            var before=await capture();
            var startedMixed=Stopwatch.GetTimestamp();
            var nativeWork=RunNativeAsync();
            var tdsWork=MeasureSqlAsync(tdsLatency,ReadTdsAsync);
            var httpWork=MeasureSqlAsync(httpLatency,ReadHttpAsync);
            await Task.WhenAll(nativeWork,tdsWork,httpWork);
            var tdsOverlap=await tdsWork;
            var httpOverlap=await httpWork;
            var elapsed=Stopwatch.GetElapsedTime(startedMixed).TotalSeconds;
            var after=await capture();
            var remaining=CountConnections();
            var tdsRecovered=await VerifyCrossProtocolRecoveryAsync();
            results.Add(new{Case="legacy-mixed-at-capacity",Target=target,NativeConnections=remaining.Native,TdsConnections=remaining.Tds,HttpConnections=remaining.Http,
                RejectedNative=rejectedNative,RejectedTds=rejectedTds,RejectedHttp=rejectedHttp,NativeRequests=nativeLatency.Length,TdsRequests=tdsLatency.Length,
                HttpRequests=httpLatency.Length,ConcurrentTdsCompletions=tdsOverlap,ConcurrentHttpCompletions=httpOverlap,TdsRecovered=tdsRecovered,
                NativeLatency=Summarize(nativeLatency),TdsLatency=Summarize(tdsLatency),HttpLatency=Summarize(httpLatency),ElapsedSeconds=elapsed,Before=before,After=after});
            if(!rejectedNative||!rejectedTds||!rejectedHttp||!tdsRecovered||remaining!=(target,1,1)||tdsOverlap==0||httpOverlap==0
                ||before.SharedConnections!=target+2||after.SharedConnections!=target+2){return false;}

            async Task RunNativeAsync(){
                await Parallel.ForEachAsync(Enumerable.Range(0,nativeLatency.Length),limits,async(index,cancellation)=>{
                    var sent=Stopwatch.GetTimestamp();
                    await ReadNativeAsync(clients[index%target]!,index,cancellation);
                    nativeLatency[index]=Stopwatch.GetElapsedTime(sent).TotalMilliseconds;
                });
                Volatile.Write(ref nativeDone,1);
            }
            async Task<int> MeasureSqlAsync(double[] latency,Func<CancellationToken,Task> read){
                var overlapped=0;
                for(var index=0;index<latency.Length;index++){
                    var sent=Stopwatch.GetTimestamp();
                    await read(token);
                    latency[index]=Stopwatch.GetElapsedTime(sent).TotalMilliseconds;
                    if(Volatile.Read(ref nativeDone)==0){overlapped++;}
                }
                return overlapped;
            }
        }finally{
            foreach(var client in clients){if(client is not null){await client.DisposeAsync();}}
            await tds.CloseAsync();
            http.Dispose();
        }
        using var cleanup=CancellationTokenSource.CreateLinkedTokenSource(token);
        cleanup.CancelAfter(TimeSpan.FromSeconds(15));
        var final=CountConnections();
        var server=await capture();
        while(final!=(0,0,0)||server.SharedConnections!=0){
            await Task.Delay(25,cleanup.Token);
            final=CountConnections();
            server=await capture();
        }
        results.Add(new{Case="legacy-mixed-cleanup",NativeConnections=final.Native,TdsConnections=final.Tds,HttpConnections=final.Http,Server=server});
        return true;

        async Task<bool> VerifyCrossProtocolRecoveryAsync(){
            await tds.CloseAsync();
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            while(CountConnections().Tds!=0){await Task.Delay(25,deadline.Token);}
            var tcpClosedAt=Stopwatch.GetTimestamp();
            var first=await capture();
            await WaitForSharedSlotAsync(first);
            var releaseMs=Stopwatch.GetElapsedTime(tcpClosedAt).TotalMilliseconds;
            await using(var additionalNative=new NativeQueryClient("127.0.0.1",nativePort)){
                await ReadNativeAsync(additionalNative,0,deadline.Token);
                if(CountConnections()!=(target+1,0,1)){return false;}
            }
            while(CountConnections().Native!=target){await Task.Delay(25,deadline.Token);}
            await WaitForSharedSlotAsync(await capture());
            await tds.OpenAsync(deadline.Token);
            await ReadTdsAsync(deadline.Token);
            results.Add(new{Case="legacy-mixed-recovery",SharedAtTdsTcpClose=first.SharedConnections,SharedAfterTdsRelease=target+1,ReleaseMs=releaseMs});
            return CountConnections()==(target,1,1);

            async Task WaitForSharedSlotAsync(Snapshot observed){
                while(observed.SharedConnections!=target+1){
                    await Task.Delay(10,deadline.Token);
                    observed=await capture();
                }
            }
        }
        async Task ReadTdsAsync(CancellationToken cancellation){
            using var command=tds.CreateCommand();
            command.CommandText="SELECT DB_NAME()";
            if(await command.ExecuteScalarAsync(cancellation) is not "fixture"){throw new InvalidDataException("Unexpected TDS scalar result.");}
        }
        async Task ReadHttpAsync(CancellationToken cancellation){
            using var body=new StringContent("{\"query\":\"SELECT 7 AS id\"}",Encoding.UTF8,"application/json");
            using var response=await http.PostAsync("/api/sql",body,cancellation);
            response.EnsureSuccessStatusCode();
            using var document=JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellation));
            var rows=document.RootElement.GetProperty("rows");
            if(rows.GetArrayLength()!=1||rows[0].GetProperty("id").GetInt32()!=7){throw new InvalidDataException("Unexpected HTTP scalar result.");}
        }
        static async Task ReadNativeAsync(NativeQueryClient client,int value,CancellationToken cancellation){
            if(await client.ExecuteScalarAsync<int,int>("probe","small","fixture",Fingerprint,value,cancellation)!=value+1){
                throw new InvalidDataException("Unexpected native scalar result.");
            }
        }
        (int Native,int Tds,int Http) CountConnections(){
            var count=(Native:0,Tds:0,Http:0);
            foreach(var socket in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections()){
                if(socket.State!=TcpState.Established||!IPAddress.IsLoopback(socket.RemoteEndPoint.Address)){continue;}
                var port=socket.RemoteEndPoint.Port;
                if(port==nativePort){count.Native++;}
                else if(port==tdsPort){count.Tds++;}
                else if(port==httpPort){count.Http++;}
            }
            return count;
        }
    }
}
