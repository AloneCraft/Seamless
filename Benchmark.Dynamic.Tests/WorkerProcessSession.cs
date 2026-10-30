using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Infrastructure;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class WorkerProcessSession{
    private static readonly TimeSpan HangTimeout=TimeSpan.FromSeconds(30);

    [Fact,Trait("Category","Unit")]
    public void ForcedTerminationDelay(){
        Assert.Equal(TimeSpan.FromSeconds(3),global::Benchmark.Dynamic.Infrastructure.WorkerProcessSession.ForcedTerminationDelay);
    }

    [Fact,Trait("Category","Integration")]
    public async Task CancelAsync(){
        var workerPath=Path.Combine(AppContext.BaseDirectory,"Benchmark.Dynamic.Worker.exe");
        var gateEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workerStarted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var phases=new ConcurrentQueue<string>();
        var observations=new List<TaskObservation>();
        var operationID=Guid.NewGuid().ToString("N");
        using var process=new Process{
            StartInfo=VersionedJsonLineWorkerProcessClient.CreateStartInfo(workerPath),
            EnableRaisingEvents=true,
        };
        global::Benchmark.Dynamic.Infrastructure.WorkerProcessSession? session=null;
        Task? processExit=null;
        Task? responses=null;
        Task<bool>? cancel=null;
        Task<bool>? shutdown=null;
        Task? dispose=null;
        Exception? scenarioFailure=null;
        var cleanupFailures=new List<ExceptionObservation>();
        var processStarted=false;
        var contractReached=false;
        var shutdownCompletedEarly=false;
        var disposeCompletedEarly=false;
        int? processID=null;
        DateTime? processCreatedAtUtc=null;
        string? standardError=null;
        try{
            Assert.True(File.Exists(workerPath),$"Worker was not found: {workerPath}");
            processStarted=process.Start();
            Assert.True(processStarted);
            processExit=process.WaitForExitAsync();
            processID=process.Id;
            processCreatedAtUtc=process.StartTime.ToUniversalTime();
            session=new(process,async()=>{
                phases.Enqueue("FirstExitTaskCompleted");
                gateEntered.TrySetResult();
                await gateRelease.Task.ConfigureAwait(false);
                phases.Enqueue("StopGateReleased");
            });
            responses=ReadResponsesAsync(session,workerStarted,phases);
            await workerStarted.Task.WaitAsync(HangTimeout);
            cancel=session.CancelAsync("cancel-"+operationID);
            await gateEntered.Task.WaitAsync(HangTimeout);
            await processExit.WaitAsync(HangTimeout);
            await responses.WaitAsync(HangTimeout);
            standardError=await session.GetStandardErrorAsync().WaitAsync(HangTimeout);
            phases.Enqueue("StandardErrorCompleted");

            // OS終了と両streamのEOF後にも、先行する停止処理の完了はgateで保留されている。
            shutdown=session.ShutdownAsync("shutdown-"+operationID);
            shutdownCompletedEarly=shutdown.IsCompleted;
            phases.Enqueue("ShutdownInvoked");
            dispose=session.DisposeAsync().AsTask();
            disposeCompletedEarly=dispose.IsCompleted;
            phases.Enqueue("DisposeInvoked");
            contractReached=true;
        }catch(Exception exception){
            scenarioFailure=exception;
        }finally{
            // 契約assertionより先にgateを解放し、自分が起動したProcessと全Taskを回収する。
            gateRelease.TrySetResult();
            if(processStarted&&processExit?.IsCompletedSuccessfully!=true){
                try{
                    if(!process.HasExited){
                        process.Kill(true);
                        phases.Enqueue("OwnedWorkerKilledDuringCleanup");
                    }
                }catch(Exception exception){
                    cleanupFailures.Add(DescribeException(exception));
                }
            }
            if(session is not null&&dispose is null){
                try{
                    dispose=session.DisposeAsync().AsTask();
                }catch(Exception exception){
                    cleanupFailures.Add(DescribeException(exception));
                }
            }
            foreach(var task in new[]{
                (Name:"ProcessExit",Task:processExit),
                (Name:"Responses",Task:responses),
                (Name:"Cancel",Task:(Task?)cancel),
                (Name:"Shutdown",Task:(Task?)shutdown),
                (Name:"Dispose",Task:dispose),
            }){
                if(task.Task is not null){
                    observations.Add(await ObserveAsync(task.Name,task.Task));
                }
            }
        }

        ExceptionObservation? scenarioFailureObservation=null;
        if(scenarioFailure is not null){
            scenarioFailureObservation=DescribeException(scenarioFailure);
        }
        var artifact=new{
            Scenario="CancelShutdownDisposeJoin",
            OperationID=operationID,
            Baseline="82c92ef9de535bad80ae1f0608805ebd90cb7968",
            OriginalFailureLocation="VersionedJsonLineWorkerProcessClient.cs:221 (second WaitForExitAsync)",
            WorkerPath=workerPath,
            ProcessID=processID,
            ProcessCreatedAtUtc=processCreatedAtUtc,
            ContractReached=contractReached,
            ShutdownCompletedBeforeStopGateRelease=shutdownCompletedEarly,
            DisposeCompletedBeforeStopGateRelease=disposeCompletedEarly,
            StandardError=standardError,
            Phases=phases.ToArray(),
            Tasks=observations,
            ScenarioFailure=scenarioFailureObservation,
            CleanupFailures=cleanupFailures,
        };
        var json=JsonSerializer.Serialize(artifact,new JsonSerializerOptions{WriteIndented=true});
        var artifactDirectory=ArtifactDirectory();
        Directory.CreateDirectory(artifactDirectory);
        var artifactPath=Path.Combine(artifactDirectory,"worker-stop-ownership-"+operationID+".json");
        await File.WriteAllTextAsync(artifactPath,json);
        var failureMessage=$"Observation: {artifactPath}{Environment.NewLine}{json}";
        Assert.True(scenarioFailure is null,failureMessage);
        Assert.True(contractReached,failureMessage);
        Assert.False(shutdownCompletedEarly,failureMessage);
        Assert.False(disposeCompletedEarly,failureMessage);
        Assert.True(cleanupFailures.Count==0,failureMessage);
        Assert.True(observations.All(static observation=>observation.Exception is null),failureMessage);
    }

    [Fact,Trait("Category","Integration")]
    public async Task DisposeAsync(){
        {
            // Disposeが最初に停止を開始しても、重複停止と終了済みsnapshotは同じ完了を待つ。
            await VerifySharedStopAsync(disposeFirst:true,failStop:false);
        }
        {
            // 停止末尾で注入した失敗は、重複Cancel/Shutdown/Disposeとsnapshotにも残る。
            await VerifySharedStopAsync(disposeFirst:false,failStop:true);
        }
    }

    private static async Task VerifySharedStopAsync(bool disposeFirst,bool failStop){
        var workerPath=Path.Combine(AppContext.BaseDirectory,"Benchmark.Dynamic.Worker.exe");
        var operationID=Guid.NewGuid().ToString("N");
        var gateEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workerStarted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var phases=new ConcurrentQueue<string>();
        var operations=new List<(string Name,Task Task,bool IsStop)>();
        var observations=new List<TaskObservation>();
        var failures=new Dictionary<string,Exception?>(StringComparer.Ordinal);
        var cleanupFailures=new List<ExceptionObservation>();
        var expectedFailure=new IOException("Injected failure after confirmed Worker exit.");
        using var process=new Process{
            StartInfo=VersionedJsonLineWorkerProcessClient.CreateStartInfo(workerPath),
            EnableRaisingEvents=true,
        };
        global::Benchmark.Dynamic.Infrastructure.WorkerProcessSession? session=null;
        Task? processExit=null;
        Task? dispose=null;
        Exception? scenarioFailure=null;
        var started=false;
        var contractReached=false;
        var anyStopCompletedEarly=false;
        var phaseInvocationCount=0;
        int? processID=null;
        DateTime? processCreatedAtUtc=null;
        int? cachedProcessID=null;
        var cachedHasExited=false;
        var processAssociationRetained=false;
        try{
            Assert.True(File.Exists(workerPath),$"Worker was not found: {workerPath}");
            started=process.Start();
            Assert.True(started);
            processExit=process.WaitForExitAsync();
            operations.Add(("ProcessExit",processExit,false));
            processID=process.Id;
            processCreatedAtUtc=process.StartTime.ToUniversalTime();
            session=new(process,async()=>{
                Interlocked.Increment(ref phaseInvocationCount);
                phases.Enqueue("StopPhaseEntered");
                gateEntered.TrySetResult();
                await gateRelease.Task.ConfigureAwait(false);
                if(failStop){
                    throw expectedFailure;
                }
            });
            operations.Add(("Responses",ReadResponsesAsync(session,workerStarted,phases),false));
            await workerStarted.Task.WaitAsync(HangTimeout);
            if(disposeFirst){
                dispose=session.DisposeAsync().AsTask();
                operations.Add(("InitialDispose",dispose,true));
            }else{
                operations.Add(("InitialCancel",session.CancelAsync("initial-"+operationID),true));
            }
            await gateEntered.Task.WaitAsync(HangTimeout);
            operations.Add(("Cancel",session.CancelAsync("cancel-"+operationID),true));
            operations.Add(("DuplicateCancel",session.CancelAsync("cancel-again-"+operationID),true));
            operations.Add(("Shutdown",session.ShutdownAsync("shutdown-"+operationID),true));
            dispose=session.DisposeAsync().AsTask();
            operations.Add(("Dispose",dispose,true));
            operations.Add(("DuplicateDispose",session.DisposeAsync().AsTask(),true));
            anyStopCompletedEarly=operations.Any(static operation=>operation.IsStop&&operation.Task.IsCompleted);
            contractReached=true;
        }catch(Exception exception){
            scenarioFailure=exception;
        }finally{
            gateRelease.TrySetResult();
            if(started&&processExit?.IsCompletedSuccessfully!=true){
                try{
                    if(!process.HasExited){
                        process.Kill(true);
                    }
                }catch(Exception exception){
                    cleanupFailures.Add(DescribeException(exception));
                }
            }
            if(session is not null&&dispose is null){
                try{
                    dispose=session.DisposeAsync().AsTask();
                    operations.Add(("CleanupDispose",dispose,true));
                }catch(Exception exception){
                    cleanupFailures.Add(DescribeException(exception));
                }
            }
            foreach(var operation in operations){
                var failure=await Record.ExceptionAsync(async()=>{
                    await operation.Task.WaitAsync(HangTimeout);
                });
                failures.Add(operation.Name,failure);
                ExceptionObservation? failureObservation=null;
                if(failure is not null){
                    failureObservation=DescribeException(failure);
                }
                observations.Add(new(operation.Name,operation.Task.Status.ToString(),failureObservation));
            }
        }

        if(session is not null&&contractReached){
            try{
                IWorkerProcessSession retainedSnapshot=session;
                cachedProcessID=retainedSnapshot.ProcessID;
                cachedHasExited=retainedSnapshot.HasExited;
                _=await retainedSnapshot.GetStandardErrorAsync().WaitAsync(HangTimeout);
                if(failStop){
                    processAssociationRetained=process.Id==processID;
                }
                foreach(var operation in new[]{
                    (Name:"SnapshotCancel",Task:(Task)retainedSnapshot.CancelAsync("snapshot-cancel-"+operationID)),
                    (Name:"SnapshotShutdown",Task:(Task)retainedSnapshot.ShutdownAsync("snapshot-shutdown-"+operationID)),
                    (Name:"SnapshotDispose",Task:retainedSnapshot.DisposeAsync().AsTask()),
                }){
                    var failure=await Record.ExceptionAsync(async()=>{
                        await operation.Task.WaitAsync(HangTimeout);
                    });
                    failures.Add(operation.Name,failure);
                    ExceptionObservation? failureObservation=null;
                    if(failure is not null){
                        failureObservation=DescribeException(failure);
                    }
                    observations.Add(new(operation.Name,operation.Task.Status.ToString(),failureObservation));
                    operations.Add((operation.Name,operation.Task,true));
                }
            }catch(Exception exception){
                scenarioFailure=exception;
            }
        }

        ExceptionObservation? scenarioFailureObservation=null;
        if(scenarioFailure is not null){
            scenarioFailureObservation=DescribeException(scenarioFailure);
        }
        var artifact=new{
            Scenario="SharedStopCompletionAndFailure",
            OperationID=operationID,
            DisposeFirst=disposeFirst,
            InjectedStopFailure=failStop,
            WorkerPath=workerPath,
            ProcessID=processID,
            ProcessCreatedAtUtc=processCreatedAtUtc,
            ContractReached=contractReached,
            AnyStopCompletedBeforeGateRelease=anyStopCompletedEarly,
            PhaseInvocationCount=phaseInvocationCount,
            CachedProcessID=cachedProcessID,
            CachedHasExited=cachedHasExited,
            ProcessAssociationRetainedAfterFailure=processAssociationRetained,
            SameFailureObservedByAllStopWaiters=failStop&&operations.Where(static operation=>operation.IsStop)
                .All(operation=>failures.TryGetValue(operation.Name,out var failure)&&ReferenceEquals(expectedFailure,failure)),
            Phases=phases.ToArray(),
            Tasks=observations,
            ScenarioFailure=scenarioFailureObservation,
            CleanupFailures=cleanupFailures,
        };
        var json=JsonSerializer.Serialize(artifact,new JsonSerializerOptions{WriteIndented=true});
        var artifactDirectory=ArtifactDirectory();
        Directory.CreateDirectory(artifactDirectory);
        var artifactPath=Path.Combine(artifactDirectory,"worker-shared-stop-"+operationID+".json");
        await File.WriteAllTextAsync(artifactPath,json);
        var failureMessage=$"Observation: {artifactPath}{Environment.NewLine}{json}";
        Assert.True(scenarioFailure is null,failureMessage);
        Assert.True(contractReached,failureMessage);
        Assert.False(anyStopCompletedEarly,failureMessage);
        Assert.Equal(1,phaseInvocationCount);
        Assert.Equal(processID,cachedProcessID);
        Assert.True(cachedHasExited,failureMessage);
        Assert.True(cleanupFailures.Count==0,failureMessage);
        foreach(var operation in operations){
            if(failStop&&operation.IsStop){
                Assert.Same(expectedFailure,failures[operation.Name]);
            }else{
                Assert.True(failures[operation.Name] is null,failureMessage);
            }
        }
        if(failStop){
            Assert.True(processAssociationRetained,failureMessage);
        }else{
            var stopResults=operations.Where(static operation=>operation.Task is Task<bool>)
                .Select(static operation=>((Task<bool>)operation.Task).Result).Distinct().ToArray();
            Assert.Single(stopResults);
        }
    }

    private static async Task ReadResponsesAsync(
        IWorkerProcessSession session,
        TaskCompletionSource workerStarted,
        ConcurrentQueue<string> phases){
        await foreach(var response in session.ReadAllAsync().ConfigureAwait(false)){
            if(response.MessageType==WorkerMessageTypes.WorkerStarted){
                phases.Enqueue("WorkerStarted");
                workerStarted.TrySetResult();
            }
        }
        phases.Enqueue("ResponsesEof");
    }

    private static async Task<TaskObservation> ObserveAsync(string name,Task task){
        try{
            await task.WaitAsync(HangTimeout);
            return new(name,task.Status.ToString(),null);
        }catch(Exception exception){
            return new(name,task.Status.ToString(),DescribeException(exception));
        }
    }

    private static ExceptionObservation DescribeException(Exception exception){
        return new(exception.GetType().FullName??exception.GetType().Name,exception.Message,exception.StackTrace);
    }

    private static string ArtifactDirectory([CallerFilePath]string fixturePath=""){
        var configured=Environment.GetEnvironmentVariable("SEAMLESS_WORKER_LIFETIME_ARTIFACT_DIRECTORY");
        if(!string.IsNullOrWhiteSpace(configured)){
            return Path.GetFullPath(configured);
        }
        var repositoryRoot=Directory.GetParent(Path.GetDirectoryName(fixturePath)!)!.FullName;
        return Path.Combine(repositoryRoot,"Work","Orchestration","worker-lifetime-after-01");
    }

    private sealed record TaskObservation(string Name,string Status,ExceptionObservation? Exception);
    private sealed record ExceptionObservation(string Type,string Message,string? StackTrace);
}
