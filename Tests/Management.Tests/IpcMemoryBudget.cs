using System.IO.Pipes;
using System.Buffers;
using System.Buffers.Binary;
using System.Text.Json;
using Seamless.Execution;
using Seamless.Transport.Kestrel;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class IpcMemoryBudget{
    [Fact]public async Task ReadAheadUsesTheManagementParentBudgetAndReleasesAfterDisconnect(){
        using var store=ManagementStore.Initialize(Root(),"admin","BudgetTestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);_=service.Info;var baseline=store.ResourceBudget.MemoryBytes;server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        // 接続は1要求ごとに閉じるので、受信のバッファを持っている間（要求を途中まで送った状態）で計上を確かめる。
        await using(var pipe=await Connect(server,timeout.Token)){
            var bytes=JsonSerializer.SerializeToUtf8Bytes(new RpcRequest(Guid.NewGuid(),"info",null,"",ManagementJson.Element(new{})),ManagementJson.Options);
            var header=new byte[4];BinaryPrimitives.WriteInt32BigEndian(header,bytes.Length);
            await pipe.WriteAsync(header,timeout.Token);await pipe.WriteAsync(bytes.AsMemory(0,1),timeout.Token);await pipe.FlushAsync(timeout.Token);
            while(server.PipeBufferBytes==0){await Task.Delay(10,timeout.Token);}
            Assert.True(store.ResourceBudget.MemoryBytes>baseline,"IPC受信バッファが管理DBの親予算へ計上されていません。");
        }
        await WaitForMemory(store,baseline,timeout.Token);
        Assert.Equal(0,server.PipeBufferBytes);
    }
    [Fact]public async Task ExhaustedParentBudgetRejectsBeforeAllocationAndAllowsReconnect(){
        using var store=ManagementStore.Initialize(Root(),"admin","BudgetTestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);var baseline=store.ResourceBudget.MemoryBytes;
        var rejected=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);server.ConnectionFailed+=error=>{if(error is ResourceLimitException){rejected.TrySetResult();}};server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using(var held=store.ResourceBudget.ReserveMemory(store.ResourceBudget.AvailableMemoryBytes)){
            await using var denied=await Connect(server,timeout.Token);await rejected.Task.WaitAsync(timeout.Token);
            Assert.Equal(0,server.PipeBufferBytes);Assert.Equal(store.ResourceBudget.MemoryLimitBytes,store.ResourceBudget.MemoryBytes);
        }
        using var client=ManagementClient.ForLocalPipe(server.PipeName!);Assert.Equal(store.InstanceId,(await client.InfoAsync(timeout.Token)).InstanceId);
        client.Dispose();await WaitForMemory(store,baseline,timeout.Token);
    }
    [Theory][InlineData(false)][InlineData(true)]public async Task FragmentedFramesReleaseTheReadAheadAndReassemblyBudget(bool malformed){
        using var store=ManagementStore.Initialize(Root(),"admin","BudgetTestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);var baseline=store.ResourceBudget.MemoryBytes;
        var invalid=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);server.ConnectionFailed+=error=>{if(error is JsonException){invalid.TrySetResult();}};server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new RpcRequest(Guid.NewGuid(),"info",null,"",ManagementJson.Element(new{padding=new string('x',32768)})),ManagementJson.Options);
        if(malformed){Array.Fill(bytes,(byte)'x');}
        var header=new byte[4];BinaryPrimitives.WriteInt32BigEndian(header,bytes.Length);
        await using(var pipe=await Connect(server,timeout.Token)){
            await pipe.WriteAsync(header,timeout.Token);await pipe.WriteAsync(bytes.AsMemory(0,1),timeout.Token);await pipe.FlushAsync(timeout.Token);
            while(server.PipeBufferBytes<bytes.Length+64){await Task.Delay(10,timeout.Token);}
            Assert.True(store.ResourceBudget.MemoryBytes>=baseline+bytes.Length+64);
            await pipe.WriteAsync(bytes.AsMemory(1),timeout.Token);await pipe.FlushAsync(timeout.Token);
            if(malformed){await invalid.Task.WaitAsync(timeout.Token);}
            else{Assert.Equal("result",(await ManagementWire.ReadAsync<RpcResponse>(pipe,timeout.Token)).Kind);}
        }
        await WaitForMemory(store,baseline,timeout.Token);Assert.Equal(0,server.PipeBufferBytes);Assert.Equal(0,server.BufferedRequestBytes);
    }
    [Theory][InlineData(false)][InlineData(true)]public async Task IncompleteFramesReleaseAllReservationsOnDisconnectOrStop(bool stop){
        using var store=ManagementStore.Initialize(Root(),"admin","BudgetTestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);var baseline=store.ResourceBudget.MemoryBytes;server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));var stopped=false;
        try{
            await using var pipe=await Connect(server,timeout.Token);var bytes=new byte[5];BinaryPrimitives.WriteInt32BigEndian(bytes,32768);bytes[4]=(byte)'{';
            await pipe.WriteAsync(bytes,timeout.Token);await pipe.FlushAsync(timeout.Token);
            while(server.PipeBufferBytes<32768+64){await Task.Delay(10,timeout.Token);}
            if(stop){await server.DisposeAsync().AsTask().WaitAsync(timeout.Token);stopped=true;}
            else{await pipe.DisposeAsync();}
            await WaitForMemory(store,baseline,timeout.Token);Assert.Equal(0,server.PipeBufferBytes);Assert.Equal(0,server.BufferedRequestBytes);
        }finally{if(!stopped){await server.DisposeAsync();}}
    }
    [Fact]public async Task IncompleteConnectionsShareTheParentLimitAndRecoverAfterRejection(){
        using var store=ManagementStore.Initialize(Root(),"admin","BudgetTestPassword-123");
        using var service=new Seamless.Management.Server.ManagementService(store,null,startAuditWorker:false);
        await using var server=Seamless.Management.Server.ManagementServer.ForLocalOwner(service);var baseline=store.ResourceBudget.MemoryBytes;server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));var held=new List<NamedPipeClientStream>();long observed=0;
        try{
            for(var index=0;index<6;index++){
                var pipe=await Connect(server,timeout.Token);held.Add(pipe);var prefix=new byte[5];BinaryPrimitives.WriteInt32BigEndian(prefix,32768);prefix[4]=(byte)'{';await pipe.WriteAsync(prefix,timeout.Token);await pipe.FlushAsync(timeout.Token);
            }
            while(server.PipeBufferBytes<6L*(32768+64)){await Task.Delay(10,timeout.Token);}observed=server.PipeBufferBytes;Assert.True(store.ResourceBudget.MemoryBytes>=baseline+6L*(32768+64));
            var denied=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);server.ConnectionFailed+=error=>{if(error is ResourceLimitException){denied.TrySetResult();}};
            using(var pressure=store.ResourceBudget.ReserveMemory(store.ResourceBudget.AvailableMemoryBytes-65536)){
                await using var extra=await Connect(server,timeout.Token);var prefix=new byte[5];BinaryPrimitives.WriteInt32BigEndian(prefix,1<<20);prefix[4]=(byte)'{';await extra.WriteAsync(prefix,timeout.Token);await extra.FlushAsync(timeout.Token);
                await denied.Task.WaitAsync(timeout.Token);Assert.True(store.ResourceBudget.MemoryBytes<=store.ResourceBudget.MemoryLimitBytes);
            }
        }finally{foreach(var pipe in held){await pipe.DisposeAsync();}}
        await WaitForMemory(store,baseline,timeout.Token);Assert.Equal(0,server.BufferedRequestBytes);Assert.Equal(0,server.PipeBufferBytes);
        using(var client=ManagementClient.ForLocalPipe(server.PipeName!)){Assert.Equal(store.InstanceId,(await client.InfoAsync(timeout.Token)).InstanceId);}
        await WaitForMemory(store,baseline,timeout.Token);
        await File.WriteAllTextAsync(Path.Combine(store.Root,"ipc-memory-evidence.json"),JsonSerializer.Serialize(new{Connections=6,ParentLimit=store.ResourceBudget.MemoryLimitBytes,Baseline=baseline,ObservedPipeBytes=observed,FinalParentBytes=store.ResourceBudget.MemoryBytes,FinalPipeBytes=server.PipeBufferBytes,FinalFrameBytes=server.BufferedRequestBytes},ManagementJson.Options));
    }
    [Theory][InlineData("success")][InlineData("parse")][InlineData("cancel")][InlineData("end")][InlineData("limit")]
    public async Task ReceivePoolReturnsParentAndFrameReservations(string outcome){
        var limit=256;if(outcome=="limit"){limit=191;}
        var parent=new ResourceBudget(limit,0);using var resources=new ExecutionScope(new(){ParentBudget=parent,MemoryLimitBytes=256,TemporaryStorageLimitBytes=0,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!});
        using var pool=new TestMemoryPool(resources);var input=new System.IO.Pipelines.Pipe();var output=new System.IO.Pipelines.Pipe();var quota=new FrameBufferQuota(128);
        var frames=new LengthPrefixedPipe(new Duplex(input.Reader,output.Writer),128,false,null,null,quota,null,pool);
        using var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try{
            await input.Writer.WriteAsync(new byte[]{128,0,0,0,1},cancellation.Token);
            var reading=frames.ReadAsync(bytes=>{if(outcome=="parse"){throw new FormatException("invalid payload");}return new Packet(bytes.ToArray());},cancellation.Token).AsTask();
            if(outcome=="limit"){await Assert.ThrowsAsync<ResourceLimitException>(()=>reading);Assert.Equal(0,parent.PeakMemoryBytes);}
            else{
                Assert.False(reading.IsCompleted);Assert.Equal(192,parent.MemoryBytes);Assert.Equal(128,quota.UsedBytes);
                if(outcome=="cancel"){cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>reading);}
                else if(outcome=="end"){await input.Writer.CompleteAsync();await Assert.ThrowsAsync<EndOfStreamException>(()=>reading);}
                else{await input.Writer.WriteAsync(new byte[127],cancellation.Token);if(outcome=="parse"){await Assert.ThrowsAsync<FormatException>(()=>reading);}else{Assert.Equal(128,(await reading)!.Bytes.Length);}}
            }
            Assert.Equal(0,parent.MemoryBytes);Assert.Equal(0,quota.UsedBytes);
        }finally{await input.Writer.CompleteAsync();await input.Reader.CompleteAsync();await output.Writer.CompleteAsync();await output.Reader.CompleteAsync();}
    }
    private sealed record Packet(byte[] Bytes);
    private sealed record Duplex(System.IO.Pipelines.PipeReader Input,System.IO.Pipelines.PipeWriter Output):System.IO.Pipelines.IDuplexPipe;
    private sealed class TestMemoryPool(ExecutionScope resources):MemoryPool<byte>{
        public override int MaxBufferSize=>128;
        public override IMemoryOwner<byte> Rent(int minBufferSize=-1)=>resources.Rent(minBufferSize);
        protected override void Dispose(bool disposing){}
    }
    private static async Task<NamedPipeClientStream> Connect(Seamless.Management.Server.ManagementServer server,CancellationToken cancellation){
        var pipe=new NamedPipeClientStream(".",server.PipeName!,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        try{await pipe.ConnectAsync(5000,cancellation);return pipe;}catch{pipe.Dispose();throw;}
    }
    private static async Task WaitForMemory(ManagementStore store,long expected,CancellationToken cancellation){
        while(store.ResourceBudget.MemoryBytes!=expected){await Task.Delay(10,cancellation);}
    }
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"ipc-memory-"+Guid.NewGuid().ToString("N"));
}
