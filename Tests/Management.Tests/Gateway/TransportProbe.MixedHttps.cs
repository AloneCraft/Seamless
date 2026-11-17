using System.Diagnostics;
using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Tests;

internal static partial class TransportProbe{
    private static HttpClient CreateMixedHttpsClient(int port,X509Certificate2 certificate){
        var trust=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};
        trust.CustomTrustStore.Add(certificate);
        return new(new SocketsHttpHandler{UseProxy=false,MaxConnectionsPerServer=1,SslOptions=new(){CertificateChainPolicy=trust}}){
            BaseAddress=new($"https://127.0.0.1:{port}"),Timeout=TimeSpan.FromSeconds(10)
        };
    }

    private static async Task VerifyMixedHttpsAsync(HttpClient client,string credential,CancellationToken token,bool close=false){
        using var request=new HttpRequestMessage(HttpMethod.Get,"/api/databases");
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",credential);
        request.Headers.ConnectionClose=close;
        using var response=await client.SendAsync(request,token);
        response.EnsureSuccessStatusCode();
        using var document=JsonDocument.Parse(await response.Content.ReadAsStreamAsync(token));
        var databases=document.RootElement;
        if(databases.GetArrayLength()!=1||databases[0].GetProperty("Name").GetString()!="fixture"){
            throw new InvalidDataException("Unexpected authorized HTTPS catalogue.");
        }
    }

    private static async Task<bool> MeasureMixedHttpsAsync(HttpClient https,int port,SqlConnection? tds,int tdsPort,PolicyProbeAuth auth,int connections,ParallelOptions limits,
        Func<int,CancellationToken,Task> smallAt,Func<int,NativeQueryClient> createClient,Func<NativeQueryClient,int,int,CancellationToken,Task> small,
        Func<Task<Snapshot>> capture,List<object> results,CancellationToken token,bool mixedLong){
        const int nativeRequests=2000,httpsRequests=200;
        var credential=await auth.Identities[0].TokenProvider(token);
        var before=await capture();
        using(var anonymous=await https.GetAsync("/api/databases",token)){
            if(anonymous.StatusCode!=HttpStatusCode.Unauthorized){throw new InvalidDataException("HTTPS accepted an anonymous catalogue request.");}
        }
        var rejected=await RejectNewConnectionAsync(port,token);
        var rejectedExtraTds=tds is not null&&await RejectNewConnectionAsync(tdsPort,token);
        var nativeLatency=new double[nativeRequests];
        var httpsLatency=new double[httpsRequests];
        var tdsLatency=new double[tds is null?0:200];
        var nativeDone=0;
        var overlap=0;
        var tdsOverlap=0;
        var started=Stopwatch.GetTimestamp();
        var native=RunNativeAsync();
        await Task.WhenAll(native,RunHttpsAsync(),RunTdsAsync());
        var elapsed=Stopwatch.GetElapsedTime(started).TotalSeconds;
        var longPassed=true;
        if(mixedLong){
            if(tds is null){throw new InvalidDataException("A TDS connection is required for long mixed load.");}
            longPassed=await MeasureMixedRateAsync(https,tds,credential,connections,limits.MaxDegreeOfParallelism,smallAt,capture,results,token);
        }
        var after=await capture();
        var httpsConnections=CountMixedTcp(port);
        var tdsConnections=tds is null?0:CountMixedTcp(tdsPort);
        var sharedLimit=connections+1+(tds is null?0:1);

        await VerifyMixedHttpsAsync(https,credential,token,close:true);
        using var recovery=CancellationTokenSource.CreateLinkedTokenSource(token);
        recovery.CancelAfter(TimeSpan.FromSeconds(15));
        await WaitMixedSharedConnectionsAsync(capture,sharedLimit-1,recovery.Token);
        bool recoveredNative;
        Snapshot withReplacement;
        await using(var replacement=createClient(0)){
            await replacement.ConnectAsync(recovery.Token);
            await small(replacement,0,0,recovery.Token);
            recoveredNative=replacement.AuthenticatedPrincipal==auth.Identities[0].Principal;
            withReplacement=await capture();
        }
        var released=await WaitMixedSharedConnectionsAsync(capture,sharedLimit-1,recovery.Token);
        await VerifyMixedHttpsAsync(https,credential,recovery.Token,close:tds is null);
        int? tdsSharedImmediatelyAfterClose=null;
        int? tdsSharedAfterRelease=null;
        if(tds is not null){
            await VerifyMixedTdsAsync(tds,-1,recovery.Token);
            await tds.CloseAsync();
            tdsSharedImmediatelyAfterClose=(await capture()).SharedConnections;
            tdsSharedAfterRelease=(await WaitMixedSharedConnectionsAsync(capture,sharedLimit-1,recovery.Token)).SharedConnections;
            await using var reopened=await OpenMixedTdsAsync(tdsPort,auth.Certificate,credential,recovery.Token);
            await VerifyMixedTdsAsync(reopened,-2,recovery.Token);
            if((await capture()).SharedConnections!=sharedLimit){throw new InvalidDataException("The reopened TDS session did not fill the released shared slot.");}
        }
        results.Add(new{Case=tds is null?"authenticated-mixed-https":"authenticated-mixed-https-tds",NativeConnections=connections,HttpsConnections=httpsConnections,
            TdsConnections=tdsConnections,SharedLimit=sharedLimit,TdsSharedImmediatelyAfterClose=tdsSharedImmediatelyAfterClose,TdsSharedAfterRelease=tdsSharedAfterRelease,
            RejectedExtraHttps=rejected,AnonymousRejected=true,NativeRequests=nativeRequests,HttpsRequests=httpsRequests,ConcurrentHttpsCompletions=overlap,
            RejectedExtraTds=rejectedExtraTds,InvalidTdsCredentialRejected=tds is not null,
            TdsRequests=tdsLatency.Length,ConcurrentTdsCompletions=tdsOverlap,TdsLatency=Summarize(tdsLatency),
            NativeLatency=Summarize(nativeLatency),HttpsLatency=Summarize(httpsLatency),ElapsedSeconds=elapsed,RecoveredNative=recoveredNative,RecoveredHttps=true,
            RecoveredTds=tds is not null,Before=before,After=after,WithReplacement=withReplacement,AfterReplacement=released});
        return longPassed&&rejected&&recoveredNative&&httpsConnections==1&&overlap>0&&before.ActiveAuthenticatedSessions==connections
            &&(tds is null||rejectedExtraTds&&tdsConnections==1&&tdsOverlap>0)
            &&after.ActiveAuthenticatedSessions==connections&&after.AuthenticatedSessions==before.AuthenticatedSessions
            &&before.SharedConnections==sharedLimit&&after.SharedConnections==sharedLimit&&withReplacement.SharedConnections==sharedLimit
            &&withReplacement.ActiveAuthenticatedSessions==connections+1&&released.ActiveAuthenticatedSessions==connections
            &&released.AuthenticatedSessions==after.AuthenticatedSessions+1;

        async Task RunNativeAsync(){
            await Parallel.ForEachAsync(Enumerable.Range(0,nativeRequests),limits,async(index,cancellation)=>{
                var sent=Stopwatch.GetTimestamp();
                await smallAt(index,cancellation);
                nativeLatency[index]=Stopwatch.GetElapsedTime(sent).TotalMilliseconds;
            });
            Volatile.Write(ref nativeDone,1);
        }
        async Task RunHttpsAsync(){
            for(var index=0;index<httpsRequests;index++){
                var sent=Stopwatch.GetTimestamp();
                await VerifyMixedHttpsAsync(https,credential,token);
                httpsLatency[index]=Stopwatch.GetElapsedTime(sent).TotalMilliseconds;
                if(Volatile.Read(ref nativeDone)==0){overlap++;}
            }
        }
        async Task RunTdsAsync(){
            if(tds is null){return;}
            for(var index=0;index<tdsLatency.Length;index++){
                var sent=Stopwatch.GetTimestamp();
                await VerifyMixedTdsAsync(tds,index,token);
                tdsLatency[index]=Stopwatch.GetElapsedTime(sent).TotalMilliseconds;
                if(Volatile.Read(ref nativeDone)==0){tdsOverlap++;}
            }
        }
    }

    private static async Task<bool> MeasureMixedRateAsync(HttpClient https,SqlConnection tds,string credential,int connections,int parallelism,
        Func<int,CancellationToken,Task> smallAt,Func<Task<Snapshot>> capture,List<object> results,CancellationToken token){
        var smoke=connections<10000;
        var duration=smoke?3:330;
        var rate=smoke?500:2000;
        var refreshCycles=smoke?0:1;
        var before=await capture();
        var started=Stopwatch.GetTimestamp();
        var native=MeasureAuthenticatedRateAsync(rate,duration,connections,parallelism,smallAt,capture,results,token,refreshCycles,sharedIssuer:true);
        var http=RunCompanionAsync((_,cancellation)=>VerifyMixedHttpsAsync(https,credential,cancellation),"https");
        var sql=RunCompanionAsync((index,cancellation)=>VerifyMixedTdsAsync(tds,index,cancellation),"tds");
        await Task.WhenAll(native,http,sql);
        var after=await capture();
        var httpsResult=await http;
        var tdsResult=await sql;
        var passed=await native&&httpsResult.Passed&&tdsResult.Passed&&before.SharedConnections==connections+2&&after.SharedConnections==connections+2;
        results.Add(new{Case="authenticated-mixed-rate",DurationSeconds=duration,NativeRate=rate,Https=httpsResult,Tds=tdsResult,
            Before=before,After=after,Passed=passed});
        return passed;

        async Task<MixedCompanionResult> RunCompanionAsync(Func<int,CancellationToken,Task> execute,string stage){
            var latency=new List<double>(duration*10);
            var last=started;
            var maxGap=TimeSpan.Zero;
            CapacityFailure? failure=null;
            using var interval=new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while(Stopwatch.GetElapsedTime(started)<TimeSpan.FromSeconds(duration)){
                var sent=Stopwatch.GetTimestamp();
                try{await execute(latency.Count,token);}
                catch(Exception error)when(!token.IsCancellationRequested){failure=Failure(latency.Count,stage,error);break;}
                var now=Stopwatch.GetTimestamp();
                latency.Add(Stopwatch.GetElapsedTime(sent,now).TotalMilliseconds);
                var gap=Stopwatch.GetElapsedTime(last,now);
                if(gap>maxGap){maxGap=gap;}
                last=now;
                await interval.WaitForNextTickAsync(token);
            }
            var lastSeconds=Stopwatch.GetElapsedTime(started,last).TotalSeconds;
            var passed=failure is null&&latency.Count>=duration*5&&maxGap<TimeSpan.FromSeconds(2)&&lastSeconds>=duration-1;
            return new(latency.Count,Summarize(latency.ToArray()),maxGap.TotalMilliseconds,lastSeconds,failure,passed);
        }
    }

    private sealed record MixedCompanionResult(int Completed,object? Latency,double LargestCompletionGapMs,double LastCompletionSeconds,CapacityFailure? Failure,bool Passed);

    private static int CountMixedTcp(int port)=>IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().Count(connection=>
        connection.RemoteEndPoint.Port==port&&connection.State==TcpState.Established&&IPAddress.IsLoopback(connection.RemoteEndPoint.Address));

    private static async Task<Snapshot> WaitMixedSharedConnectionsAsync(Func<Task<Snapshot>> capture,int expected,CancellationToken token){
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while(true){
            var snapshot=await capture();
            if(snapshot.SharedConnections==expected){return snapshot;}
            if(snapshot.SharedConnections is null){throw new InvalidDataException("The gateway did not report its shared connection count.");}
            await Task.Delay(TimeSpan.FromMilliseconds(25),deadline.Token);
        }
    }

    private static async Task VerifyMixedTdsRejectsInvalidCredentialAsync(int port,X509Certificate2 certificate,CancellationToken token){
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try{
            await using var invalid=await OpenMixedTdsAsync(port,certificate,"invalid",deadline.Token);
            throw new InvalidDataException("TDS accepted an invalid identity.");
        }catch(SqlException error)when(error.Number==18456){
            while(CountMixedTcp(port)!=0){await Task.Delay(TimeSpan.FromMilliseconds(25),deadline.Token);}
        }
    }

    private static async Task<SqlConnection> OpenMixedTdsAsync(int port,X509Certificate2 certificate,string credential,CancellationToken token){
        var pin=Path.Combine(Path.GetTempPath(),"seamless-tds-probe-"+Guid.NewGuid().ToString("N")+".cer");
        try{
            await File.WriteAllBytesAsync(pin,certificate.Export(X509ContentType.Cert),token);
            var builder=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{port}",InitialCatalog="fixture",
                Encrypt=SqlConnectionEncryptOption.Strict,Pooling=false,ConnectTimeout=5,ConnectRetryCount=0};
            builder["Server Certificate"]=pin;
            var connection=new SqlConnection(builder.ConnectionString){AccessToken=credential};
            try{await connection.OpenAsync(token);return connection;}
            catch{await connection.DisposeAsync();throw;}
        }finally{File.Delete(pin);}
    }

    private static async Task VerifyMixedTdsAsync(SqlConnection connection,int value,CancellationToken token){
        using var command=new SqlCommand("SELECT @value AS [Value]",connection);
        command.Parameters.Add("@value",SqlDbType.Int).Value=value;
        if(await command.ExecuteScalarAsync(token) is not int result||result!=value){throw new InvalidDataException("Unexpected authorized TDS result.");}
    }

    private static async Task<bool> RejectNewConnectionAsync(int port,CancellationToken token){
        using var socket=new TcpClient();
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try{
            await socket.ConnectAsync(IPAddress.Loopback,port,deadline.Token);
            return await socket.GetStream().ReadAsync(new byte[1],deadline.Token)==0;
        }catch(IOException error)when(error.InnerException is SocketException{SocketErrorCode:SocketError.ConnectionReset or SocketError.ConnectionAborted}){
            return true;
        }
    }
}
