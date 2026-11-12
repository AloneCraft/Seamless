using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ProductTransactions;
using Seamless.Collections.Tsavorite;
using Seamless.Database;
using Seamless.Execution;
using Seamless.Transactions;
using NativeRows=Seamless.Collections.Tsavorite.Set<int,ProductTransactions.Row>;
using Store=Seamless.Execution.Store;

internal sealed partial class Program{
    private const int ComparisonRepeats=5;
    private static long InitialBalance(int id)=>100+id%17;
    private static DatabaseContainerOptions ComparisonOptions(string root,DatabaseStorageMode storage,TransactionConcurrencyMode mode,int workers)=>new(){
        TransactionConcurrency=mode,StorageMode=storage,MaximumQueryWorkers=workers,
        SharedStoreOptions=new(){Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>()},
        // DatabaseのIndexSizeはbucket数、TsavoriteTuningはbyte数。同じ2MiBに揃える。
        StorageDirectory=Path.Combine(root,"database"),TemporaryDirectory=Path.Combine(root,"temporary"),IndexSize=(2L<<20)/64,PageSize=65536,SegmentSize=16L<<20,MemorySize=32L<<20,
        ManagedMemoryLimitBytes=128L<<20,TemporaryStorageLimitBytes=256L<<20,TransactionMemoryLimit=1L<<20,
        TransactionHistory=new(){MaxRetainedMemoryBytes=24L<<20,MaxRetainedStorageBytes=24L<<20,MaxMetadataBytes=48L<<20,MaintenanceReserveMemoryBytes=1L<<20,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(15)}
    };
    private static async Task<Database> ComparisonDatabase(string root,int rows,DatabaseStorageMode storage,TransactionConcurrencyMode mode,int workers){
        var options=ComparisonOptions(root,storage,mode,workers);
        if(storage==DatabaseStorageMode.Memory){options=options with{TransactionHistory=options.TransactionHistory! with{MaxRetainedStorageBytes=0}};}
        var db=new Database(options);
        try{
            for(var start=0;start<rows;start+=1024){
                await using var tx=await db.BeginTransactionAsync();
                for(var id=start;id<Math.Min(rows,start+1024);id++){tx.Accounts.Add(new(id,InitialBalance(id)));}
                await tx.CommitAsync();
            }
            await db.CheckpointTransactionsAsync();return db;
        }catch{db.Dispose();throw;}
    }
    private static async Task CompareSources(string root,string report,string sourceHash,int rows,DatabaseStorageMode storage,int operations,string stage){
        root=Path.GetFullPath(root);report=Path.GetFullPath(report);
        if(Path.GetPathRoot(root)!="D:\\"||Path.GetPathRoot(report)!="F:\\"||rows is<64 or>100000||operations is<8 or>4096||!Enum.IsDefined(storage)||stage is not("queries" or "mixed" or "all")){throw new ArgumentException("Use D data, F report, 64..100000 rows, 8..4096 operations and queries/mixed/all.");}
        if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()){throw new InvalidOperationException("Comparison requires a fresh private root.");}
        Directory.CreateDirectory(root);Directory.CreateDirectory(Path.GetDirectoryName(report)!);var records=new List<object>();
        async Task Save(){
            var result=new{Format="product-mvcc-comparison-v1",Utc=DateTimeOffset.UtcNow,Rows=rows,StorageMode=storage.ToString(),Stage=stage,Repeats=ComparisonRepeats,OperationsPerWorker=operations,SourceManifestSha256=sourceHash,
                Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,ProcessorCount=Environment.ProcessorCount,Affinity=ComparisonAffinity(),CpuCapacity=CpuScheduler.Shared.Usage.Capacity,IndexBytes=2L<<20,LogLimitBytes=32L<<20,HistoryLimitBytes=24L<<20,OwnerBudgetBytes=128L<<20,
                AssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))),TsavoriteVersion=typeof(global::Tsavorite.core.KVSettings).Assembly.GetName().Version!.ToString(),TsavoriteSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(global::Tsavorite.core.KVSettings).Assembly.Location))),
                ResidentState="warm; corpus fits the configured log",NativeEncoding="Auto<int>/Auto<Row>",TransactionEncoding="Auto<int>/Auto<Row>; version header is additional",AllocationScope="process managed allocations; not native/RSS",QueryWorkerLimitIsCap=true,MixedPeakInterval="read/stage body after Begin and before Commit",HarnessCosts="RNG, latency samples, commit/model counters and body-overlap tracking are included in mixed timings",Records=records};
            await File.WriteAllTextAsync(report,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        }
        if(stage is "queries" or "all"){await CompareQueries(root,rows,storage,records,Save);}
        if(stage is "mixed" or "all"){await CompareMixed(root,rows,storage,operations,records,Save);}
        await Save();Console.WriteLine($"Comparison complete: {storage}, rows={rows}, stage={stage}, samples={records.Count}");
    }
    private static async Task CompareQueries(string root,int rows,DatabaseStorageMode storage,List<object> records,Func<Task> save){
        var limits=new StoreLimits{MemoryBytes=128L<<20,DiskBytes=256L<<20};
        var storeOptions=new StoreOptions{Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>()};
        using var nativeOwner=storage==DatabaseStorageMode.Memory?Store.CreateMemory(limits,options:storeOptions):Store.Open(Path.Combine(root,"native"),limits,options:storeOptions);
        var data=nativeOwner.CreateTsavoriteSet<int,Row>("rows",new(){IndexSize=2L<<20,PageSize=65536,SegmentSize=16L<<20,LogMemorySize=32L<<20},rows);
        using var native=data.Create();
        for(var id=0;id<rows;id++){native.Add(new(id,InitialBalance(id)));}
        if(storage==DatabaseStorageMode.Storage){nativeOwner.Save();}
        var expected=new long[4];expected[1]=rows;var counts=new long[64];var totals=new long[64];
        for(var id=0;id<rows;id++){var balance=InitialBalance(id);if(id%2==0){expected[0]+=balance;}counts[id%64]++;totals[id%64]+=balance;expected[3]+=InitialBalance(id%64)+balance;}
        for(var key=0;key<64;key++){expected[2]+=(key+1)*totals[key]+17*counts[key];}
        var depths=new[]{1};if(rows<=10000){depths=[1,8];}
        foreach(var profile in depths.SelectMany(depth=>new[]{1,2,4,8}.Select(workers=>(Depth:depth,Workers:workers)))){
            var depth=profile.Depth;var workers=profile.Workers;
            using var db=await ComparisonDatabase(Path.Combine(root,"query-"+depth+"-"+workers),rows,storage,TransactionConcurrencyMode.Optimistic,workers);
            Database.TypedTransaction? retained=null;
            try{
            if(depth>1){
                retained=await db.BeginReadOnlyTransactionAsync();
                Task update;
                using(ExecutionContext.SuppressFlow()){
                    update=Task.Run(async()=>{
                        for(var version=1;version<depth;version++){
                            for(var start=0;start<rows;start+=1024){
                                await using var tx=await db.BeginTransactionAsync();
                                for(var id=start;id<Math.Min(rows,start+1024);id++){tx.Accounts[id]=new(id,InitialBalance(id)+version);}
                                await tx.CommitAsync();
                            }
                        }
                    });
                }
                await update;
            }
            var flavors=6;if(retained is not null){flavors=4;}
            GC.Collect();GC.WaitForPendingFinalizers();
            for(var repeat=-1;repeat<ComparisonRepeats;repeat++){
                for(var query=0;query<4;query++){
                    for(var order=0;order<flavors;order++){
                        var flavor=(order+repeat+flavors)%flavors;long value;double milliseconds;long allocated;
                        if(flavor<2){
                            var sample=Measure(()=>{if(flavor==0){return ComparisonQueries.Native(native,query);}return ComparisonQueries.Linq(native,query);});
                            value=sample.Value;milliseconds=sample.Milliseconds;allocated=sample.Allocated;
                        }else if(flavor<4){
                            if(retained is{} snapshot){
                                var sample=Measure(()=>{if(flavor==2){return ComparisonQueries.Transaction(snapshot,query);}return ComparisonQueries.Linq(snapshot.Accounts.Rows,query);});
                                value=sample.Value;milliseconds=sample.Milliseconds;allocated=sample.Allocated;
                            }else{
                                await using var tx=await db.BeginReadOnlyTransactionAsync();
                                var sample=Measure(()=>{if(flavor==2){return ComparisonQueries.Transaction(tx,query);}return ComparisonQueries.Linq(tx.Accounts.Rows,query);});
                                value=sample.Value;milliseconds=sample.Milliseconds;allocated=sample.Allocated;await tx.CommitAsync();
                            }
                        }else{
                            var before=GC.GetTotalAllocatedBytes(true);var started=Stopwatch.GetTimestamp();
                            await using(var tx=await db.BeginReadOnlyTransactionAsync()){
                                if(flavor==4){value=ComparisonQueries.Transaction(tx,query);}else{value=ComparisonQueries.Linq(tx.Accounts.Rows,query);}await tx.CommitAsync();
                            }
                            milliseconds=Stopwatch.GetElapsedTime(started).TotalMilliseconds;allocated=GC.GetTotalAllocatedBytes(true)-before;
                        }
                        if(value!=expected[query]){throw new InvalidDataException($"Comparison mismatch: query={query}, flavor={flavor}, value={value}, expected={expected[query]}");}
                        if(repeat>=0){records.Add(new{Kind="query",Query=new[]{"Sum","CountWhere","GroupBy","Join"}[query],Flavor=new[]{"NativeFLinq","NativeLinq","TransactionFLinq","TransactionLinq","TransactionFLinqEndToEnd","TransactionLinqEndToEnd"}[flavor],TransactionVersionDepth=depth,QueryWorkerLimit=workers,Repeat=repeat,Order=order,Milliseconds=milliseconds,AllocatedBytes=allocated,Value=value,Expected=expected[query]});}
                    }
                }
            }
            if(CpuScheduler.Shared.Usage.Active!=0||CpuScheduler.Shared.Usage.Outstanding!=0){throw new InvalidDataException("Query leaked a CPU lease.");}
            records.Add(new{Kind="query-resources",TransactionVersionDepth=depth,QueryWorkerLimit=workers,Usage=db.ResourceUsage,Domain=db.GetTransactionDomainDiagnostics(),NativeUsage=new{nativeOwner.Budget.MemoryBytes,nativeOwner.Budget.PeakMemoryBytes,nativeOwner.Budget.TemporaryStorageBytes,nativeOwner.Budget.PeakTemporaryStorageBytes},Native=data.GetStatistics(),Cpu=CpuScheduler.Shared.Usage,ProcessPeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64});
            await save();Console.WriteLine($"Queries: {storage}, rows={rows}, versions={depth}, query workers={workers} complete");
            }finally{if(retained is not null){await retained.DisposeAsync();}}
            if(depth>1){await using var latest=await db.BeginReadOnlyTransactionAsync();if(latest.Accounts[0].Balance!=InitialBalance(0)+depth-1){throw new InvalidDataException("The retained snapshot profile did not actually install the newer versions.");}await latest.CommitAsync();}
        }
    }
    private static long ComparisonAffinity(){
        if(OperatingSystem.IsWindows()||OperatingSystem.IsLinux()){return Process.GetCurrentProcess().ProcessorAffinity.ToInt64();}
        throw new PlatformNotSupportedException("CPU affinity evidence is required.");
    }
    private static (long Value,double Milliseconds,long Allocated) Measure(Func<long> action){
        var before=GC.GetTotalAllocatedBytes(true);var started=Stopwatch.GetTimestamp();var value=action();var elapsed=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        return(value,elapsed,GC.GetTotalAllocatedBytes(true)-before);
    }
    private static async Task CompareMixed(string root,int rows,DatabaseStorageMode storage,int operations,List<object> records,Func<Task> save){
        var modes=new[]{TransactionConcurrencyMode.Exclusive,TransactionConcurrencyMode.Optimistic};var databases=new Database?[2];var models=new[]{new int[rows],new int[rows]};
        try{
            for(var index=0;index<2;index++){databases[index]=await ComparisonDatabase(Path.Combine(root,"mixed-"+modes[index]),rows,storage,modes[index],1);}
            for(var repeat=-1;repeat<ComparisonRepeats;repeat++){
                foreach(var concurrency in new[]{1,2,4,8}){
                    foreach(var readPercent in new[]{100,95,50}){
                        foreach(var hot in new[]{false,true}){
                            for(var order=0;order<2;order++){
                                var index=(order+repeat+2)%2;var db=databases[index]!;var model=models[index];
                                var result=await MixedSample(db,model,rows,operations,concurrency,readPercent,hot,repeat);
                                if(repeat>=0){records.Add(new{Kind="mixed",Mode=modes[index].ToString(),Concurrency=concurrency,QueryWorkerLimit=1,ReadPercent=readPercent,Hot=hot,Repeat=repeat,Order=order,result.Attempts,result.Committed,result.Updates,result.Conflicts,result.Milliseconds,result.Throughput,result.P50,result.P95,result.P99,result.SuccessP95,result.AllocatedBytes,result.PeakConcurrentBodies});}
                                await VerifyMixed(db,model);await db.ReclaimTransactionHistoryAsync();
                            }
                        }
                    }
                }
                await save();Console.WriteLine($"Mixed: {storage}, rows={rows}, repeat={repeat} complete");
            }
        }finally{foreach(var db in databases){db?.Dispose();}}
    }
    private readonly record struct MixedMeasurement(int Attempts,int Committed,int Updates,int Conflicts,double Milliseconds,double Throughput,double P50,double P95,double P99,double SuccessP95,long AllocatedBytes,int PeakConcurrentBodies);
    private static async Task<MixedMeasurement> MixedSample(Database db,int[] model,int rows,int operations,int concurrency,int readPercent,bool hot,int repeat){
        var latency=new double[concurrency*operations];var success=new bool[latency.Length];var committed=0;var updates=0;var conflicts=0;var active=0;var peak=0;
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var before=GC.GetTotalAllocatedBytes(true);var started=Stopwatch.GetTimestamp();
        var tasks=Enumerable.Range(0,concurrency).Select(worker=>Task.Run(async()=>{
            var hotSeed=0;if(hot){hotSeed=19;}var random=new Random(unchecked(1703+worker*53+(repeat+1)*1009+readPercent*7+hotSeed));await ready.Task;
            for(var operation=0;operation<operations;operation++){
                var offset=worker*operations+operation;var start=Stopwatch.GetTimestamp();var range=rows;if(hot){range=Math.Max(1,rows/100);}var key=random.Next(range);var write=random.Next(100)>=readPercent;
                try{
                    await using(var tx=await db.BeginTransactionAsync()){
                        var current=Interlocked.Increment(ref active);int observed;
                        do{observed=Volatile.Read(ref peak);if(current<=observed){break;}}while(Interlocked.CompareExchange(ref peak,current,observed)!=observed);
                        try{var row=tx.Accounts[key];if(write){tx.Accounts[key]=row with{Balance=checked(row.Balance+1)};}}finally{Interlocked.Decrement(ref active);}
                        await tx.CommitAsync();
                    }
                    Interlocked.Increment(ref committed);success[offset]=true;if(write){Interlocked.Increment(ref updates);Interlocked.Increment(ref model[key]);}
                }catch(TransactionAbortedException error)when(error.Reason==TransactionAbortReason.Conflict){Interlocked.Increment(ref conflicts);}
                latency[offset]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
        })).ToArray();ready.TrySetResult();await Task.WhenAll(tasks);var elapsed=Stopwatch.GetElapsedTime(started).TotalMilliseconds;var allocated=GC.GetTotalAllocatedBytes(true)-before;
        var successful=latency.Where((_,index)=>success[index]).Order().ToArray();Array.Sort(latency);
        if(committed+conflicts!=latency.Length||active!=0||CpuScheduler.Shared.Usage.Active!=0||CpuScheduler.Shared.Usage.Outstanding!=0){throw new InvalidDataException("An attempted transaction or CPU lease has no terminal outcome.");}
        return new(latency.Length,committed,updates,conflicts,elapsed,committed/(elapsed/1000),Percentile(latency,.5),Percentile(latency,.95),Percentile(latency,.99),Percentile(successful,.95),allocated,peak);
    }
    private static double Percentile(double[] sorted,double quantile){if(sorted.Length==0){return 0;}return sorted[Math.Min(sorted.Length-1,(int)Math.Ceiling(sorted.Length*quantile)-1)];}
    private static async Task VerifyMixed(Database db,int[] model){
        var seen=new bool[model.Length];var count=0;await using var tx=await db.BeginReadOnlyTransactionAsync();
        foreach(var row in tx.Accounts.Rows){if(row.Id<0||row.Id>=model.Length||seen[row.Id]||row.Balance!=InitialBalance(row.Id)+model[row.Id]){throw new InvalidDataException("Successful commits disagree with the independent per-key model.");}seen[row.Id]=true;count++;}
        if(count!=model.Length){throw new InvalidDataException("Mixed workload lost a row.");}await tx.CommitAsync();
    }
}
