using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Seamless.Management;
using Seamless.Management.Server;
using PublishedConnection=Seamless.Management.LocalServerConnection;
using ServerService=Seamless.Management.Server.ManagementService;

namespace Management.Tests;

public sealed class ManagementTransport{
    // 接続は1要求ごとに閉じる（2026-11-12 の決定で接続の再利用をやめた）。要求ごとに受信の枠を返し、認証も要求ごとに確かめる。
    [Theory][InlineData(false)][InlineData(true)]
    public Task EachConnectionServesOneRequestAndReleasesRequestBytes(bool pipe)=>WithServerCertificate(async certificate=>{
        const int limit=1024;
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"single-requests-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new ServerService(store);
        await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(IPAddress.Loopback,0),"admin",limit);
        server.Start();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // どちらも単独なら上限内。1つ目の接続が閉じて枠を返してから、2つ目を読める。
        foreach(var text in new[]{new string('a',600),new string('b',600)}){
            var response=await Exchange("info",null,text);
            Assert.Equal("result",response.Kind);
            Assert.Equal(store.InstanceId,ManagementJson.Read<ServerInfo>(response.Payload).InstanceId);
        }
        while(server.BufferedRequestBytes!=0){await Task.Delay(10,deadline.Token);}

        if(pipe){
            var identity=await Exchange("localIdentity",null,new{});
            Assert.Equal("admin",ManagementJson.Read<UserInfo>(identity.Payload).Name);
        }else{
            var login=await Exchange("login",null,new LoginRequest("admin","TestPassword-123"));
            var token=ManagementJson.Read<LoginResult>(login.Payload).Token;
            var authenticated=await Exchange("users",token,new{});
            Assert.Equal("result",authenticated.Kind);
            var unauthenticated=await Exchange("users",null,new{});
            Assert.Equal("error",unauthenticated.Kind);
            Assert.Equal("Authentication",unauthenticated.ErrorCode);
        }

        async Task<RpcResponse> Exchange(string operation,string? token,object payload){
            await using var stream=pipe?await OpenPipeAsync(server.PipeName!,deadline.Token):await OpenTlsAsync(server.Port,certificate,deadline.Token);
            var request=new RpcRequest(Guid.NewGuid(),operation,token,"",ManagementJson.Element(payload));
            await ManagementWire.WriteAsync(stream,request,deadline.Token);
            var response=await ManagementWire.ReadAsync<RpcResponse>(stream,deadline.Token);
            Assert.Equal(request.Id,response.Id);
            // 応答のあとでサーバーが接続を閉じる。
            Assert.Equal(0,await stream.ReadAsync(new byte[1],deadline.Token));
            return response;
        }
    });

    [Fact]
    public Task ConnectionClosesRightAfterTheResponse()=>WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"single-close-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new ServerService(store);
        await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(IPAddress.Loopback,0));
        var failed=0;
        server.ConnectionFailed+=_=>Interlocked.Increment(ref failed);
        server.Start();
        // 以前の再利用の待ち時間（15秒）より十分短い期限で、閉じることを確かめる。
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream=await OpenTlsAsync(server.Port,certificate,deadline.Token);
        var request=new RpcRequest(Guid.NewGuid(),"info",null,"",ManagementJson.Element(new{}));
        await ManagementWire.WriteAsync(stream,request,deadline.Token);
        var response=await ManagementWire.ReadAsync<RpcResponse>(stream,deadline.Token);
        Assert.Equal(request.Id,response.Id);
        Assert.Equal(0,await stream.ReadAsync(new byte[1],deadline.Token));
        Assert.Equal(0,Volatile.Read(ref failed));
        var client=new ManagementClient("localhost",server.Port,certificate);
        Assert.Equal(store.InstanceId,(await client.InfoAsync(deadline.Token)).InstanceId);
    });

    [Fact]
    public Task TlsDisconnectDuringUpdateEnumerationRollsBack()=>WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"tls-execute-cancel-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new ServerService(store);
        var login=service.Login(new("admin","TestPassword-123"));
        var user=service.Authenticate(login.Token);
        NativeWorkflow.Publish(service,user,NativeWorkflow.Database());
        var query=new Definition("update-stream","Native",DefinitionKind.Query,"Model.Updates","Run",new("1",[new("update.cs","""
            using System;
            using System.Collections.Generic;
            using Seamless.Management.Server;
            namespace Model;
            public static class Updates{
                public static IEnumerable<int> Run(ManagementExecutionContext context){
                    context.GetDatabase<Db>().Rows.Add(new Records.Row(1,9));
                    yield return 9;
                    // A disconnected client leaves the server between rows. Bound the fixture even without cancellation.
                    var deadline=DateTime.UtcNow.AddSeconds(8);
                    while(DateTime.UtcNow<deadline){
                        context.CancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }
            """)],[]),true);
        var count=new Definition("count-rows","Native",DefinitionKind.Query,"Model.Reads","Run",new("1",[new("read.cs","""
            using System.Linq;
            using Seamless.Management.Server;
            namespace Model;
            public static class Reads{
                public static int Run(ManagementExecutionContext context)=>context.GetDatabase<Db>().Rows.Count();
            }
            """)],[]));
        NativeWorkflow.Publish(service,user,query);
        NativeWorkflow.Publish(service,user,count);
        await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(IPAddress.Loopback,0),null,4096);
        server.Start();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var request=new RpcRequest(Guid.NewGuid(),"execute",login.Token,"Native",ManagementJson.Element(new ExecuteRequest(query.Id,null,ManagementJson.Element(new{}))));
        await using(var stream=await OpenTlsAsync(server.Port,certificate,deadline.Token)){
            await ManagementWire.WriteAsync(stream,request,deadline.Token);
            var row=await ManagementWire.ReadAsync<RpcResponse>(stream,deadline.Token);
            Assert.Equal(request.Id,row.Id);
            Assert.Equal("row",row.Kind);
            Assert.Equal(9,ManagementJson.Read<ExecutionRow>(row.Payload).Value.GetInt32());
            Assert.True(server.BufferedRequestBytes>0);
        }
        while(server.BufferedRequestBytes!=0){await Task.Delay(10,deadline.Token);}
        var rows=service.Execute(user,new(count.Id,null,ManagementJson.Element(new{})),deadline.Token);
        Assert.Equal(0,Assert.Single(rows).Value.GetInt32());
    });

    [Theory][InlineData(false)][InlineData(true)]
    public Task ResponseBudgetDoesNotReportACommittedChangeAsFailed(bool pipe)=>WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"response-budget-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new ServerService(store);
        var login=service.Login(new("admin","TestPassword-123"));
        await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(IPAddress.Loopback,0),"admin",null,1,TimeSpan.FromSeconds(2));
        var failed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ConnectionFailed+=error=>{if(error is IOException){failed.TrySetResult();}};
        server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var stream=pipe?await OpenPipeAsync(server.PipeName!,timeout.Token):await OpenTlsAsync(server.Port,certificate,timeout.Token);
        var change=new ChangeUserRequest("created","AnotherPassword-123",[]);
        await ManagementWire.WriteAsync(stream,new RpcRequest(Guid.NewGuid(),"changeUser",login.Token,"",ManagementJson.Element(change)),timeout.Token);
        await failed.Task.WaitAsync(timeout.Token);
        try{Assert.Equal(0,await stream.ReadAsync(new byte[1],timeout.Token));}
        catch(IOException error)when(error.InnerException is SocketException{SocketErrorCode:SocketError.ConnectionAborted or SocketError.ConnectionReset}){}
        Assert.Equal("created",service.Login(new("created","AnotherPassword-123")).User.Name);
    });

    [Theory][InlineData(false)][InlineData(true)]
    public Task SlowResponseUsesTheSameWriteBudgetAcrossTlsAndIpc(bool holdLocal)=>WithServerCertificate(async certificate=>{
        const int rowBytes=12*1024*1024;
        const int budgetBytes=16*1024*1024;
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"write-budget-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new ServerService(store);
        var login=service.Login(new("admin","TestPassword-123"));
        var user=service.Authenticate(login.Token);
        NativeWorkflow.Publish(service,user,NativeWorkflow.Database());
        var query=new Definition("large-result","Native",DefinitionKind.Query,"Model.LargeResult","Run",new("1",[new("large.cs","""
            namespace Model;
            public static class LargeResult{
                public static string Run()=>new string('x',12*1024*1024);
            }
            """)],[]));
        NativeWorkflow.Publish(service,user,query);
        NativeWorkflow.Publish(service,user,NativeWorkflow.Database() with{Id="write-budget-peer",Database="Peer"});
        var peer=query with{Id="large-peer-result",Database="Peer"};
        NativeWorkflow.Publish(service,user,peer);
        await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(IPAddress.Loopback,0),"admin",4096,budgetBytes,TimeSpan.FromSeconds(10));
        var denied=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expired=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ConnectionFailed+=error=>{
            if(error is IOException{InnerException:null}){denied.TrySetResult();}
            if(error is IOException{InnerException:OperationCanceledException}){expired.TrySetResult();}
        };
        server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using(var held=await OpenAsync(holdLocal)){
            await SendAsync(held,query);
            while(server.PendingWriteBytes<rowBytes){await Task.Delay(10,timeout.Token);}
            await using(var competing=await OpenAsync(!holdLocal)){
                await SendAsync(competing,peer);
                await denied.Task.WaitAsync(timeout.Token);
                Assert.True(server.PendingWriteBytes>=rowBytes);
                try{Assert.Equal(0,await competing.ReadAsync(new byte[1],timeout.Token));}
                catch(IOException error)when(error.InnerException is SocketException{SocketErrorCode:SocketError.ConnectionAborted or SocketError.ConnectionReset}){}
            }
            await expired.Task.WaitAsync(timeout.Token);
            Assert.Equal(0,server.PendingWriteBytes);
        }
        await using var recovered=await OpenAsync(!holdLocal);
        var id=await SendAsync(recovered,peer);
        var row=await ManagementWire.ReadAsync<RpcResponse>(recovered,timeout.Token);
        Assert.Equal(id,row.Id);
        Assert.Equal("row",row.Kind);
        var value=ManagementJson.Read<ExecutionRow>(row.Payload).Value.GetString();
        Assert.Equal(rowBytes,value?.Length);
        Assert.Equal("complete",(await ManagementWire.ReadAsync<RpcResponse>(recovered,timeout.Token)).Kind);

        Task<Stream> OpenAsync(bool pipe)=>pipe?OpenPipeAsync(server.PipeName!,timeout.Token):OpenTls();
        async Task<Stream> OpenTls()=>await OpenTlsAsync(server.Port,certificate,timeout.Token);
        async Task<Guid> SendAsync(Stream stream,Definition definition){
            var id=Guid.NewGuid();
            var payload=ManagementJson.Element(new ExecuteRequest(definition.Id,null,ManagementJson.Element(new{})));
            await ManagementWire.WriteAsync(stream,new RpcRequest(id,"execute",login.Token,definition.Database,payload),timeout.Token);
            return id;
        }
    });

    [Theory][InlineData(false)][InlineData(true)]
    public Task TlsAndLocalPipeShareTheBufferedRequestBudget(bool holdLocal)=>WithServerCertificate(async certificate=>{
        const int limit=512;
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"wire-budget-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new ServerService(store);
        await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(IPAddress.Loopback,0),"admin",limit);
        var denied=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ConnectionFailed+=error=>{if(error is ManagementException{Code:"Busy"}){denied.TrySetResult();}};
        server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var header=new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header,limit);
        await using(var held=await OpenAsync(holdLocal)){
            await held.WriteAsync(header,timeout.Token);
            await held.FlushAsync(timeout.Token);
            await WaitForBytesAsync(limit);
            await using var competing=await OpenAsync(!holdLocal);
            await competing.WriteAsync(header,timeout.Token);
            await competing.FlushAsync(timeout.Token);
            await denied.Task.WaitAsync(timeout.Token);
            Assert.Equal(limit,server.BufferedRequestBytes);
        }
        await WaitForBytesAsync(0);
        var tcp=new ManagementClient("localhost",server.Port,certificate);
        Assert.Equal(store.InstanceId,(await tcp.InfoAsync(timeout.Token)).InstanceId);
        var local=ManagementClient.ForLocalPipe(server.PipeName!);
        Assert.Equal("admin",(await local.LocalIdentityAsync(timeout.Token)).Name);

        async Task<Stream> OpenAsync(bool pipe){
            if(!pipe){return await OpenTlsAsync(server.Port,certificate,timeout.Token);}
            return await OpenPipeAsync(server.PipeName!,timeout.Token);
        }
        async Task WaitForBytesAsync(long expected){
            while(server.BufferedRequestBytes!=expected){await Task.Delay(10,timeout.Token);}
        }
    });

    private static async Task<Stream> OpenPipeAsync(string name,CancellationToken cancellationToken){
        var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        try{await pipe.ConnectAsync(5000,cancellationToken);return pipe;}
        catch{pipe.Dispose();throw;}
    }

    [Theory][InlineData(false)][InlineData(true)]
    public Task FragmentedJsonRetainsWholeDocumentValidation(bool trailing)=>WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"framing-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new ServerService(store);
        await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(IPAddress.Loopback,0));
        server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var stream=await OpenTlsAsync(server.Port,certificate,timeout.Token);
        var id=Guid.NewGuid();var text=JsonSerializer.Serialize(new RpcRequest(id,"info",null,"",ManagementJson.Element(new string('x',200000))),ManagementJson.Options);if(trailing){text+="{}";}var bytes=Encoding.UTF8.GetBytes(text);var prefix=new byte[4];BinaryPrimitives.WriteInt32BigEndian(prefix,bytes.Length);foreach(var value in prefix){await stream.WriteAsync(new[]{value},timeout.Token);}for(var offset=0;offset<bytes.Length;offset+=8192){await stream.WriteAsync(bytes.AsMemory(offset,Math.Min(8192,bytes.Length-offset)),timeout.Token);}await stream.FlushAsync(timeout.Token);
        if(trailing){try{Assert.Equal(0,await stream.ReadAsync(new byte[1],timeout.Token));}catch(IOException){}}
        else{var response=await ManagementWire.ReadAsync<RpcResponse>(stream,timeout.Token);Assert.Equal(id,response.Id);Assert.Equal("result",response.Kind);Assert.Equal(store.InstanceId,ManagementJson.Read<ServerInfo>(response.Payload).InstanceId);}
    });

    [Fact]
    public Task TlsConnectionQuotaReleasesSlotForLogin()=>WithServerCertificate(async certificate=>{
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"tls-quota-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new ServerService(store);
        await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(IPAddress.Loopback,0));
        server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var open=new List<SslStream>();
        try{
            for(var i=0;i<32;i++){open.Add(await OpenTlsAsync(server.Port,certificate,timeout.Token));}
            var denied=await Record.ExceptionAsync(async()=>{await using var extra=await OpenTlsAsync(server.Port,certificate,timeout.Token);});
            Assert.True(denied is IOException or AuthenticationException or SocketException,denied?.ToString());

            var id=Guid.NewGuid();
            await ManagementWire.WriteAsync(open[0],new RpcRequest(id,"info",null,"",ManagementJson.Element(new{})),timeout.Token);
            var response=await ManagementWire.ReadAsync<RpcResponse>(open[0],timeout.Token);
            Assert.Equal(id,response.Id);
            Assert.Equal("result",response.Kind);
            Assert.Equal(0,await open[0].ReadAsync(new byte[1],timeout.Token));

            var client=new ManagementClient("localhost",server.Port,certificate);
            Assert.False(string.IsNullOrEmpty((await client.LoginAsync("admin","TestPassword-123",timeout.Token)).Token));
        }finally{foreach(var stream in open){await stream.DisposeAsync();}}
    });

    [Theory][InlineData(false)][InlineData(true)]
    public Task DiscoveredTcpUsesAdvertisedAddressWithCertificateName(bool ipv6)=>WithServerCertificate(async certificate=>{
        var address=IPAddress.Loopback;
        if(ipv6){address=IPAddress.IPv6Loopback;}
        var data=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")??throw new InvalidOperationException("An isolated test root is required.");
        var root=Path.Combine(data,"tls-local-"+Guid.NewGuid().ToString("N"));
        using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
        using var service=new ServerService(store);
        await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(address,0));
        server.Start();
        var published=PublishedConnection.Publish(root,store.InstanceId,server.Port,certificate,null,address);
        Assert.Equal(address.ToString(),published.LoopbackAddress);
        Assert.Equal(published,PublishedConnection.Read(root));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var client=ManagementClient.ForLocalTcp(published,certificate);
        Assert.Equal(store.InstanceId,(await client.InfoAsync(timeout.Token)).InstanceId);
        await Assert.ThrowsAsync<AuthenticationException>(()=>ManagementClient.ForLocalTcp(published with{Host=address.ToString()},certificate).InfoAsync(timeout.Token));
        Assert.Throws<InvalidDataException>(()=>{_=ManagementClient.ForLocalTcp(published with{LoopbackAddress="192.0.2.1"},certificate);});
        published.Remove(root);
        Assert.Null(PublishedConnection.Read(root));
    });

    private static async Task<SslStream>OpenTlsAsync(int port,X509Certificate2 certificate,CancellationToken cancellationToken){
        var socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp){NoDelay=true};
        SslStream? stream=null;
        try{
            await socket.ConnectAsync(IPAddress.Loopback,port,cancellationToken);
            stream=new SslStream(new NetworkStream(socket,ownsSocket:true));
            var policy=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};
            policy.CustomTrustStore.Add(certificate);
            await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost="localhost",CertificateChainPolicy=policy},cancellationToken);
            return stream;
        }catch{stream?.Dispose();socket.Dispose();throw;}
    }

    internal static async Task<int>RunTlsProbeAsync(int catalogueRequests){
        var exitCode=0;
        await WithServerCertificate(async certificate=>{
            var data=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")??throw new InvalidOperationException("An isolated test root is required.");
            var root=Path.Combine(data,"tls-probe-"+Guid.NewGuid().ToString("N"));
            using var store=ManagementStore.Initialize(root,"admin","TestPassword-123");
            using var service=new ServerService(store);
            await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(IPAddress.Loopback,0));
            server.Start();
            using var warmupTimeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var published=PublishedConnection.Publish(root,store.InstanceId,server.Port,certificate,null,IPAddress.Loopback);
            var client=ManagementClient.ForLocalTcp(published,certificate);
            await client.LoginAsync("admin","TestPassword-123",warmupTimeout.Token);

            await Measure("login",128,async cancellation=>{
                var login=ManagementClient.ForLocalTcp(published,certificate);
                var result=await login.LoginAsync("admin","TestPassword-123",cancellation);
                if(result.User.Name!="admin"||string.IsNullOrEmpty(result.Token)){throw new InvalidDataException("Login response mismatch.");}
            });
            await Measure("catalogDatabases",catalogueRequests,async cancellation=>{
                var result=await client.CallAsync<CatalogPage<CatalogDatabase>>("catalogDatabases","",new{},cancellation);
                if(result.Items.Length!=1||!result.Items[0].IsSystem){throw new InvalidDataException("Catalogue response mismatch.");}
            });

            async Task Measure(string name,int count,Func<CancellationToken,Task> operation){
                using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(4));
                var options=new ParallelOptions{MaxDegreeOfParallelism=8,CancellationToken=timeout.Token};
                var successes=new ConcurrentBag<double>();
                var failures=new ConcurrentDictionary<string,int>();
                var started=0;
                var clock=Stopwatch.StartNew();
                try{
                    await Parallel.ForEachAsync(Enumerable.Range(0,count),options,async(_,cancellation)=>{
                        Interlocked.Increment(ref started);
                        var start=Stopwatch.GetTimestamp();
                        try{
                            await operation(cancellation);
                            successes.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                        }catch(OperationCanceledException)when(cancellation.IsCancellationRequested){
                        }catch(Exception error){
                            var code=error switch{
                                SocketException socket=>socket.NativeErrorCode.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
                                ManagementException management=>management.Code,
                                _=>error.GetType().Name
                            };
                            failures.AddOrUpdate(code,1,static(_,previous)=>previous+1);
                        }
                    });
                }catch(OperationCanceledException)when(timeout.IsCancellationRequested){
                }
                var elapsed=clock.Elapsed.TotalSeconds;
                var sorted=successes.Order().ToArray();
                var failed=failures.Values.Sum();
                double? Percentile(double quantile){
                    if(sorted.Length==0){return null;}
                    return sorted[(int)Math.Ceiling(sorted.Length*quantile)-1];
                }
                Console.WriteLine(JsonSerializer.Serialize(new{Case=name,Count=count,Parallelism=options.MaxDegreeOfParallelism,ProcessorCount=Environment.ProcessorCount,Started=started,Succeeded=sorted.Length,Failed=failed,Cancelled=started-sorted.Length-failed,NotStarted=count-started,DeadlineReached=timeout.IsCancellationRequested,Failures=failures,Seconds=elapsed,SuccessfulRequestsPerSecond=sorted.Length/elapsed,P50Ms=Percentile(.50),P95Ms=Percentile(.95),P99Ms=Percentile(.99)}));
                if(sorted.Length!=count){exitCode=1;}
            }
        });
        return exitCode;
    }

    internal static async Task<int>RunTlsAddressProbeAsync(){
        await WithServerCertificate(async certificate=>{
            var data=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")??throw new InvalidOperationException("An isolated test root is required.");
            using var store=ManagementStore.Initialize(Path.Combine(data,"tls-address-"+Guid.NewGuid().ToString("N")),"admin","TestPassword-123");
            using var service=new ServerService(store);
            await using var server=new Seamless.Management.Server.ManagementServer(service,certificate,new(IPAddress.Loopback,0));
            server.Start();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var client=new ManagementClient("127.0.0.1",server.Port,certificate);
            var login=await client.LoginAsync("admin","TestPassword-123",timeout.Token);
            var addresses=await Dns.GetHostAddressesAsync("localhost",timeout.Token);
            Console.WriteLine(JsonSerializer.Serialize(new{Localhost=addresses.Select(static address=>address.ToString())}));
            for(var i=0;i<12;i++){
                await Measure("localhost",i);
                await Measure("127.0.0.1",i);
            }

            async Task Measure(string host,int round){
                using var socket=new TcpClient{NoDelay=true};
                var watch=Stopwatch.StartNew();
                await socket.ConnectAsync(host,server.Port,timeout.Token);
                var connectMs=watch.Elapsed.TotalMilliseconds;
                await using var stream=new SslStream(socket.GetStream());
                var policy=new X509ChainPolicy{TrustMode=X509ChainTrustMode.CustomRootTrust,RevocationMode=X509RevocationMode.NoCheck};
                policy.CustomTrustStore.Add(certificate);
                watch.Restart();
                await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost=host,CertificateChainPolicy=policy},timeout.Token);
                var tlsMs=watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                var id=Guid.NewGuid();
                await ManagementWire.WriteAsync(stream,new RpcRequest(id,"catalogDatabases",login.Token,"",ManagementJson.Element(new{})),timeout.Token);
                var response=await ManagementWire.ReadAsync<RpcResponse>(stream,timeout.Token);
                var rpcMs=watch.Elapsed.TotalMilliseconds;
                if(response.Id!=id||response.Kind!="result"||!ManagementJson.Read<CatalogPage<CatalogDatabase>>(response.Payload).Items.Single().IsSystem){
                    throw new InvalidDataException("Catalogue response mismatch.");
                }
                Console.WriteLine(JsonSerializer.Serialize(new{Host=host,Round=round,ConnectMs=connectMs,TlsMs=tlsMs,RpcMs=rpcMs}));
            }
        },includeLoopbackAddress:true);
        return 0;
    }

    internal static async Task WithServerCertificate(Func<X509Certificate2,Task> test,bool includeLoopbackAddress=false,bool expired=false,bool includeIPv6LoopbackAddress=false){
        using var key=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);var names=new SubjectAlternativeNameBuilder();names.AddDnsName("localhost");
        if(includeLoopbackAddress){names.AddIpAddress(IPAddress.Loopback);}
        if(includeIPv6LoopbackAddress){names.AddIpAddress(IPAddress.IPv6Loopback);}
        request.CertificateExtensions.Add(names.Build());request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));using var ephemeral=request.CreateSelfSigned(expired?DateTimeOffset.UtcNow.AddDays(-2):DateTimeOffset.UtcNow.AddMinutes(-1),expired?DateTimeOffset.UtcNow.AddDays(-1):DateTimeOffset.UtcNow.AddDays(1));var certificate=X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.Exportable);string? keyName=null;CngProvider? provider=null;
        if(OperatingSystem.IsWindows()){using var privateKey=certificate.GetRSAPrivateKey();if(privateKey is RSACng cng){keyName=cng.Key.KeyName;provider=cng.Key.Provider;}}
        try{await test(certificate);}finally{certificate.Dispose();if(OperatingSystem.IsWindows()&&keyName is not null&&provider is not null){if(CngKey.Exists(keyName,provider)){using var owned=CngKey.Open(keyName,provider);owned.Delete();}Assert.False(CngKey.Exists(keyName,provider));}}
    }
}
