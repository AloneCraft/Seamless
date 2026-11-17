using Seamless.Transactions;
namespace Seamless.Transactions.Tests;

public sealed partial class OptimisticTransactionTests{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task StoppingDuringDrainOrCheckpointNeverAdmitsWaitingRootsAndOtherDomainsProgress(bool physical){
        await using var owner=Coordinator();var table=new Versions(1);owner.Register(table);
        await using var other=Coordinator();var independent=new Versions(2);other.Register(independent);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.ConfigureMaintenance(async()=>{entered.TrySetResult();await release.Task;});using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        TransactionScope? writer=null;if(!physical){writer=await owner.BeginAsync();}
        Task? reclaim=null,admission=null;Exception? reclaimError=null,admissionError=null;var admitted=0;
        try{
            reclaim=OutsideCurrent(async()=>await owner.ReclaimHistoryAsync());
            if(physical){await entered.Task.WaitAsync(timeout.Token);}else{Assert.True(SpinWait.SpinUntil(()=>owner.GetDomainDiagnostics().Reclaiming,TimeSpan.FromSeconds(2)));}
            var attempted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            admission=OutsideCurrent(async()=>{var request=owner.BeginReadOnlyAsync(timeout.Token);var pending=!request.IsCompleted;attempted.TrySetResult();await using var scope=await request;Interlocked.Increment(ref admitted);Assert.True(pending);});
            await attempted.Task.WaitAsync(timeout.Token);Assert.False(admission.IsCompleted);owner.StopAccepting();
            await using(var progress=await other.BeginAsync()){independent.Set(progress,1,150);await progress.CommitAsync();}
            await using(var check=await other.BeginReadOnlyAsync()){Assert.Equal(150,independent.Read(check,1));}
            await Assert.ThrowsAsync<InvalidOperationException>(async()=>await owner.DisposeAsync());
        }finally{
            release.TrySetResult();if(writer is not null){await writer.DisposeAsync();}
            if(reclaim is not null){reclaimError=await Record.ExceptionAsync(async()=>await reclaim.WaitAsync(TimeSpan.FromSeconds(5)));}
            if(admission is not null){admissionError=await Record.ExceptionAsync(async()=>await admission.WaitAsync(TimeSpan.FromSeconds(5)));}
        }
        Assert.IsType<ObjectDisposedException>(admissionError);Assert.Equal(0,admitted);Assert.Equal(0,owner.GetDomainDiagnostics().ActiveTransactions);Assert.Null(owner.Failure.Error);
        if(physical){Assert.Null(reclaimError);Assert.Equal(1,table.Reclaims);}else{Assert.IsType<ObjectDisposedException>(reclaimError);Assert.Equal(0,table.Reclaims);}
        await Assert.ThrowsAsync<ObjectDisposedException>(async()=>{await using var rejected=await owner.BeginReadOnlyAsync();});
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task AdmissionCancellationAndDeadlineDoNotCancelMaintenanceOrAReplacementReader(bool deadline){
        using var resources=new global::Seamless.Execution.ExecutionScope(new(){MemoryLimitBytes=2<<20,TemporaryStorageLimitBytes=4<<20});
        var options=Options();var wait=TimeSpan.FromSeconds(3);if(deadline){wait=TimeSpan.FromMilliseconds(250);}
        options=options with{ParentBudget=resources.Budget,History=options.History! with{MaintenanceWaitTimeout=wait}};
        await using(var owner=new TransactionCoordinator(null,null,null,options)){
            var table=new Versions(1);owner.Register(table);await using(var warm=await owner.BeginReadOnlyAsync()){Assert.Equal(100,table.Read(warm,1));}
            var baselineMemory=resources.Budget.MemoryBytes;var baselineStorage=resources.Budget.TemporaryStorageBytes;
            // ConfigureMaintenanceは初回開始前の契約なので、同じphaseをparticipantの再取得で止める。
            using var reopening=new ManualResetEventSlim();using var resume=new ManualResetEventSlim();
            table.BeforeReopen=()=>{reopening.Set();if(!resume.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException();}};
            using var cancellation=new CancellationTokenSource();Task? reclaim=null,first=null,replacement=null;var admitted=0;
            try{
                reclaim=OutsideCurrent(async()=>await owner.ReclaimHistoryAsync());Assert.True(await Task.Run(()=>reopening.Wait(TimeSpan.FromSeconds(5))));
                var attempted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                first=OutsideCurrent(async()=>{var request=owner.BeginReadOnlyAsync(cancellation.Token);attempted.TrySetResult();await using var scope=await request;Interlocked.Increment(ref admitted);});
                await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(first.IsCompleted);
                if(deadline){await Assert.ThrowsAsync<TimeoutException>(async()=>await first.WaitAsync(TimeSpan.FromSeconds(5)));}
                else{cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await first.WaitAsync(TimeSpan.FromSeconds(5)));}
                Assert.False(reclaim.IsCompleted);Assert.Equal(0,admitted);Assert.Equal(0,owner.GetDomainDiagnostics().ActiveTransactions);
                var replacementAttempted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                replacement=OutsideCurrent(async()=>{var request=owner.BeginReadOnlyAsync();var pending=!request.IsCompleted;replacementAttempted.TrySetResult();await using var scope=await request;Assert.True(pending);Assert.Equal(100,table.Read(scope,1));});
                await replacementAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.False(replacement.IsCompleted);
            }finally{
                resume.Set();
                if(first is not null){_=await Record.ExceptionAsync(async()=>await first.WaitAsync(TimeSpan.FromSeconds(5)));}
                if(reclaim is not null){await reclaim.WaitAsync(TimeSpan.FromSeconds(5));}if(replacement is not null){await replacement.WaitAsync(TimeSpan.FromSeconds(5));}
            }
            Assert.Equal(0,admitted);Assert.Equal(0,owner.GetDomainDiagnostics().ActiveTransactions);Assert.Null(owner.Failure.Error);
            Assert.Equal(baselineMemory,resources.Budget.MemoryBytes);Assert.Equal(baselineStorage,resources.Budget.TemporaryStorageBytes);
        }
        Assert.Equal(0,resources.Budget.MemoryBytes);Assert.Equal(0,resources.Budget.TemporaryStorageBytes);
    }
    [Fact]
    public async Task NewRootsWaitThroughDrainCheckpointAndHandleReopeningWhileTheExistingWriterFinishes(){
        var options=Options();options=options with{History=options.History! with{MaintenanceWaitTimeout=TimeSpan.FromSeconds(10)}};
        await using var owner=new TransactionCoordinator(null,null,null,options);var table=new Versions(1);owner.Register(table);
        var checkpointEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var checkpointRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reopenEntered=new ManualResetEventSlim();using var reopenRelease=new ManualResetEventSlim();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        owner.ConfigureMaintenance(async()=>{checkpointEntered.TrySetResult();await checkpointRelease.Task.WaitAsync(timeout.Token);});
        table.BeforeReopen=()=>{reopenEntered.Set();if(!reopenRelease.Wait(TimeSpan.FromSeconds(10))){throw new TimeoutException();}};
        var writer=await owner.BeginAsync();Task? reclaim=null;var waiting=new List<Task>();var admitted=0;
        async Task StartWaiter(){
            var attempted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var task=OutsideCurrent(async()=>{
                var request=owner.BeginReadOnlyAsync(timeout.Token);var suspended=!request.IsCompleted;attempted.TrySetResult();
                await using var scope=await request;Interlocked.Increment(ref admitted);Assert.True(suspended);Assert.Equal(90,table.Read(scope,1));
            });waiting.Add(task);await attempted.Task.WaitAsync(timeout.Token);
        }
        try{
            reclaim=OutsideCurrent(async()=>await owner.ReclaimHistoryAsync());
            Assert.True(SpinWait.SpinUntil(()=>owner.GetDomainDiagnostics().Reclaiming,TimeSpan.FromSeconds(3)));await StartWaiter();
            writer.CreateSavepoint("before");await using(var child=writer.BeginNested()){table.Set(child,1,80);await child.CommitAsync();}
            writer.RollbackToSavepoint("before");table.Set(writer,1,90);await writer.CommitAsync();
            await checkpointEntered.Task.WaitAsync(timeout.Token);await StartWaiter();Assert.Equal(0,Volatile.Read(ref admitted));Assert.False(reclaim.IsCompleted);
            checkpointRelease.TrySetResult();Assert.True(await Task.Run(()=>reopenEntered.Wait(TimeSpan.FromSeconds(5))));
            await StartWaiter();Assert.Equal(0,Volatile.Read(ref admitted));Assert.False(reclaim.IsCompleted);
        }finally{
            checkpointRelease.TrySetResult();reopenRelease.Set();await writer.DisposeAsync();
            if(reclaim is not null){await reclaim.WaitAsync(TimeSpan.FromSeconds(10));}await Task.WhenAll(waiting).WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(3,admitted);Assert.Equal(1,table.Reclaims);Assert.Equal(0,owner.GetDomainDiagnostics().ActiveTransactions);Assert.Null(owner.Failure.Error);
    }
    [Fact]
    public async Task MaintenanceReportsTheSharedDrainAndPhysicalIntervalsAndFreezesTheCompletedResult(){
        await using var owner=Coordinator();var table=new Versions(1);owner.Register(table);
        Assert.Equal(TransactionMaintenancePhase.None,owner.GetDomainDiagnostics().Maintenance.Phase);
        using var readEntered=new ManualResetEventSlim();using var readRelease=new ManualResetEventSlim();
        var physicalEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var physicalRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.ConfigureMaintenance(async()=>{physicalEntered.TrySetResult();await physicalRelease.Task;});
        await using var old=await owner.BeginReadOnlyAsync();
        table.BeforeRead=()=>{readEntered.Set();if(!readRelease.Wait(TimeSpan.FromSeconds(5))){throw new TimeoutException();}};
        var read=Task.Run(()=>table.Read(old,1));Task? first=null,joined=null;
        TransactionMaintenanceDiagnostics physical=default;
        try{
            Assert.True(await Task.Run(()=>readEntered.Wait(TimeSpan.FromSeconds(5))));
            first=OutsideCurrent(async()=>await owner.ReclaimHistoryAsync());
            Assert.True(await Task.Run(()=>SpinWait.SpinUntil(()=>owner.GetDomainDiagnostics().Maintenance.Phase==TransactionMaintenancePhase.Draining,TimeSpan.FromSeconds(3))));
            var draining=owner.GetDomainDiagnostics().Maintenance;
            Assert.Equal(TransactionMaintenanceStep.None,draining.Step);
            Assert.Equal(1,draining.Sequence);Assert.False(draining.PhysicalStarted);Assert.Equal(TimeSpan.Zero,draining.PhysicalWork);Assert.True(draining.DrainWait>TimeSpan.Zero);
            var joinedStarted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            joined=OutsideCurrent(async()=>{var pending=owner.ReclaimHistoryAsync();joinedStarted.TrySetResult();await pending;});
            await joinedStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            readRelease.Set();
            Assert.Equal(TransactionAbortReason.HistoryPressure,(await Assert.ThrowsAsync<TransactionAbortedException>(async()=>await read)).Reason);
            await physicalEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));physical=owner.GetDomainDiagnostics().Maintenance;
            Assert.Equal(TransactionMaintenanceStep.Checkpoint,physical.Step);
            Assert.Equal(TransactionMaintenancePhase.Physical,physical.Phase);Assert.True(physical.PhysicalStarted);Assert.Equal(draining.Sequence,physical.Sequence);Assert.True(physical.DrainWait>=draining.DrainWait);
            Assert.False(first.IsCompleted);Assert.False(joined.IsCompleted);Assert.Equal(physical.DrainWait+physical.PhysicalWork,physical.Total);
        }finally{
            readRelease.Set();physicalRelease.TrySetResult();
            if(first is not null){await first.WaitAsync(TimeSpan.FromSeconds(5));}
            if(joined is not null){await joined.WaitAsync(TimeSpan.FromSeconds(5));}
        }
        var completed=owner.GetDomainDiagnostics().Maintenance;
        Assert.Equal(TransactionMaintenanceStep.Finalize,completed.Step);
        Assert.Equal(TransactionMaintenancePhase.Completed,completed.Phase);Assert.Equal(1,completed.Sequence);Assert.True(completed.PhysicalStarted);
        Assert.Equal(physical.DrainWait,completed.DrainWait);Assert.True(completed.PhysicalWork>=physical.PhysicalWork);Assert.Equal(completed.DrainWait+completed.PhysicalWork,completed.Total);
        Assert.Equal(completed,owner.GetDomainDiagnostics().Maintenance);Assert.Equal(1,table.Reclaims);
        await OutsideCurrent(async()=>await owner.ReclaimHistoryAsync());Assert.Equal(2,owner.GetDomainDiagnostics().Maintenance.Sequence);
    }
    [Fact]
    public async Task DrainTimeoutReportsNoPhysicalWorkAndTheNextReclaimCanSucceed(){
        var options=Options();options=options with{History=options.History! with{MaintenanceWaitTimeout=TimeSpan.FromMilliseconds(100)}};
        await using var owner=new TransactionCoordinator(null,null,null,options);owner.Register(new Versions(1));
        await using(var update=await owner.BeginAsync()){
            await Assert.ThrowsAsync<TimeoutException>(async()=>await OutsideCurrent(async()=>await owner.ReclaimHistoryAsync()));
            var failed=owner.GetDomainDiagnostics().Maintenance;
            Assert.Equal(TransactionMaintenancePhase.Failed,failed.Phase);Assert.Equal(1,failed.Sequence);Assert.False(failed.PhysicalStarted);
            Assert.Equal(TimeSpan.Zero,failed.PhysicalWork);Assert.Equal(failed.DrainWait,failed.Total);Assert.True(failed.Total>TimeSpan.Zero);Assert.Null(owner.Failure.Error);
            await update.RollbackAsync();
        }
        await owner.ReclaimHistoryAsync();var completed=owner.GetDomainDiagnostics().Maintenance;
        Assert.Equal(TransactionMaintenancePhase.Completed,completed.Phase);Assert.Equal(2,completed.Sequence);Assert.True(completed.PhysicalStarted);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public async Task PhysicalFailureIsRecordedForReclaimCheckpointAndHandleReopening(int phase){
        await using var owner=Coordinator();var table=new Versions(1);owner.Register(table);var expected=new IOException("maintenance failure");
        if(phase==0){table.BeforeReclaim=()=>throw expected;}else if(phase==1){owner.ConfigureMaintenance(()=>ValueTask.FromException(expected));}else{table.BeforeReopen=()=>throw expected;}
        Assert.Same(expected,await Assert.ThrowsAsync<IOException>(async()=>await owner.ReclaimHistoryAsync()));
        var diagnostics=owner.GetDomainDiagnostics();var failed=diagnostics.Maintenance;
        Assert.Equal(TransactionMaintenancePhase.Failed,failed.Phase);Assert.True(failed.PhysicalStarted);Assert.Equal(1,failed.Sequence);Assert.False(diagnostics.Reclaiming);
        Assert.True(failed.DrainWait>=TimeSpan.Zero);Assert.True(failed.PhysicalWork>=TimeSpan.Zero);Assert.Equal(failed.DrainWait+failed.PhysicalWork,failed.Total);
        Assert.NotNull(owner.Failure.Error);Assert.Equal(failed,owner.GetDomainDiagnostics().Maintenance);
        Assert.Equal(1,table.MaintenanceFaults);
        await Assert.ThrowsAnyAsync<Exception>(async()=>{await using var rejected=await owner.BeginReadOnlyAsync();});
        Assert.Equal(0,owner.GetDomainDiagnostics().ActiveTransactions);
    }
}
