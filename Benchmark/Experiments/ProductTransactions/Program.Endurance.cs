using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ProductTransactions;
using Seamless.Database;
using Seamless.Execution;
using Seamless.Transactions;

internal sealed partial class Program{
    private static async Task Endurance(string root,string report,string sourceHash,int cycles,bool churn=false){
        root=Path.GetFullPath(root);report=Path.GetFullPath(report);
        if(Path.GetPathRoot(root)!="F:\\"||Path.GetPathRoot(report)!="F:\\"||cycles is<1 or>512){throw new ArgumentException("Use F data, F evidence and 1..512 cycles.");}
        if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()){throw new InvalidOperationException("Endurance requires a new private data root.");}
        Directory.CreateDirectory(root);Directory.CreateDirectory(Path.GetDirectoryName(report)!);var records=new List<object>();var finalStates=new List<object>();
        foreach(var storage in new[]{DatabaseStorageMode.Memory,DatabaseStorageMode.Storage}){
            var options=new DatabaseContainerOptions{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
                StorageMode=storage,StorageDirectory=Path.Combine(root,storage.ToString(),"database"),TemporaryDirectory=Path.Combine(root,storage.ToString(),"temporary"),MaximumQueryWorkers=4,
                IndexSize=1024,PageSize=4096,SegmentSize=65536,MemorySize=65536,ManagedMemoryLimitBytes=32L<<20,TemporaryStorageLimitBytes=16L<<20,TransactionMemoryLimit=65536,
                TransactionHistory=new(){MaxRetainedMemoryBytes=1L<<20,MaxRetainedStorageBytes=1L<<20,MaxMetadataBytes=2L<<20,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=65536,MaintenanceWaitTimeout=TimeSpan.FromSeconds(10)}};
            if(storage==DatabaseStorageMode.Memory){options=options with{TransactionHistory=options.TransactionHistory! with{MaxRetainedStorageBytes=0,MaintenanceReserveStorageBytes=0}};}
            var model=Enumerable.Repeat(100L,256).ToArray();var present=Enumerable.Repeat(true,model.Length).ToArray();Database? database=new(options);var started=Stopwatch.StartNew();var committed=0;var reopens=0;
            var updates=0;var deletes=0;var inserts=0;long forgottenThrough=0;TransactionCommitResult latest=default;
            try{
                await using(var seed=await database.BeginTransactionAsync()){for(var id=0;id<model.Length;id++){seed.Accounts.Add(new(id,model[id]));}await seed.CommitAsync();}
                await database.CheckpointTransactionsAsync();
                for(var cycle=0;cycle<cycles;cycle++){
                    var cycleNumber=cycle;var db=database;var before=(long[])model.Clone();var beforePresent=(bool[])present.Clone();var phase=churn?cycle%3:0;var outcomes=new TransactionCommitResult[4];
                    await using(var read=await db.BeginReadOnlyTransactionAsync()){
                        VerifyEnduranceRows(read,before,beforePresent);
                        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var prepared=0;Task[] writers;
                        // 別取引として開始する。古い読取scopeのAsyncLocalをworkerへ継承しない。
                        using(ExecutionContext.SuppressFlow()){
                            writers=Enumerable.Range(0,4).Select(worker=>Task.Run(async()=>{
                                for(var operation=0;operation<8;operation++){
                                    timeout.Token.ThrowIfCancellationRequested();await using var tx=await db.BeginTransactionAsync();var key=worker*64+((churn?cycleNumber/3:cycleNumber)*8+operation)%64;
                                    if(phase==1){if(!tx.Accounts.Remove(key)){throw new InvalidDataException("Churn deletion did not find its row.");}}
                                    else if(phase==2){tx.Accounts.Add(new(key,model[key]+1));}
                                    else{var row=tx.Accounts[key];tx.Accounts[key]=row with{Balance=row.Balance+1};}
                                    if(operation==0){if(Interlocked.Increment(ref prepared)==4){ready.TrySetResult();}await release.Task.WaitAsync(timeout.Token);}
                                    outcomes[worker]=await tx.CommitAsync();
                                }
                            })).ToArray();
                        }
                        try{await ready.Task.WaitAsync(timeout.Token);}
                        finally{release.TrySetResult();await Task.WhenAll(writers);}
                        // 確定済みの現在値へ読み換わらず、同じReadVersionの全行とFLinq結果を返す。
                        VerifyEnduranceRows(read,before,beforePresent);await read.CommitAsync();
                    }
                    for(var worker=0;worker<4;worker++){for(var operation=0;operation<8;operation++){var key=worker*64+((churn?cycle/3:cycle)*8+operation)%64;if(phase==1){present[key]=false;}else{present[key]=true;model[key]++;}}}
                    if(phase==1){deletes+=32;}else if(phase==2){inserts+=32;}else{updates+=32;}
                    latest=outcomes.MaxBy(static outcome=>outcome.Lsn);committed+=32;await using(var verify=await db.BeginReadOnlyTransactionAsync()){VerifyEnduranceRows(verify,model,present);await verify.CommitAsync();}
                    var reclaimStarted=Stopwatch.GetTimestamp();await db.ReclaimTransactionHistoryAsync();var reclaimMilliseconds=Stopwatch.GetElapsedTime(reclaimStarted).TotalMilliseconds;var maintenance=db.GetTransactionDomainDiagnostics().Maintenance;EnduranceForget? forgetting=null;
                    if(maintenance.Phase!=TransactionMaintenancePhase.Completed||!maintenance.PhysicalStarted||maintenance.DrainWait<TimeSpan.Zero||maintenance.PhysicalWork<TimeSpan.Zero||maintenance.Total!=maintenance.DrainWait+maintenance.PhysicalWork){throw new InvalidDataException("Reclamation phase diagnostics disagree with completion.");}
                    if(churn&&storage==DatabaseStorageMode.Storage&&(cycle+1)%8==0){forgetting=await ForgetEnduranceOutcomes(db,latest);forgottenThrough=latest.Lsn;}
                    if(storage==DatabaseStorageMode.Storage&&(cycle+1)%8==0){
                        db.Dispose();database=null;database=new(options);reopens++;await using var verify=await database.BeginReadOnlyTransactionAsync();VerifyEnduranceRows(verify,model,present);await verify.CommitAsync();
                        if(churn&&(await database.GetTransactionOutcomeAsync(latest.Id)).Kind!=TransactionOutcomeKind.Unknown){throw new InvalidDataException("A forgotten result reappeared after restart.");}
                    }
                    var usage=database.ResourceUsage;if(usage.MemoryBytes>usage.MemoryLimitBytes||usage.TemporaryStorageBytes>usage.TemporaryStorageLimitBytes){throw new InvalidDataException("Reported resources exceed the configured budget.");}
                    records.Add(new{StorageMode=storage.ToString(),Cycle=cycle+1,Committed=committed,Updates=updates,Deletes=deletes,Inserts=inserts,Rows=present.Count(static value=>value),ForgottenThrough=forgottenThrough,Forgetting=forgetting,ReclaimMilliseconds=reclaimMilliseconds,ReclaimSequence=maintenance.Sequence,ReclaimDrainMilliseconds=maintenance.DrainWait.TotalMilliseconds,ReclaimPhysicalMilliseconds=maintenance.PhysicalWork.TotalMilliseconds,ReclaimInternalMilliseconds=maintenance.Total.TotalMilliseconds,Reopens=reopens,Milliseconds=started.Elapsed.TotalMilliseconds,Usage=usage,Cpu=CpuScheduler.Shared.Usage});
                }
                await database.CheckpointTransactionsAsync();
                EnduranceForget? finalForgetting=null;
                if(storage==DatabaseStorageMode.Storage){
                    if(churn&&latest.Lsn>forgottenThrough){finalForgetting=await ForgetEnduranceOutcomes(database,latest);forgottenThrough=latest.Lsn;}
                    database.Dispose();database=null;database=new(options);await using var verify=await database.BeginReadOnlyTransactionAsync();VerifyEnduranceRows(verify,model,present);await verify.CommitAsync();
                    if(churn&&(await database.GetTransactionOutcomeAsync(latest.Id)).Kind!=TransactionOutcomeKind.Unknown){throw new InvalidDataException("Final restart restored a forgotten result.");}
                }
                finalStates.Add(new{StorageMode=storage.ToString(),Committed=committed,Updates=updates,Deletes=deletes,Inserts=inserts,Rows=present.Count(static value=>value),ForgottenThrough=forgottenThrough,Forgetting=finalForgetting,LatestTransactionId=latest.Id,LatestOutcome=storage==DatabaseStorageMode.Storage?(await database.GetTransactionOutcomeAsync(latest.Id)).Kind.ToString():null,Usage=database.ResourceUsage});
            }finally{database?.Dispose();}
            Console.WriteLine($"Endurance {storage}: cycles={cycles}, changes={committed}, updates={updates}, deletes={deletes}, inserts={inserts}, periodic reopens={reopens}");
        }
        var result=new{Format="product-mvcc-endurance-v2",Utc=DateTimeOffset.UtcNow,Workload=churn?"Churn":"Update",Rows=256,Cycles=cycles,UpdateWorkers=4,UpdatesPerWorkerPerCycle=8,SourceManifestSha256=sourceHash,AssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Database).Assembly.Location))),Records=records,FinalStates=finalStates};
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
    }
    private readonly record struct EnduranceForget(long Lsn,long MemoryBefore,long MemoryAfter);
    private static async Task<EnduranceForget> ForgetEnduranceOutcomes(Database database,TransactionCommitResult latest){
        var outcome=await database.GetTransactionOutcomeAsync(latest.Id);if(outcome.Kind!=TransactionOutcomeKind.Committed||outcome.Lsn!=latest.Lsn){throw new InvalidDataException("The committed result was unavailable before forgetting.");}
        var before=database.ResourceUsage.MemoryBytes;
        await database.ForgetTransactionOutcomesThroughAsync(latest.Lsn);
        if((await database.GetTransactionOutcomeAsync(latest.Id)).Kind!=TransactionOutcomeKind.Unknown){throw new InvalidDataException("Explicit result forgetting did not remove the result.");}
        var after=database.ResourceUsage.MemoryBytes;if(after>=before){throw new InvalidDataException("Forgetting results did not release their memory reservation.");}return new(latest.Lsn,before,after);
    }
    private static void VerifyEnduranceRows(Database.TypedTransaction tx,long[] model,bool[] present){
        var seen=new bool[model.Length];var count=0;long total=0;long evenTotal=0;
        foreach(var row in tx.Accounts){
            if(row.Id<0||row.Id>=model.Length||!present[row.Id]||seen[row.Id]||row.Balance!=model[row.Id]){throw new InvalidDataException("Endurance rows disagree with the independent model.");}
            seen[row.Id]=true;count++;total+=row.Balance;if(row.Id%2==0){evenTotal+=row.Balance;}
        }
        var expectedRows=present.Count(static value=>value);if(count!=expectedRows||Queries.Count(tx)!=expectedRows||Queries.Total(tx)!=evenTotal){throw new InvalidDataException("Endurance snapshot count or sum mismatch.");}
        for(var id=0;id<model.Length;id++){if(tx.Accounts.TryGetValue(id,out var row)!=present[id]||(present[id]&&(row.Id!=id||row.Balance!=model[id]))){throw new InvalidDataException("Endurance point read or deletion mismatch.");}}
        var expectedGroups=Enumerable.Range(0,model.Length).Where(id=>present[id]).GroupBy(static id=>id%64).ToDictionary(static group=>group.Key,group=>(Count:group.LongCount(),Total:group.Sum(id=>model[id])));
        var groups=Queries.Groups(tx);if(groups.Length!=expectedGroups.Count||groups.Select(static group=>group.Key).Distinct().Count()!=groups.Length||groups.Sum(static group=>group.Total)!=total){throw new InvalidDataException("Endurance grouping mismatch.");}
        foreach(var group in groups){if(!expectedGroups.TryGetValue(group.Key,out var expected)||group.Count!=expected.Count||group.Total!=expected.Total){throw new InvalidDataException("Endurance group membership mismatch.");}}
        var joined=Queries.Join(tx);var expectedJoined=Enumerable.Range(0,model.Length).Count(id=>present[id]&&present[id%64]);if(joined.Length!=expectedJoined||joined.Any(pair=>pair.Left!=pair.Right%64||!present[pair.Right]||!present[pair.Left])||joined.Select(static pair=>pair.Right).Distinct().Count()!=expectedJoined){throw new InvalidDataException("Endurance join mismatch.");}
    }
}
