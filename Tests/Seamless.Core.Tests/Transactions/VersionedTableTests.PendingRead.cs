using System.Reflection;
using System.Runtime.ExceptionServices;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Transactions.Collections;
using Tsavorite.core;
using Store=Seamless.Execution.Store;
namespace Seamless.Transactions.Tests;

public sealed partial class VersionedTableTests{
    [Fact]
    public async Task NativePendingReadKeepsItsOperationUntilTheRealIoCallbackIsReleased(){
        var path=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"pending-read-"+Guid.NewGuid().ToString("N"));
        using var resources=new ExecutionScope(new(){MemoryLimitBytes=32L<<20,TemporaryStorageLimitBytes=16L<<20,TemporaryDirectory=path});
        using var store=Store.Open(path,new(){MemoryBytes=24L<<20,DiskBytes=16L<<20},parentBudget:resources.Budget);
        var data=store.CreateTsavoriteHistory("rows",TuningForMutableRows(),VersionedTransactionalTable<int,(int,long)>.StorageIdentity("pending",VersionedRecordEncoding.Canonical));
        using var table=new VersionedTransactionalTable<int,(int Id,long Balance)>(0,1,"pending",data,resources,1024,static row=>row.Id);
        await using var owner=new TransactionCoordinator(null,null,null,new(){Concurrency=TransactionConcurrencyMode.Optimistic,ParentBudget=resources.Budget,
            History=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=8<<20,MaxMetadataBytes=256<<10,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=64<<10,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}});owner.Register(table);
        await using(var seed=await owner.BeginAsync()){table.For(seed).Add((1,100));await seed.CommitAsync();}
        data.Store.Log.FlushAndEvict(true);
        // このfixtureのデバイスだけを包む。実ファイルのI/Oは元デバイスへ渡し、完了通知だけを保留する。
        // 公開製品APIへ試験専用hookを加えず、readonly fieldや他のインスタンスは書き換えない。
        PropertyInfo? settingsProperty=null;for(var type=data.GetType();type is not null&&settingsProperty is null;type=type.BaseType){settingsProperty=type.GetProperty("Settings",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);}
        var settings=(KVSettings)settingsProperty!.GetValue(data)!;var deferred=settings.LogDevice;var innerField=deferred.GetType().GetField("inner",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var original=(IDevice)innerField.GetValue(deferred)!;var device=DispatchProxy.Create<IDevice,PendingReadProxy>();var probe=(PendingReadProxy)(object)device;probe.Inner=original;innerField.SetValue(deferred,device);
        var generationField=typeof(HistoryStore).GetField("generation",BindingFlags.Instance|BindingFlags.NonPublic)!;var generation=(long)generationField.GetValue(data)!;
        await using var read=await owner.BeginReadOnlyAsync();Task? reading=null;Task? reclaim=null;Exception? readError=null;
        try{
            using(ExecutionContext.SuppressFlow()){reading=Task.Run(()=>{Assert.Equal(100,table.For(read)[1].Balance);});}
            await probe.Captured.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(0u,probe.ErrorCode);Assert.True(probe.ReadBytes>0);Assert.False(reading.IsCompleted);
            reclaim=OutsideCurrent(async()=>await owner.ReclaimHistoryAsync());Assert.True(SpinWait.SpinUntil(()=>owner.GetDomainDiagnostics().Reclaiming,TimeSpan.FromSeconds(2)));
            // 回収の開始flagだけでは中止要求前と競合する。寿命検査が実際に中止を返すまで待つ。
            Assert.True(SpinWait.SpinUntil(()=>Record.Exception(()=>read.ValidateOwner(owner)) is TransactionAbortedException{Reason:TransactionAbortReason.HistoryPressure},TimeSpan.FromSeconds(2)));
            Assert.False(reclaim.IsCompleted);Assert.True(owner.GetDomainDiagnostics().ActiveOperations>0);Assert.False(owner.GetDomainDiagnostics().Maintenance.PhysicalStarted);Assert.Equal(generation,(long)generationField.GetValue(data)!);
        }finally{
            try{
                probe.Release();if(reading is not null){readError=await Record.ExceptionAsync(async()=>await reading.WaitAsync(TimeSpan.FromSeconds(10)));}
                if(reclaim is not null){await reclaim.WaitAsync(TimeSpan.FromSeconds(10));}
            }finally{if(ReferenceEquals(innerField.GetValue(deferred),device)){innerField.SetValue(deferred,original);}}
        }
        Assert.Equal(TransactionAbortReason.HistoryPressure,Assert.IsType<TransactionAbortedException>(readError).Reason);
        Assert.True((long)generationField.GetValue(data)!>generation);Assert.Equal(0,owner.GetDomainDiagnostics().ActiveOperations);Assert.Null(owner.Failure.Error);
        await OutsideCurrent(async()=>{await using var current=await owner.BeginReadOnlyAsync();Assert.Equal(100,table.For(current)[1].Balance);});
    }
    public class PendingReadProxy:DispatchProxy{
        private readonly object gate=new();private bool armed=true;private Action? pending;
        internal IDevice Inner=null!;
        internal readonly TaskCompletionSource Captured=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal uint ErrorCode,ReadBytes;
        protected override object? Invoke(MethodInfo? method,object?[]? args){
            if(method!.Name==nameof(IDevice.ReadAsync)){
                var index=Array.FindIndex(args!,static value=>value is DeviceIOCompletionCallback);var callback=(DeviceIOCompletionCallback)args![index]!;
                args[index]=new DeviceIOCompletionCallback((error,bytes,context,exception)=>{
                    lock(this.gate){if(this.armed){this.armed=false;this.ErrorCode=error;this.ReadBytes=bytes;this.pending=()=>callback(error,bytes,context,exception);this.Captured.TrySetResult();return;}}
                    callback(error,bytes,context,exception);
                });
            }
            try{return method.Invoke(this.Inner,args);}catch(TargetInvocationException error)when(error.InnerException is not null){ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        }
        internal void Release(){Action? resume;lock(this.gate){this.armed=false;resume=this.pending;this.pending=null;}resume?.Invoke();}
    }
}
