using Seamless.Management;
using Seamless.Management.Server;
using Seamless.Transactions;

namespace Management.Tests;

[Collection("AuditStorage")]
public sealed class AuditDeliveryTests{
    private static string Root()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")??throw new InvalidOperationException("Missing isolated root."),"audit-delivery-"+Guid.NewGuid().ToString("N"));
    private static WalOptions Options()=>new(){MaxFrameBytes=128<<10,QueueBytes=256<<10,SegmentBytes=1<<20,MetadataConsumer="audit-v1",CatalogIdentity="audit-delivery-tests"};
    private static AuditEvent Event(string message="注文の更新")=>new(Guid.NewGuid(),null,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,Guid.NewGuid(),"user","sales","orders",AuditAction.DataChange,AuditOutcome.Succeeded,7,"code-1",Guid.NewGuid(),"OK",message,"Company.Sales","Orders","Save");
    private static async Task Append(TransactionJournal journal,byte[] payload,Guid transaction){var result=await journal.AppendAsync(new(transaction,[0],[new(0,1,WalMutation.Upsert,"test-v1",[1],[2])],payload));await result.Durable;}
    [Fact]public async Task DeliveryPersistsQueryableMetadataBeforeAcknowledgement(){
        var targetRoot=Root();using(var store=ManagementStore.Initialize(targetRoot,"admin","TestPassword-123")){await using var source=new TransactionJournal(Root(),Options());var item=Event();await Append(source,AuditWalPayload.Encode(store.InstanceId,source.Device.WalId,item),item.TransactionId!.Value);var worker=new AuditDelivery();worker.Checkpoint=phase=>{if(phase=="TargetCommitted"){Assert.Equal(0,source.MetadataAcknowledgedLsn);Assert.Equal(item,Assert.Single(store.AuditEvents(new(Text:"company.sales.orders.save 注文")).Items));}};var result=await worker.DeliverOnceAsync(source,store);Assert.Equal(1,result.EventCount);Assert.Equal(1,source.MetadataAcknowledgedLsn);Assert.False(result.HasMore);Assert.Equal(0,(await worker.DeliverOnceAsync(source,store)).EventCount);}
        using var reopened=new ManagementStore(targetRoot);Assert.Single(reopened.AuditEvents(new()).Items);
    }
    [Fact]public async Task ReplayAfterTargetCommitAndBeforeAckIsIdempotentAfterReopen(){
        var targetRoot=Root();var sourceRoot=Root();using(var store=ManagementStore.Initialize(targetRoot,"admin","TestPassword-123")){await using var source=new TransactionJournal(sourceRoot,Options());var item=Event();await Append(source,AuditWalPayload.Encode(store.InstanceId,source.Device.WalId,item),item.TransactionId!.Value);var worker=new AuditDelivery{Checkpoint=phase=>{if(phase=="TargetCommitted"){throw new IOException("injected after commit");}}};await Assert.ThrowsAsync<IOException>(()=>worker.DeliverOnceAsync(source,store));Assert.Equal(0,source.MetadataAcknowledgedLsn);Assert.Single(store.AuditEvents(new()).Items);}
        using var restored=new ManagementStore(targetRoot);await using var reopened=new TransactionJournal(sourceRoot,Options());await new AuditDelivery().DeliverOnceAsync(reopened,restored);Assert.Equal(1,reopened.MetadataAcknowledgedLsn);Assert.Single(restored.AuditEvents(new()).Items);
    }
    [Theory][InlineData("target")][InlineData("wal")][InlineData("transaction")][InlineData("malformed")][InlineData("conflict")]
    public async Task InvalidPageRollsBackEveryEventAndDoesNotAcknowledge(string failure){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");await using var source=new TransactionJournal(Root(),Options());var first=Event();await Append(source,AuditWalPayload.Encode(store.InstanceId,source.Device.WalId,first),first.TransactionId!.Value);var second=Event();var target=store.InstanceId;var wal=source.Device.WalId;var tx=second.TransactionId!.Value;if(failure=="target"){target=Guid.NewGuid();}if(failure=="wal"){wal=Guid.NewGuid();}if(failure=="transaction"){tx=Guid.NewGuid();}if(failure=="conflict"){second=second with{EventId=first.EventId};}var payload=AuditWalPayload.Encode(target,wal,second);if(failure=="malformed"){payload=payload[..^1];}await Append(source,payload,tx);
        await Assert.ThrowsAnyAsync<Exception>(()=>new AuditDelivery().DeliverOnceAsync(source,store));Assert.Equal(0,source.MetadataAcknowledgedLsn);Assert.Empty(store.AuditEvents(new()).Items);
    }
    [Fact]public async Task ConcurrentWorkersForOneWalDoNotDuplicateEvents(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");await using var source=new TransactionJournal(Root(),Options());var item=Event();await Append(source,AuditWalPayload.Encode(store.InstanceId,source.Device.WalId,item),item.TransactionId!.Value);await Task.WhenAll(Enumerable.Range(0,4).Select(_=>new AuditDelivery().DeliverOnceAsync(source,store)));Assert.Single(store.AuditEvents(new()).Items);Assert.Equal(1,source.MetadataAcknowledgedLsn);
    }
    [Fact]public async Task CancellationBeforeDeliveryLeavesBothStoresUnchanged(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");await using var source=new TransactionJournal(Root(),Options());var item=Event();await Append(source,AuditWalPayload.Encode(store.InstanceId,source.Device.WalId,item),item.TransactionId!.Value);using var cancel=new CancellationTokenSource();cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new AuditDelivery().DeliverOnceAsync(source,store,cancel.Token));Assert.Empty(store.AuditEvents(new()).Items);Assert.Equal(0,source.MetadataAcknowledgedLsn);
    }
    [Fact]public void ReferencedWireLibraryDoesNotOpenAnExternalIoRouteForUserCode(){
        foreach(var body in new[]{"return Seamless.Database.Client.NativeWireValidation.For<int>().GetHashCode();","Seamless.Database.Client.NativeQueryClient value=null;return 1;"}){var compiled=new RuntimeCompiler().Compile(new("1",[new("Query.cs","public static class Q{public static int Run(){"+body+"}}")],[]),entryType:"Q",entryMember:"Run");Assert.Empty(compiled.AssemblyBytes);Assert.Contains(compiled.Result.Diagnostics,item=>item.Code=="ManagementCodePolicy");}
    }
    [Theory][InlineData("BeforeTargetCommit",0,false)][InlineData("TargetCommitted",0,true)][InlineData("Acknowledged",1,true)]
    public async Task ProcessTerminationKeepsTargetAndAcknowledgementConsistent(string phase,long ack,bool saved){
        var targetRoot=Root();var sourceRoot=Root();var start=new System.Diagnostics.ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var argument in new[]{typeof(CrashProbe).Assembly.Location,"--audit-delivery-probe",targetRoot,sourceRoot,phase}){start.ArgumentList.Add(argument);}
        using var child=System.Diagnostics.Process.Start(start)!;var created=child.StartTime.ToUniversalTime();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{
            var stdout=child.StandardOutput.ReadToEndAsync(timeout.Token);var stderr=child.StandardError.ReadToEndAsync(timeout.Token);await child.WaitForExitAsync(timeout.Token);Assert.Equal(137,child.ExitCode);Assert.Empty(await stderr);var receipt=System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(await stdout);Assert.Equal(child.Id,receipt.GetProperty("Pid").GetInt32());Assert.Equal(created,receipt.GetProperty("Created").GetDateTime());if(OperatingSystem.IsWindows()||OperatingSystem.IsLinux()){using var current=System.Diagnostics.Process.GetCurrentProcess();Assert.Equal(current.ProcessorAffinity.ToInt64(),receipt.GetProperty("Affinity").GetInt64());}
        }finally{if(!child.HasExited){child.Kill(entireProcessTree:true);await child.WaitForExitAsync();}}
        using var store=new ManagementStore(targetRoot);await using var source=new TransactionJournal(sourceRoot,Options());Assert.Equal(ack,source.MetadataAcknowledgedLsn);Assert.Equal(saved,store.AuditEvents(new()).Items.Length==1);await new AuditDelivery().DeliverOnceAsync(source,store);Assert.Equal(1,source.MetadataAcknowledgedLsn);Assert.Single(store.AuditEvents(new()).Items);
    }
    internal static async Task<int> RunStopProbeAsync(string targetRoot,string sourceRoot,string phase){
        using var store=ManagementStore.Initialize(targetRoot,"admin","TestPassword-123");await using var source=new TransactionJournal(sourceRoot,Options());var item=Event();await Append(source,AuditWalPayload.Encode(store.InstanceId,source.Device.WalId,item),item.TransactionId!.Value);var worker=new AuditDelivery{Checkpoint=current=>{if(current!=phase){return;}using var process=System.Diagnostics.Process.GetCurrentProcess();long? affinity=null;if(OperatingSystem.IsWindows()||OperatingSystem.IsLinux()){affinity=process.ProcessorAffinity.ToInt64();}Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{Pid=process.Id,Created=process.StartTime.ToUniversalTime(),Affinity=affinity}));Console.Out.Flush();Environment.Exit(137);}};await worker.DeliverOnceAsync(source,store);return 3;
    }
    [Fact]public async Task ForgedStringLengthIsRejectedBeforeMaterialization(){
        using var store=ManagementStore.Initialize(Root(),"admin","TestPassword-123");await using var source=new TransactionJournal(Root(),Options());var item=Event();var payload=AuditWalPayload.Encode(store.InstanceId,source.Device.WalId,item);var index=payload.AsSpan().IndexOf("user"u8);Assert.True(index>80);System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(index-8,4),int.MinValue);await Append(source,payload,item.TransactionId!.Value);var before=source.Budget.MemoryBytes;await Assert.ThrowsAnyAsync<Exception>(()=>new AuditDelivery().DeliverOnceAsync(source,store));Assert.Equal(before,source.Budget.MemoryBytes);Assert.Equal(0,source.MetadataAcknowledgedLsn);Assert.Empty(store.AuditEvents(new()).Items);
    }
}
