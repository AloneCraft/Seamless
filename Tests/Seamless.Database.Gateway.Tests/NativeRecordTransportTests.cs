using System.Reflection;
using Seamless.Database.Client;
using Seamless.Database.Client.Results;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Native;
using Seamless.Execution;
using Manager=Seamless.Database.Gateway.Hosting.GatewayTransactionManager;
using System.Net.Sockets;
using MemoryPack;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Diagnostics;
using System.Text.Json;
using Seamless.Execution.Codecs;
using NativeClientPool=Seamless.Database.Client.NativeConnectionPool;
using System.IO.Pipes;
using System.Security.Principal;

namespace Seamless.Database.Gateway.Tests.RecordRpc;

internal static class DatabaseGenerator{
    public const string ClientContract="Seamless.Database.Gateway.Tests.RecordRpc.IRecordQueries";
    public const string ClientModule="record-test";
    public const string ClientDatabase="fixture";
    public const string ServerImplementation="Seamless.Database.Gateway.Tests.RecordRpc.RecordQueries";
}
public interface IRecordQueries{
    IEnumerable<RecordPayload> Echo(RecordInput input);
    IEnumerable<RecordPayload> Write(RecordInput input);
    IEnumerable<RecordPayload> Fail(RecordInput input);
    int Count(RecordInput input);
}
public interface IAlternateRecordQueries{
    IEnumerable<RecordPayload> Echo(RecordInput input);
}
public readonly record struct RecordInput(int Id,RecordNode First,RecordNode Second);
public readonly record struct RecordPayload(RecordNode First,RecordNode Second);
public sealed class RecordNode{
    public RecordNode? Next;
    public double Number;
    public decimal Amount;
    public DateTime Date;
    public DateTimeOffset Offset;
}
internal sealed class RecordQueries(RuntimeSqlGeneratedRoot database):IRecordQueries{
    public IEnumerable<RecordPayload> Echo(RecordInput input){yield return new(input.First,input.Second);}
    public IEnumerable<RecordPayload> Write(RecordInput input){database.TypedOrders.Add(new(input.Id,"record",1));yield return new(input.First,input.Second);}
    public IEnumerable<RecordPayload> Fail(RecordInput input){database.TypedOrders.Add(new(input.Id,"fail",1));yield return new(input.First,input.Second);throw new InvalidOperationException("injected record statement failure");}
    public int Count(RecordInput input)=>database.TypedOrders.Count;
}

public sealed class NativeRecordTransportTests{
    private const string Fingerprint="fingerprint";
    [Theory,InlineData(false,false),InlineData(false,true),InlineData(true,false),InlineData(true,true)]
    public async Task LocalOwnerGeneratedResultsCancelAndPreserveCompletedStatements(bool gateway,bool explicitTransaction){
        using var fixture=new Fixture(gateway,local:true);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));var budget=new ResourceBudget(1<<20,0);
        await using var connection=NativeQueryClient.ForLocalOwner(fixture.Server.PipeName!,budget);NativeTransaction? transaction=null;
        try{
            if(explicitTransaction){transaction=await connection.BeginTransactionAsync("fixture",timeout.Token);}
            RecordQueriesClient client;if(transaction is null){client=new(connection,Fingerprint);}else{client=new(transaction,Fingerprint);}
            await using(var rows=client.WriteAsync(Input(),timeout.Token)){await using var iterator=rows.GetAsyncEnumerator();Assert.True(await iterator.MoveNextAsync());Check(Input(),iterator.Current);}
            var field=typeof(NativeQueryClient).GetField("stream",BindingFlags.Instance|BindingFlags.NonPublic)!;var transport=Assert.IsType<NamedPipeClientStream>(field.GetValue(connection));
            NativeResultStream<RecordPayload> blocked;
            if(transaction is null){blocked=connection.QueryRecordsStream<RecordInput,RecordPayload,IRecordQueries>("blocked-record","Block","fixture",Fingerprint,Input() with{Id=2},timeout.Token);}
            else{blocked=transaction.QueryRecordsStream<RecordInput,RecordPayload,IRecordQueries>("blocked-record","Block",Fingerprint,Input() with{Id=2},timeout.Token);}
            await using(var rows=blocked){
                await using var iterator=rows.GetAsyncEnumerator();var reading=iterator.MoveNextAsync().AsTask();await fixture.State.BlockEntered.Task.WaitAsync(timeout.Token);
                await rows.DisposeAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>reading);
            }
            Assert.True(fixture.State.BlockReleased.Task.IsCompletedSuccessfully);Assert.Same(transport,field.GetValue(connection));Assert.Equal(1,await client.CountAsync(Input(),timeout.Token));
            if(transaction is not null){await transaction.CommitAsync(timeout.Token);}
            Assert.Equal(1,await new RecordQueriesClient(connection,Fingerprint).CountAsync(Input(),timeout.Token));Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,fixture.State.BlockBudget!.MemoryBytes);
        }finally{if(transaction is not null){await transaction.DisposeAsync();}}
    }
    [Theory,InlineData(false),InlineData(true)]
    public async Task LocalOwnerAbruptDisconnectRollsBackAndReleasesTheKey(bool gateway){
        using var fixture=new Fixture(gateway,local:true);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));var transaction=Guid.NewGuid();
        using(var pipe=new NamedPipeClientStream(".",fixture.Server.PipeName!,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly,TokenImpersonationLevel.Identification)){
            await pipe.ConnectAsync(timeout.Token);
            await NativeFrame.WriteAsync(pipe,NativeFrame.Request,MemoryPackSerializer.Serialize(new NativeQueryRequest("$transaction","begin","fixture","",[],transaction)),timeout.Token);
            Assert.Equal(NativeFrame.Completed,(await NativeFrame.ReadAsync(pipe,timeout.Token))!.Value.Kind);
            var contract=NativeRecordQueryContract.For<RecordInput,RecordPayload,IRecordQueries>();
            var request=new NativeQueryRequest("record-test","Write","fixture",Fingerprint,NativeRecordCodec<RecordInput>.Serialize(Input()),transaction,contract.ArgumentsContract,contract.ResultContract);
            await NativeFrame.WriteAsync(pipe,NativeFrame.Request,MemoryPackSerializer.Serialize(request),timeout.Token);
            var row=await NativeFrame.ReadAsync(pipe,timeout.Token);Assert.Equal(NativeFrame.Row,row!.Value.Kind);Check(Input(),NativeRecordCodec<RecordPayload>.Deserialize(row.Value.Payload));
            Assert.Equal(NativeFrame.Completed,(await NativeFrame.ReadAsync(pipe,timeout.Token))!.Value.Kind);
        }
        await using var connection=NativeQueryClient.ForLocalOwner(fixture.Server.PipeName!);var client=new RecordQueriesClient(connection,Fingerprint);
        Assert.Equal(0,await client.CountAsync(Input(),timeout.Token));Check(Input(),await Single(client.WriteAsync(Input(),timeout.Token)));Assert.Equal(1,await client.CountAsync(Input(),timeout.Token));
    }
    [Theory,InlineData(false),InlineData(true)]
    public async Task LocalOwnerStopCancelsActiveWorkAndCanRestart(bool gateway){
        using var fixture=new Fixture(gateway,local:true);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using(var connection=NativeQueryClient.ForLocalOwner(fixture.Server.PipeName!)){
            await using var rows=connection.QueryRecordsStream<RecordInput,RecordPayload,IRecordQueries>("blocked-record","Block","fixture",Fingerprint,Input(),timeout.Token);
            await using var iterator=rows.GetAsyncEnumerator();var reading=iterator.MoveNextAsync().AsTask();await fixture.State.BlockEntered.Task.WaitAsync(timeout.Token);
            await fixture.Server.StopAsync().WaitAsync(timeout.Token);await Assert.ThrowsAnyAsync<IOException>(()=>reading);
            Assert.True(fixture.State.BlockReleased.Task.IsCompletedSuccessfully);Assert.Equal(0,fixture.State.BlockBudget!.MemoryBytes);
        }
        fixture.Server.Start();await using var next=NativeQueryClient.ForLocalOwner(fixture.Server.PipeName!);Assert.Equal(0,await new RecordQueriesClient(next,Fingerprint).CountAsync(Input(),timeout.Token));
    }
    [Fact]
    public async Task TlsRecordResourceFactoriesPreserveExistingSignaturesAndPoolOptions(){
        Assert.NotNull(typeof(NativeQueryClient).GetMethod("ForTls",[typeof(string),typeof(int),typeof(X509Certificate2)]));
        Assert.NotNull(typeof(NativeQueryClient).GetMethod("ForAuthenticatedTls",[typeof(Func<CancellationToken,ValueTask<string>>),typeof(string),typeof(int),typeof(X509Certificate2)]));
        var budget=new ResourceBudget(1<<20,0);var limits=new RecordGraphValidationLimits{MaximumDepth=12};
        Func<CancellationToken,ValueTask<string>> token=static _=>throw new InvalidOperationException("Constructing a client must not authenticate.");
        await using var tls=NativeQueryClient.ForTls("localhost",1,null,budget,limits);await using var authenticated=NativeQueryClient.ForAuthenticatedTls(token,"localhost",1,null,budget,limits);
        Assert.Same(budget,tls.RecordBudget);Assert.Same(limits,tls.RecordLimits);Assert.Same(budget,authenticated.RecordBudget);Assert.Same(limits,authenticated.RecordLimits);
        var options=new NativeConnectionPoolOptions{RecordBudget=budget,RecordLimits=limits};
        using var pool=NativeClientPool.ForTls("localhost",1,options);using var authenticatedPool=NativeClientPool.ForAuthenticatedTls("localhost",1,options,new(Guid.NewGuid(),"test"),token);
        await using var pooled=await pool.RentAsync();await using var authenticatedPooled=await authenticatedPool.RentAsync();
        Assert.Same(budget,pooled.RecordBudget);Assert.Same(limits,pooled.RecordLimits);Assert.Same(budget,authenticatedPooled.RecordBudget);Assert.Same(limits,authenticatedPooled.RecordLimits);
        Assert.Throws<ArgumentNullException>(()=>NativeQueryClient.ForTls("localhost",1,null,recordBudget:null!));
        Assert.Throws<ArgumentNullException>(()=>NativeQueryClient.ForAuthenticatedTls(token,"localhost",1,null,recordBudget:null!));
        Assert.Throws<ArgumentOutOfRangeException>(()=>NativeQueryClient.ForTls("localhost",1,null,budget,new(){MaximumDepth=0}));
        Assert.Throws<ArgumentOutOfRangeException>(()=>NativeQueryClient.ForAuthenticatedTls(token,"localhost",1,null,budget,new(){MaximumBytes=int.MaxValue}));
        Assert.Throws<ArgumentNullException>(()=>NativeClientPool.ForTls("localhost",1,options with{RecordBudget=null!}));
        Assert.Throws<ArgumentNullException>(()=>NativeClientPool.ForTls("localhost",1,options with{RecordLimits=null!}));
        Assert.Throws<ArgumentOutOfRangeException>(()=>NativeClientPool.ForTls("localhost",1,options with{RecordLimits=new(){MaximumDepth=0}}));
    }
    [Theory,InlineData(false),InlineData(true)]
    public Task TlsRecordResourcesHonorParentPressureAndSurvivePoolReuse(bool pooled)=>WithTlsCertificate(async certificate=>{
        using var fixture=new Fixture(false,certificate:certificate);var parent=new ResourceBudget(1<<20,0);var budget=parent.CreateChild(1<<20,0);var limits=new RecordGraphValidationLimits{MaximumDepth=12};
        NativeClientPool? pool=null;
        if(pooled){pool=NativeClientPool.ForTls("localhost",fixture.Server.Port,new(){MaximumConnections=1,RecordBudget=budget,RecordLimits=limits},certificate);}
        ValueTask<NativeQueryClient> Rent(){if(pool is not null){return pool.RentAsync();}return ValueTask.FromResult(NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate,budget,limits));}
        try{
            await using(var connection=await Rent()){
                Assert.Same(budget,connection.RecordBudget);Assert.Same(limits,connection.RecordLimits);var client=new RecordQueriesClient(connection,Fingerprint);
                using(var pressure=parent.ReserveMemory(parent.MemoryLimitBytes)){
                    await Assert.ThrowsAsync<ResourceLimitException>(()=>client.CountAsync(Input()));Assert.Equal(0,fixture.State.Factories);Assert.Equal(0,budget.MemoryBytes);Assert.Equal(pressure.Bytes,parent.MemoryBytes);
                }
                Check(Input(),await Single(client.EchoAsync(Input())));Assert.True(budget.PeakMemoryBytes>0);Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,parent.MemoryBytes);
            }
            await using(var connection=await Rent()){Assert.Same(budget,connection.RecordBudget);Assert.Same(limits,connection.RecordLimits);Assert.Equal(0,await new RecordQueriesClient(connection,Fingerprint).CountAsync(Input()));}
            Assert.Equal(0,parent.MemoryBytes);Assert.Equal(0,budget.MemoryBytes);
        }finally{pool?.Dispose();}
        await using var constrained=NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate,budget,new(){MaximumNodes=1});
        var factories=fixture.State.Factories;await Assert.ThrowsAsync<InvalidDataException>(()=>new RecordQueriesClient(constrained,Fingerprint).CountAsync(Input()));Assert.Equal(factories,fixture.State.Factories);Assert.Equal(0,parent.MemoryBytes);
    });
    [Theory,InlineData(false,false),InlineData(false,true),InlineData(true,false),InlineData(true,true)]
    public Task TlsOwnedResultsCancelAndReuseTheConnectionWithTransactionRecovery(bool gateway,bool explicitTransaction)=>WithTlsCertificate(async certificate=>{
        using var fixture=new Fixture(gateway,certificate:certificate);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var connection=NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate);var budget=connection.RecordBudget;
        NativeTransaction? transaction=null;
        try{
            if(explicitTransaction){transaction=await connection.BeginTransactionAsync("fixture",timeout.Token);}
            RecordQueriesClient client;
            if(transaction is not null){client=new(transaction,Fingerprint);}else{client=new(connection,Fingerprint);}
            await using(var rows=client.WriteAsync(Input(),timeout.Token)){
                await using var iterator=rows.GetAsyncEnumerator();Assert.True(await iterator.MoveNextAsync());Check(Input(),iterator.Current);
            }
            var field=typeof(NativeQueryClient).GetField("stream",BindingFlags.Instance|BindingFlags.NonPublic)!;var transport=Assert.IsType<SslStream>(field.GetValue(connection));
            Assert.True(transport.IsAuthenticated);Assert.True(transport.IsEncrypted);Assert.Equal(1,await client.CountAsync(Input(),timeout.Token));
            NativeResultStream<RecordPayload> blocked;
            if(transaction is not null){blocked=transaction.QueryRecordsStream<RecordInput,RecordPayload,IRecordQueries>("blocked-record","Block",Fingerprint,Input() with{Id=2},timeout.Token);}
            else{blocked=connection.QueryRecordsStream<RecordInput,RecordPayload,IRecordQueries>("blocked-record","Block","fixture",Fingerprint,Input() with{Id=2},timeout.Token);}
            await using(var rows=blocked){
                await using var iterator=rows.GetAsyncEnumerator();var reading=iterator.MoveNextAsync().AsTask();await fixture.State.BlockEntered.Task.WaitAsync(timeout.Token);
                var closing=rows.DisposeAsync().AsTask();Assert.Same(closing,rows.DisposeAsync().AsTask());await closing.WaitAsync(timeout.Token);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>reading.WaitAsync(timeout.Token));
            }
            Assert.True(fixture.State.BlockReleased.Task.IsCompletedSuccessfully);Assert.Equal(0,Assert.IsType<ResourceBudget>(fixture.State.BlockBudget).MemoryBytes);
            Assert.Same(transport,field.GetValue(connection));Assert.Equal(1,await client.CountAsync(Input(),timeout.Token));
            if(transaction is not null){Assert.False(transaction.IsCompleted);await transaction.CommitAsync(timeout.Token);}
            Assert.Equal(1,await new RecordQueriesClient(connection,Fingerprint).CountAsync(Input(),timeout.Token));Assert.Equal(0,budget.MemoryBytes);
            Assert.All(fixture.State.Budgets,static item=>Assert.Equal(0,item.MemoryBytes));
            await using var next=NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate);Assert.Equal(1,await new RecordQueriesClient(next,Fingerprint).CountAsync(Input(),timeout.Token));
        }finally{if(transaction is not null){await transaction.DisposeAsync();}}
    });
    [Theory,InlineData(false),InlineData(true)]
    public Task TlsOwnedQueryRejectsAnUntrustedOrMismatchedCertificate(bool mismatch)=>WithTlsCertificate(async certificate=>{
        using var fixture=new Fixture(false,certificate:certificate);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        NativeQueryClient connection;
        if(mismatch){connection=NativeQueryClient.ForTls("127.0.0.1",fixture.Server.Port,certificate);}else{connection=NativeQueryClient.ForTls("localhost",fixture.Server.Port);}
        await using(connection){
            await using var rows=new RecordQueriesClient(connection,Fingerprint).WriteAsync(Input(),timeout.Token);await using var iterator=rows.GetAsyncEnumerator();
            await Assert.ThrowsAsync<global::System.Security.Authentication.AuthenticationException>(()=>iterator.MoveNextAsync().AsTask());
            Assert.Null(typeof(NativeQueryClient).GetField("stream",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(connection));Assert.Equal(0,fixture.State.Factories);
        }
        await using var valid=NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate);Assert.Equal(0,await new RecordQueriesClient(valid,Fingerprint).CountAsync(Input(),timeout.Token));
    });
    [Theory,InlineData(false,false),InlineData(false,true),InlineData(true,false),InlineData(true,true)]
    public Task SeparateGeneratedClientCancelsAndCommitsOnlyCompletedStatements(bool gateway,bool local)=>WithTlsCertificate(async certificate=>{
        using var fixture=new Fixture(gateway,certificate:certificate,local:local);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var certificatePath=Path.Combine(fixture.Root,"public.cer");File.WriteAllBytes(certificatePath,certificate.Export(X509ContentType.Cert));
        using var process=new Process{StartInfo=new("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=fixture.Root}};
        process.StartInfo.ArgumentList.Add(typeof(NativeRecordTransportTests).Assembly.Location);
        if(local){process.StartInfo.ArgumentList.Add("--record-owned-ipc-client");process.StartInfo.ArgumentList.Add(fixture.Server.PipeName!);}
        else{process.StartInfo.ArgumentList.Add("--record-owned-tls-client");process.StartInfo.ArgumentList.Add(fixture.Server.Port.ToString(global::System.Globalization.CultureInfo.InvariantCulture));}
        process.StartInfo.ArgumentList.Add(certificatePath);
        Assert.True(process.Start());var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
        File.WriteAllText(Path.Combine(fixture.Root,"child-owner.json"),JsonSerializer.Serialize(new{Pid=process.Id,CreatedUtc=process.StartTime.ToUniversalTime(),ParentPid=Environment.ProcessId}));
        try{
            await fixture.State.BlockEntered.Task.WaitAsync(timeout.Token);Assert.True(Assert.IsType<ResourceBudget>(fixture.State.BlockBudget).MemoryBytes>0);
            await process.StandardInput.WriteLineAsync("cancel");await process.StandardInput.FlushAsync(timeout.Token);await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0,process.ExitCode);using var result=JsonDocument.Parse(await output);var report=result.RootElement;
            Assert.Equal(process.Id,report.GetProperty("Pid").GetInt32());Assert.NotEqual(Environment.ProcessId,process.Id);
            if(OperatingSystem.IsWindows()||OperatingSystem.IsLinux()){
                using var parent=Process.GetCurrentProcess();Assert.Equal(parent.ProcessorAffinity.ToInt64(),report.GetProperty("Affinity").GetInt64());
            }else{throw new PlatformNotSupportedException("The process affinity probe requires Windows or Linux.");}
            Assert.Equal(!local,report.GetProperty("Encrypted").GetBoolean());Assert.Equal(local,report.GetProperty("LocalPipe").GetBoolean());Assert.True(report.GetProperty("Reused").GetBoolean());Assert.Equal(2,report.GetProperty("Rows").GetInt32());Assert.Equal(0,report.GetProperty("BudgetBytes").GetInt64());
            Assert.True(fixture.State.BlockReleased.Task.IsCompletedSuccessfully);Assert.Equal(0,Assert.IsType<ResourceBudget>(fixture.State.BlockBudget).MemoryBytes);
            NativeQueryClient Observer(){if(local){return NativeQueryClient.ForLocalOwner(fixture.Server.PipeName!);}return NativeQueryClient.ForTls("localhost",fixture.Server.Port,certificate);}
            await using var observer=Observer();Assert.Equal(2,await new RecordQueriesClient(observer,Fingerprint).CountAsync(Input(),timeout.Token));
            Assert.All(fixture.State.Budgets,static budget=>Assert.Equal(0,budget.MemoryBytes));
        }finally{
            if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}
            File.WriteAllText(Path.Combine(fixture.Root,"child-result.json"),await output);File.WriteAllText(Path.Combine(fixture.Root,"child-stderr.log"),await errors);
        }
    });
    internal static async Task<int> RunTransportClientAsync(string[] arguments){
        try{
            var local=arguments[0]=="--record-owned-ipc-client";var port=0;
            if(arguments.Length!=3||(!local&&!int.TryParse(arguments[1],out port))){return 2;}
            var root=Path.GetFullPath(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated test root."))+Path.DirectorySeparatorChar;
            var path=Path.GetFullPath(arguments[2]);if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)){return 2;}
            using var certificate=X509CertificateLoader.LoadCertificateFromFile(path);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(50));
            NativeQueryClient Create(){if(local){return NativeQueryClient.ForLocalOwner(arguments[1]);}return NativeQueryClient.ForTls("localhost",port,certificate);}
            await using var connection=Create();var input=Input();var client=new RecordQueriesClient(connection,Fingerprint);
            await using(var rows=client.WriteAsync(input,timeout.Token)){await using var iterator=rows.GetAsyncEnumerator();Assert.True(await iterator.MoveNextAsync());Check(input,iterator.Current);}
            var field=typeof(NativeQueryClient).GetField("stream",BindingFlags.Instance|BindingFlags.NonPublic)!;var transport=Assert.IsAssignableFrom<Stream>(field.GetValue(connection));
            if(local){Assert.IsType<NamedPipeClientStream>(transport);}else{Assert.True(Assert.IsType<SslStream>(transport).IsEncrypted);}
            await using(var transaction=await connection.BeginTransactionAsync("fixture",timeout.Token)){
                var transactional=new RecordQueriesClient(transaction,Fingerprint);
                await using(var rows=transactional.WriteAsync(input with{Id=2},timeout.Token)){await using var iterator=rows.GetAsyncEnumerator();Assert.True(await iterator.MoveNextAsync());Check(input,iterator.Current);}
                await using(var rows=transactional.FailAsync(input with{Id=3},timeout.Token)){await Assert.ThrowsAsync<NativeQueryException>(async()=>{await foreach(var _ in rows){} });}
                await using(var rows=transaction.QueryRecordsStream<RecordInput,RecordPayload,IRecordQueries>("blocked-record","Block",Fingerprint,input with{Id=3},timeout.Token)){
                    await using var iterator=rows.GetAsyncEnumerator();var reading=iterator.MoveNextAsync().AsTask();Assert.Equal("cancel",await Console.In.ReadLineAsync(timeout.Token));
                    await rows.DisposeAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>reading);
                }
                Assert.Same(transport,field.GetValue(connection));Assert.Equal(2,await transactional.CountAsync(input,timeout.Token));await transaction.CommitAsync(timeout.Token);
            }
            Assert.Equal(2,await client.CountAsync(input,timeout.Token));Assert.Same(transport,field.GetValue(connection));Assert.Equal(0,connection.RecordBudget.MemoryBytes);
            using var process=Process.GetCurrentProcess();long affinity;
            if(OperatingSystem.IsWindows()||OperatingSystem.IsLinux()){affinity=process.ProcessorAffinity.ToInt64();}else{throw new PlatformNotSupportedException("The process affinity probe requires Windows or Linux.");}
            Console.WriteLine(JsonSerializer.Serialize(new{Pid=Environment.ProcessId,Affinity=affinity,Encrypted=transport is SslStream{IsEncrypted:true},LocalPipe=transport is NamedPipeClientStream,Reused=true,Rows=2,BudgetBytes=connection.RecordBudget.MemoryBytes}));return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 3;}
    }
    [Fact]
    public async Task GeneratedCollectionCanBeDisposedBeforeItStarts(){
        using var fixture=new Fixture(false);await using var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        var rows=new RecordQueriesClient(connection,Fingerprint).EchoAsync(Input());
        var owner=Assert.IsAssignableFrom<IAsyncDisposable>(rows);await owner.DisposeAsync();await owner.DisposeAsync();
        Assert.Equal(0,fixture.State.Factories);Assert.Throws<ObjectDisposedException>(()=>rows.GetAsyncEnumerator());
        Assert.Equal(0,await new RecordQueriesClient(connection,Fingerprint).CountAsync(Input()));
    }
    [Fact]
    public async Task EmptyOwnedResultsCompleteAndMaterializeWithoutStorage(){
        using var fixture=new Fixture(false);await using var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port);var budget=new ResourceBudget(1<<20,0);
        await using(var rows=connection.QueryRecordsStream<RecordInput,RecordPayload,IRecordQueries>("blocked-record","Empty","fixture",Fingerprint,Input())){
            await using var iterator=rows.GetAsyncEnumerator();Assert.False(await iterator.MoveNextAsync());Assert.Throws<InvalidOperationException>(()=>iterator.Current);Assert.Throws<ObjectDisposedException>(()=>rows.GetAsyncEnumerator());
        }
        await using(var rows=connection.QueryRecordsStream<RecordInput,RecordPayload,IRecordQueries>("blocked-record","Empty","fixture",Fingerprint,Input())){
            using var result=await rows.ToResultAsync(new(Path.Combine(fixture.Root,"empty"),1<<20,0,0){ParentBudget=budget});Assert.Empty(result.ReadRows());Assert.Empty(result.ReadRows());
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
    }
    [Theory,InlineData(false),InlineData(true)]
    public async Task GeneratedCollectionOwnsOneEnumerationAndReleasesOnCompletion(bool gateway){
        using var fixture=new Fixture(gateway);var budget=new ResourceBudget(1<<20,0);await using var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port){RecordBudget=budget};
        await using var rows=new RecordQueriesClient(connection,Fingerprint).EchoAsync(Input());Assert.IsType<NativeResultStream<RecordPayload>>(rows);
        await using var iterator=rows.GetAsyncEnumerator();Assert.Throws<InvalidOperationException>(()=>rows.GetAsyncEnumerator());
        var options=new NativeResultOptions(Path.Combine(fixture.Root,"copy"),1<<20,1<<20,32768);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>rows.ToResultAsync(options));Assert.Equal(0,fixture.State.Factories);
        Assert.True(await iterator.MoveNextAsync());Check(Input(),iterator.Current);Assert.False(await iterator.MoveNextAsync());Assert.False(await iterator.MoveNextAsync());
        Assert.Throws<ObjectDisposedException>(()=>rows.GetAsyncEnumerator());Assert.Equal(0,budget.MemoryBytes);Assert.Equal(1,fixture.State.Factories);
    }
    [Theory,InlineData(false),InlineData(true)]
    public async Task OwnerDisposalWaitsForAnActiveReadAndCanBeRepeated(bool gateway){
        using var fixture=new Fixture(gateway);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));await using var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        await using var rows=connection.QueryRecordsStream<RecordInput,RecordPayload,IRecordQueries>("blocked-record","Block","fixture",Fingerprint,Input(),timeout.Token);
        await using var iterator=rows.GetAsyncEnumerator();var reading=iterator.MoveNextAsync().AsTask();await fixture.State.BlockEntered.Task.WaitAsync(timeout.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>iterator.MoveNextAsync().AsTask());
        var closing=rows.DisposeAsync().AsTask();Assert.Same(closing,rows.DisposeAsync().AsTask());await closing.WaitAsync(timeout.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>reading.WaitAsync(timeout.Token));Assert.True(fixture.State.BlockReleased.Task.IsCompletedSuccessfully);
        Assert.Throws<ObjectDisposedException>(()=>rows.GetAsyncEnumerator());Assert.Equal(0,await new RecordQueriesClient(connection,Fingerprint).CountAsync(Input(),timeout.Token));
    }
    [Theory,InlineData(false,0),InlineData(false,32768),InlineData(true,0),InlineData(true,32768)]
    public async Task GeneratedCollectionMaterializesRawGraphsAndOutlivesTheConnection(bool gateway,int threshold){
        using var fixture=new Fixture(gateway);var budget=new ResourceBudget(1<<20,1<<20);var input=Input();
        var options=new NativeResultOptions(Path.Combine(fixture.Root,"copy"),1<<20,1<<20,threshold){ParentBudget=budget};NativeResult<RecordPayload> result;
        await using(var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port)){
            await using var rows=new RecordQueriesClient(connection,Fingerprint).EchoAsync(input);
            typeof(NativeResultStream<RecordPayload>).GetField("decode",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(rows,
                (Func<ReadOnlySpan<byte>,CancellationToken,RecordPayload>)((payload,token)=>throw new InvalidOperationException("The raw materialization path must not decode a streamed row.")));
            result=await rows.ToResultAsync(options);Assert.Equal(threshold==0,result.IsFileBacked);Assert.Throws<ObjectDisposedException>(()=>rows.GetAsyncEnumerator());
        }
        using(result){
            var first=Assert.Single(result.ReadRows());var second=Assert.Single(result.ReadRows());Check(input,first);Check(input,second);Assert.NotSame(first.First,second.First);
            first.First.Number=123;Assert.Equal(0,Assert.Single(result.ReadRows()).First.Number);Assert.True(budget.MemoryBytes+budget.TemporaryStorageBytes>0);
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);
        Assert.False(Directory.Exists(options.TemporaryDirectory)&&Directory.EnumerateFileSystemEntries(options.TemporaryDirectory).Any());
    }
    [Theory,InlineData(false),InlineData(true)]
    public async Task MaterializationAndServerFailuresReleaseTheOwnerAndAllowTheNextQuery(bool gateway){
        using var fixture=new Fixture(gateway);var budget=new ResourceBudget(1<<20,0);await using var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port);var client=new RecordQueriesClient(connection,Fingerprint);
        var options=new NativeResultOptions(Path.Combine(fixture.Root,"copy"),1<<20,0,0){ParentBudget=budget};
        await using(var rows=client.EchoAsync(Input())){
            await Assert.ThrowsAsync<ResourceLimitException>(()=>rows.ToResultAsync(options));Assert.Throws<ObjectDisposedException>(()=>rows.GetAsyncEnumerator());
        }
        Assert.Equal(0,budget.MemoryBytes);Assert.Equal(0,budget.TemporaryStorageBytes);Assert.Equal(0,await client.CountAsync(Input()));
        await using(var rows=client.FailAsync(Input())){await Assert.ThrowsAsync<NativeQueryException>(async()=>{await foreach(var _ in rows){} });Assert.Throws<ObjectDisposedException>(()=>rows.GetAsyncEnumerator());}
        Assert.Equal(0,await client.CountAsync(Input()));
    }
    [Theory,InlineData(false),InlineData(true)]
    public async Task PartialGeneratedResultKeepsTheExplicitTransactionAndItsConnection(bool gateway){
        using var fixture=new Fixture(gateway);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port);await using var transaction=await connection.BeginTransactionAsync("fixture",timeout.Token);
        var client=new RecordQueriesClient(transaction,Fingerprint);var input=Input();
        var field=typeof(NativeQueryClient).GetField("stream",BindingFlags.Instance|BindingFlags.NonPublic)!;var transport=field.GetValue(connection);
        await using(var iterator=client.WriteAsync(input,timeout.Token).GetAsyncEnumerator()){Assert.True(await iterator.MoveNextAsync());Check(input,iterator.Current);}
        Assert.False(transaction.IsCompleted);Assert.Same(transport,field.GetValue(connection));Assert.Equal(1,await client.CountAsync(input,timeout.Token));await transaction.CommitAsync(timeout.Token);
        Assert.Equal(1,await new RecordQueriesClient(connection,Fingerprint).CountAsync(input,timeout.Token));
    }
    [Theory,InlineData(false,false),InlineData(false,true),InlineData(true,false),InlineData(true,true)]
    public async Task RunningRecordCancellationRollsBackOnlyItsStatement(bool gateway,bool explicitTransaction){
        using var fixture=new Fixture(gateway);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));using var cancellation=new CancellationTokenSource();
        await using var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port);NativeTransaction? transaction=null;
        try{
            if(explicitTransaction){transaction=await connection.BeginTransactionAsync("fixture",timeout.Token);await Single(new RecordQueriesClient(transaction,Fingerprint).WriteAsync(Input(),timeout.Token));}
            else{Assert.Equal(0,await new RecordQueriesClient(connection,Fingerprint).CountAsync(Input(),timeout.Token));}
            var field=typeof(NativeQueryClient).GetField("stream",BindingFlags.Instance|BindingFlags.NonPublic)!;var transport=field.GetValue(connection);
            IAsyncEnumerable<RecordPayload> rows;
            if(transaction is not null){rows=transaction.QueryRecordsAsync<RecordInput,RecordPayload,IRecordQueries>("blocked-record","Block",Fingerprint,Input() with{Id=2},cancellation.Token);}
            else{rows=connection.QueryRecordsAsync<RecordInput,RecordPayload,IRecordQueries>("blocked-record","Block","fixture",Fingerprint,Input() with{Id=2},cancellation.Token);}
            async Task Read(){await foreach(var _ in rows){throw new InvalidOperationException("The blocked statement returned before cancellation.");}}
            var pending=Read();await fixture.State.BlockEntered.Task.WaitAsync(timeout.Token);cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending.WaitAsync(timeout.Token));Assert.True(fixture.State.BlockReleased.Task.IsCompletedSuccessfully);Assert.Same(transport,field.GetValue(connection));
            var expected=0;
            if(transaction is not null){Assert.False(transaction.IsCompleted);Assert.Equal(1,await new RecordQueriesClient(transaction,Fingerprint).CountAsync(Input(),timeout.Token));await transaction.CommitAsync(timeout.Token);expected=1;}
            Assert.Equal(expected,await new RecordQueriesClient(connection,Fingerprint).CountAsync(Input(),timeout.Token));
        }finally{if(transaction is not null){await transaction.DisposeAsync();}}
    }
    [Theory,InlineData(false),InlineData(true)]
    public async Task PrebuiltModule_SharesTheDatabaseAssemblyAndRunsGeneratedEntry(bool gateway){
        _=new RecordQueriesModule();
        using var fixture=new Fixture(gateway,prebuilt:true);await using var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        var client=new RecordQueriesClient(connection,Fingerprint);var input=Input();
        Check(input,await Single(client.WriteAsync(input)));Assert.Equal(1,await client.CountAsync(input));
        await using(var transaction=await connection.BeginTransactionAsync("fixture")){
            var transactional=new RecordQueriesClient(transaction,Fingerprint);
            Check(input,await Single(transactional.WriteAsync(input with{Id=2})));
            await Assert.ThrowsAsync<NativeQueryException>(async()=>{await foreach(var _ in transactional.FailAsync(input with{Id=3})){} });
            Assert.Equal(2,await transactional.CountAsync(input));await transaction.RollbackAsync();
        }
        Assert.Equal(1,await client.CountAsync(input));
    }

    [Fact]
    public void PrebuiltModule_RejectsADifferentImageWithTheSameAssemblyName(){
        using var fixture=new Fixture(false);var module=new RecordQueriesModule();
        var manifest=new NativeModuleManifest("record-test","","","","test",[module.GetContract("Echo").Define("Echo","fixture",Fingerprint,true)]);
        var artifact=Prebuilt(fixture.Root,manifest);
        using(var output=new FileStream(artifact.AssemblyPath,FileMode.Append,FileAccess.Write)){output.WriteByte(0);}
        var changed=Hash(artifact.AssemblyPath);
        artifact=artifact with{Sha256=changed,Manifest=artifact.Manifest with{AssemblySha256=changed}};
        var error=Assert.Throws<InvalidDataException>(()=>new LoadedNativeQueryModule(artifact,new Dictionary<string,Assembly>{{"fixture",typeof(RecordQueriesModule).Assembly}}));
        Assert.Contains("query and database assembly builds differ",error.Message,StringComparison.Ordinal);
    }
    [Theory,InlineData(false),InlineData(true)]
    public async Task GeneratedClientAndModule_PreserveGraphsAndTransactionBoundaries(bool gateway){
        using var fixture=new Fixture(gateway);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        var client=new RecordQueriesClient(connection,Fingerprint);var input=Input();
        var echo=await Single(client.EchoAsync(input,timeout.Token));Check(input,echo);
        Assert.Equal(0,await client.CountAsync(input,timeout.Token));
        Check(input,await Single(client.WriteAsync(input,timeout.Token)));Assert.Equal(1,await client.CountAsync(input,timeout.Token));
        await using(var transaction=await connection.BeginTransactionAsync("fixture",timeout.Token)){
            var transactional=new RecordQueriesClient(transaction,Fingerprint);
            await Single(transactional.WriteAsync(input with{Id=2},timeout.Token));
            var seen=0;
            await Assert.ThrowsAsync<NativeQueryException>(async()=>{await foreach(var row in transactional.FailAsync(input with{Id=3},timeout.Token)){seen++;}});
            Assert.Equal(0,seen);Assert.Equal(2,await transactional.CountAsync(input,timeout.Token));
            await transaction.RollbackAsync(timeout.Token);
        }
        Assert.Equal(1,await client.CountAsync(input,timeout.Token));
        Assert.True(fixture.State.Factories>0);Assert.True(fixture.State.BudgetObserved);
        Assert.NotEmpty(fixture.State.Budgets);Assert.All(fixture.State.Budgets,static budget=>Assert.Equal(0,budget.MemoryBytes));
    }

    [Theory,InlineData(false),InlineData(true)]
    public async Task BufferedResults_ReplayIndependentGraphsAfterDisconnect(bool gateway){
        using var fixture=new Fixture(gateway);var root=Path.Combine(fixture.Root,"result");var parent=new ResourceBudget(4<<20,8<<20);
        var options=new NativeResultOptions(root,2<<20,8<<20,0){ParentBudget=parent};var input=Input();
        NativeResult<RecordPayload> result;
        await using(var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port)){
            result=await connection.QueryRecordsBufferedAsync<RecordInput,RecordPayload,IRecordQueries>("record-test","Echo","fixture",Fingerprint,input,options);
            await using var transaction=await connection.BeginTransactionAsync("fixture");
            using var transactional=await transaction.QueryRecordsBufferedAsync<RecordInput,RecordPayload,IRecordQueries>("record-test","Echo",Fingerprint,input,options);
            Assert.True(transactional.IsFileBacked);Check(input,Assert.Single(transactional.ReadRows()));
            await transaction.RollbackAsync();
        }
        using(result){
            Assert.True(result.IsFileBacked);var first=Assert.Single(result.ReadRows());Check(input,first);first.First.Number=99;
            var second=Assert.Single(result.ReadRows());Assert.Equal(0,second.First.Number);Assert.NotSame(first.First,second.First);
        }
        Assert.Equal(0,parent.MemoryBytes);Assert.Equal(0,parent.TemporaryStorageBytes);
    }

    [Fact]
    public async Task WrongResultContract_IsRejectedBeforeTheFactoryRuns(){
        using var fixture=new Fixture(false);await using var client=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        var error=await Assert.ThrowsAsync<NativeQueryException>(async()=>{await foreach(var row in client.QueryRecordsAsync<RecordInput,int,IRecordQueries>("record-test","Echo","fixture",Fingerprint,Input())){}});
        Assert.True(error.IsRecoverable);Assert.Equal(0,fixture.State.Factories);
        Assert.Equal(0,await new RecordQueriesClient(client,Fingerprint).CountAsync(Input()));
    }

    [Theory,InlineData(false,false),InlineData(false,true),InlineData(true,false),InlineData(true,true)]
    public async Task WrongPublicInterface_IsRejectedBeforeTheFactoryRuns(bool gateway,bool transactional){
        using var fixture=new Fixture(gateway);await using var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port);
        NativeTransaction? transaction=null;
        try{
            IAsyncEnumerable<RecordPayload> wrong,unbound;
            if(transactional){
                transaction=await connection.BeginTransactionAsync("fixture");
                wrong=transaction.QueryRecordsAsync<RecordInput,RecordPayload,IAlternateRecordQueries>("record-test","Echo",Fingerprint,Input());
                unbound=transaction.QueryRecordsAsync<RecordInput,RecordPayload>("record-test","Echo",Fingerprint,Input());
            }else{
                wrong=connection.QueryRecordsAsync<RecordInput,RecordPayload,IAlternateRecordQueries>("record-test","Echo","fixture",Fingerprint,Input());
                unbound=connection.QueryRecordsAsync<RecordInput,RecordPayload>("record-test","Echo","fixture",Fingerprint,Input());
            }
            foreach(var rows in new[]{wrong,unbound}){
                var error=await Assert.ThrowsAsync<NativeQueryException>(async()=>{await foreach(var row in rows){}});
                Assert.True(error.IsRecoverable);Assert.Equal(0,fixture.State.Factories);
            }
            RecordQueriesClient valid;
            if(transaction is not null){valid=new(transaction,Fingerprint);}else{valid=new(connection,Fingerprint);}
            Assert.Equal(0,await valid.CountAsync(Input()));Assert.Equal(1,fixture.State.Factories);
        }finally{if(transaction is not null){await transaction.DisposeAsync();}}
    }

    [Theory,InlineData(false),InlineData(true)]
    public async Task InvalidArguments_AreRejectedBeforeTheFactoryRuns(bool gateway){
        using var fixture=new Fixture(gateway);using var socket=new TcpClient();await socket.ConnectAsync("127.0.0.1",fixture.Server.Port);
        await using var stream=socket.GetStream();var bytes=NativeRecordCodec<RecordInput>.Serialize(Input());bytes[4]^=1;
        var contract=NativeRecordQueryContract.For<RecordInput,RecordPayload,IRecordQueries>();
        var request=new NativeQueryRequest("record-test","Echo","fixture",Fingerprint,bytes,ArgumentsContract:contract.ArgumentsContract,ResultContract:contract.ResultContract);
        await NativeFrame.WriteAsync(stream,NativeFrame.Request,MemoryPackSerializer.Serialize(request));
        var response=await NativeFrame.ReadAsync(stream);Assert.NotNull(response);Assert.Equal(NativeFrame.RecoverableError,response.Value.Kind);Assert.Equal(0,fixture.State.Factories);
    }

    [Theory,InlineData(false),InlineData(true)]
    public async Task InvalidResults_AreRejectedBeforeCommitOrTransmission(bool gateway){
        using var fixture=new Fixture(gateway);await using var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port);var seen=0;
        await Assert.ThrowsAsync<NativeQueryException>(async()=>{await foreach(var row in connection.QueryRecordsAsync<RecordInput,RecordPayload>("bad-record","Bad","fixture",Fingerprint,Input())){seen++;}});
        Assert.Equal(0,seen);Assert.Equal(0,await new RecordQueriesClient(connection,Fingerprint).CountAsync(Input()));
    }

    [Fact]
    public async Task ClientBudgetFailure_DoesNotSendAndCanBeRetried(){
        using var fixture=new Fixture(false);var budget=new ResourceBudget(1<<20,0);
        await using var connection=new NativeQueryClient("127.0.0.1",fixture.Server.Port){RecordBudget=budget};var client=new RecordQueriesClient(connection,Fingerprint);
        using(var pressure=budget.ReserveMemory(budget.MemoryLimitBytes-1024)){
            await Assert.ThrowsAsync<ResourceLimitException>(()=>client.CountAsync(Input()));Assert.Equal(0,fixture.State.Factories);
            Assert.Equal(pressure.Bytes,budget.MemoryBytes);
        }
        Assert.Equal(0,await client.CountAsync(Input()));Assert.Equal(0,budget.MemoryBytes);
    }

    [Fact]
    public void PublishedArgumentValidation_UsesTheGatewayBudgetAndCancellation(){
        var budget=new ResourceBudget(1<<20,0);var module=new InvalidResultModule();var contract=module.GetContract("Bad");
        var definition=contract.Define("Bad","fixture",Fingerprint,false);
        var execution=new NativeQueryExecution(Guid.Empty,Guid.Empty,"","","record-test","entry",definition,module,[],null,contract,false,budget);
        var bytes=NativeRecordCodec<RecordInput>.Serialize(Input());
        using(var pressure=budget.ReserveMemory(budget.MemoryLimitBytes-1024)){
            Assert.Throws<ResourceLimitException>(()=>execution.ValidateArguments(bytes));Assert.Equal(pressure.Bytes,budget.MemoryBytes);
        }
        execution.ValidateArguments(bytes);Assert.Equal(0,budget.MemoryBytes);
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(()=>execution.ValidateArguments(bytes,cancellation.Token));Assert.Equal(0,budget.MemoryBytes);
    }

    private static async Task WithTlsCertificate(Func<X509Certificate2,Task> action){
        using var key=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var names=new SubjectAlternativeNameBuilder();names.AddDnsName("localhost");request.CertificateExtensions.Add(names.Build());request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
        using var ephemeral=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddHours(1));
        var certificate=X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.Exportable);string? keyName=null;CngProvider? provider=null;
        if(OperatingSystem.IsWindows()){using var privateKey=certificate.GetRSAPrivateKey();if(privateKey is RSACng cng){keyName=cng.Key.KeyName;provider=cng.Key.Provider;}}
        try{await action(certificate);}
        finally{
            certificate.Dispose();
            if(OperatingSystem.IsWindows()&&keyName is not null&&provider is not null){
                if(CngKey.Exists(keyName,provider)){using var owned=CngKey.Open(keyName,provider);owned.Delete();}Assert.False(CngKey.Exists(keyName,provider));
            }
        }
    }
    private static RecordInput Input(){var node=new RecordNode{Number=-0.0,Amount=12.00m,Date=new(2026,11,7),Offset=new(2026,11,7,9,0,0,TimeSpan.FromHours(9))};node.Next=node;return new(1,node,node);}
    private static void Check(RecordInput input,RecordPayload row){
        Assert.Same(row.First,row.Second);Assert.Same(row.First,row.First.Next);Assert.NotSame(input.First,row.First);
        Assert.Equal(0,BitConverter.DoubleToInt64Bits(row.First.Number));Assert.Equal(decimal.GetBits(12m),decimal.GetBits(row.First.Amount));
        Assert.Equal(DateTimeKind.Utc,row.First.Date.Kind);Assert.Equal(TimeSpan.Zero,row.First.Offset.Offset);
        Assert.Equal(long.MinValue,BitConverter.DoubleToInt64Bits(input.First.Number));Assert.Equal(decimal.GetBits(12.00m),decimal.GetBits(input.First.Amount));
    }
    private static async Task<T> Single<T>(IAsyncEnumerable<T> rows){var count=0;T result=default!;await foreach(var row in rows){count++;result=row;}Assert.Equal(1,count);return result;}
    private static NativeQueryBuildArtifact Prebuilt(string root,NativeModuleManifest manifest){
        var assembly=typeof(RecordQueriesModule).Assembly;var copy=Path.Combine(root,"prebuilt.dll");File.Copy(assembly.Location,copy);
        var metadata=Path.Combine(root,"prebuilt.json");File.WriteAllText(metadata,"{}");var hash=Hash(copy);
        manifest=manifest with{AssemblyPath=copy,AssemblySha256=hash,EntryType=typeof(RecordQueriesModule).FullName!};
        return new(Guid.NewGuid().ToString("N"),copy,hash,metadata,root,manifest,[new("fixture",Fingerprint,assembly.GetName().Name!,hash)]){SourceProjectSha256=Hash(metadata)};
    }
    private static string Hash(string path){using var file=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(file));}
    private sealed class State{
        internal int Factories;internal bool BudgetObserved;internal readonly List<ResourceBudget> Budgets=[];
        internal ResourceBudget? BlockBudget;
        internal readonly TaskCompletionSource BlockEntered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource BlockReleased=new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class BlockingRecordModule(State state):INativeRecordQueryModule{
        public NativeRecordQueryContract GetContract(string queryId)=>NativeRecordQueryContract.For<RecordInput,RecordPayload,IRecordQueries>();
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            if(queryId=="Empty"){yield break;}
            state.BlockBudget=context.Budget;
            using var input=NativeRecordCodec<RecordInput>.DeserializeOwned(arguments.Span,context.Budget,context.CancellationToken);
            ((RuntimeSqlGeneratedRoot)context.Database).TypedOrders.Add(new(input.Value.Id,"cancelled",1));state.BlockEntered.TrySetResult();
            try{Task.Delay(Timeout.Infinite,context.CancellationToken).GetAwaiter().GetResult();}
            finally{state.BlockReleased.TrySetResult();}
            yield break;
        }
    }
    private sealed class InvalidResultModule:INativeRecordQueryModule{
        public NativeRecordQueryContract GetContract(string queryId)=>NativeRecordQueryContract.For<RecordInput,RecordPayload>();
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            ((RuntimeSqlGeneratedRoot)context.Database).TypedOrders.Add(new(99,"invalid-result",1));yield return [0];
        }
    }
    private sealed class Fixture:IDisposable{
        internal readonly string Root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing test root."),"record-rpc-"+Guid.NewGuid().ToString("N"));
        internal readonly State State=new();
        private readonly Manager? manager;
        private readonly RuntimeSqlGeneratedRoot database;
        private readonly LoadedNativeQueryModule? loaded;
        internal readonly NativeQueryServer Server;
        internal Fixture(bool gateway,bool prebuilt=false,X509Certificate2? certificate=null,bool local=false){
            var options=DatabaseContainerOptions.OnStorage(Path.Combine(this.Root,"db")) with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(this.Root,"tmp")};
            if(gateway){this.manager=new(Path.Combine(this.Root,"gateway"));options=options with{GatewayProfile=this.manager.Profile(1)};}
            this.database=new(options);var registry=new DatabaseRegistry(transactions:this.manager);registry.Register("fixture",new InProcessDatabaseAdapter(this.database),schemaFingerprint:Fingerprint);this.manager?.Recover();
            var module=new RecordQueriesModule(context=>{
                this.State.Factories++;var budget=context.Budget??throw new InvalidOperationException("Missing record budget.");
                this.State.BudgetObserved=budget.MemoryBytes>0;this.State.Budgets.Add(budget);return new RecordQueries((RuntimeSqlGeneratedRoot)context.Database);
            });
            var definitions=new[]{module.GetContract("Echo").Define("Echo","fixture",Fingerprint,true),module.GetContract("Write").Define("Write","fixture",Fingerprint,false),module.GetContract("Fail").Define("Fail","fixture",Fingerprint,false),module.GetContract("Count").Define("Count","fixture",Fingerprint,true)};
            var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
            INativeQueryModule entry=module;var manifest=new NativeModuleManifest("record-test","","","",version,definitions);
            if(prebuilt){
                var assembly=typeof(RecordQueriesModule).Assembly;var artifact=Prebuilt(this.Root,manifest);manifest=artifact.Manifest;
                this.loaded=new(artifact,new Dictionary<string,Assembly>{{"fixture",assembly}});entry=this.loaded.Module;
                Assert.Same(assembly,entry.GetType().Assembly);Assert.IsType<RecordQueriesModule>(entry);
            }
            if(local){this.Server=NativeQueryServer.CreateLocalOwner(registry,"seamless-rpc-"+Guid.NewGuid().ToString("N"),new(){MaximumConnections=4,MaximumConcurrentRequests=1});}
            else if(certificate is null){this.Server=new(registry,0);}else{this.Server=NativeQueryServer.CreateTls(registry,certificate,0,maximumConcurrentRequests:1);}
            this.Server.Register(manifest,entry,version);
            var bad=new InvalidResultModule();this.Server.Register(new("bad-record","","","",version,[bad.GetContract("Bad").Define("Bad","fixture",Fingerprint,false)]),bad,version);
            var blocked=new BlockingRecordModule(this.State);this.Server.Register(new("blocked-record","","","",version,[blocked.GetContract("Block").Define("Block","fixture",Fingerprint,false),blocked.GetContract("Empty").Define("Empty","fixture",Fingerprint,true)]),blocked,version);this.Server.Start();
        }
        public void Dispose(){try{this.Server.Dispose();}finally{try{this.loaded?.Dispose();}finally{try{this.database.Dispose();}finally{this.manager?.Dispose();}}}}
    }
}
