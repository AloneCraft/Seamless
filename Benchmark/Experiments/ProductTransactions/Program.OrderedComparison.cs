using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using ProductTransactions;
using Seamless.Database;
using Seamless.Execution;
using Seamless.Transactions;

internal sealed partial class Program{
    private static readonly string[] OrderedQueryNames=["JoinIdentity","JoinMultiple","GroupIdentity","GroupEscapeMultiple"];
    private static async Task CompareOrdered(string root,string report,string sourceHash,int rows,DatabaseStorageMode storage,int workers,string flavor,int repeats){
        root=Path.GetFullPath(root);report=Path.GetFullPath(report);
        if(Path.GetPathRoot(root)!="D:\\"||Path.GetPathRoot(report)!="F:\\"||rows is not(4096 or 16384)||workers is not(1 or 4)||flavor is not("FLinq" or "Linq")||!Enum.IsDefined(storage)||repeats is<3 or>25){throw new ArgumentException("Invalid ordered comparison profile.");}
        if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()){throw new InvalidOperationException("Use a fresh private data root.");}
        Directory.CreateDirectory(root);Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        var budget=new ResourceBudget(64L<<20,32L<<20);
        var history=new TransactionHistoryOptions{MaxRetainedMemoryBytes=8L<<20,MaxRetainedStorageBytes=8L<<20,MaxMetadataBytes=8L<<20,MaintenanceReserveMemoryBytes=256L<<10,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(10)};
        if(storage==DatabaseStorageMode.Memory){history=history with{MaxRetainedStorageBytes=0};}
        var options=ComparisonOptions(root,storage,TransactionConcurrencyMode.Optimistic,workers) with{
            ParentBudget=budget,IndexSize=1024,SegmentSize=4L<<20,MemorySize=4L<<20,ManagedMemoryLimitBytes=64L<<20,TemporaryStorageLimitBytes=32L<<20,TransactionMemoryLimit=128L<<10,TransactionHistory=history
        };
        var process=Process.GetCurrentProcess();var records=new List<object>();var observations=new List<object>();
        using(var db=new Database(options)){
            for(var start=0;start<rows;start+=512){
                await using var tx=await db.BeginTransactionAsync();
                for(var index=start;index<Math.Min(rows,start+512);index++){var id=(index*4051)&(rows-1);tx.Accounts.Add(new(id,InitialBalance(id)));}
                await tx.CommitAsync();
            }
            await db.CheckpointTransactionsAsync();
            Row[] source;
            await using(var read=await db.BeginReadOnlyTransactionAsync()){source=read.Accounts.Rows.ToArray();await read.CommitAsync();}
            if(source.Length!=rows||source.Select(static row=>row.Id).Distinct().Count()!=rows||source.Any(row=>row.Id<0||row.Id>=rows||row.Balance!=InitialBalance(row.Id))){throw new InvalidDataException("Source differs from the independent model.");}
            var expected=new object[4];for(var query=0;query<4;query++){expected[query]=OrderedComparisonQueries.Linq(source,query);}
            var joinValues=Enumerable.Range(0,rows).Select(id=>id*257L+InitialBalance(id%64)).OrderDescending().ToArray();
            if(!joinValues.SequenceEqual((long[])expected[0])){throw new InvalidDataException("Join cardinality/model mismatch.");}
            // 各プロセスは一つのflavorだけを測る。反復ごとにquery順を巡回し、結果検査と初期投入は時間/割当へ含めない。
            for(var repeat=-2;repeat<repeats;repeat++){
                for(var order=0;order<4;order++){
                    var query=(order+repeat+4)%4;
                    await using var tx=await db.BeginReadOnlyTransactionAsync();_ = tx.Accounts[0];
                    ResetOrderedPeaks(budget);var baselineMemory=budget.MemoryBytes;var baselineStorage=budget.TemporaryStorageBytes;
                    var gc0=GC.CollectionCount(0);var gc1=GC.CollectionCount(1);var gc2=GC.CollectionCount(2);
                    var allocated=GC.GetTotalAllocatedBytes(true);var started=Stopwatch.GetTimestamp();
                    var result=RunOrdered(tx,query,flavor);
                    var milliseconds=Stopwatch.GetElapsedTime(started).TotalMilliseconds;allocated=GC.GetTotalAllocatedBytes(true)-allocated;
                    var peakMemory=budget.PeakMemoryBytes-baselineMemory;var peakStorage=budget.PeakTemporaryStorageBytes-baselineStorage;
                    if(repeat>=0){records.Add(new{Query=OrderedQueryNames[query],Repeat=repeat,Order=order,Milliseconds=milliseconds,AllocatedBytes=allocated,PeakAdditionalReservedMemoryBytes=peakMemory,PeakAdditionalReservedStorageBytes=peakStorage,Gc0=GC.CollectionCount(0)-gc0,Gc1=GC.CollectionCount(1)-gc1,Gc2=GC.CollectionCount(2)-gc2});}
                    ValidateOrdered(result,expected[query]);result=null!;await tx.CommitAsync();CheckOrderedIdle(db);
                }
            }
            // GCを伴う保持量の観測はタイミング標本から分離。結果をKeepAliveして、返却後の寿命も検証する。
            for(var query=0;query<4;query++){
                await using var tx=await db.BeginReadOnlyTransactionAsync();_ = tx.Accounts[0];
                var managedBefore=GC.GetTotalMemory(true);process.Refresh();var privateBefore=process.PrivateMemorySize64;var workingSetBefore=process.WorkingSet64;
                ResetOrderedPeaks(budget);var baselineMemory=budget.MemoryBytes;var baselineStorage=budget.TemporaryStorageBytes;
                var result=RunOrdered(tx,query,flavor);var peakMemory=budget.PeakMemoryBytes-baselineMemory;var peakStorage=budget.PeakTemporaryStorageBytes-baselineStorage;
                var managedAfter=GC.GetTotalMemory(true);process.Refresh();
                observations.Add(new{Query=OrderedQueryNames[query],ManagedRetainedDeltaBytes=managedAfter-managedBefore,PrivateBytesBefore=privateBefore,PrivateBytesAfter=process.PrivateMemorySize64,WorkingSetBytesBefore=workingSetBefore,WorkingSetBytesAfter=process.WorkingSet64,PeakAdditionalReservedMemoryBytes=peakMemory,PeakAdditionalReservedStorageBytes=peakStorage,ResultPayloadBytes=OrderedPayload(result)});
                await tx.CommitAsync();ValidateOrdered(result,expected[query]);GC.KeepAlive(result);result=null!;CheckOrderedIdle(db);
            }
            process.Refresh();
            await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{
                Format="product-ordered-comparison-v1",Utc=DateTimeOffset.UtcNow,Rows=rows,StorageMode=storage.ToString(),QueryWorkers=workers,Flavor=flavor,Repeats=repeats,Warmups=2,SourceManifestSha256=sourceHash,
                AssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))),Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                Affinity=ComparisonAffinity(),Cpu=CpuScheduler.Shared.Usage,OwnerUsage=db.ResourceUsage,OwnerBudgetBytes=64L<<20,LogLimitBytes=4L<<20,IndexBytes=65536,
                ProcessPeakWorkingSetBytes=process.PeakWorkingSet64,ProcessPrivateBytes=process.PrivateMemorySize64,QueryCount=4,Records=records,MemoryObservations=observations,
                Measurement="kernel only; Begin/enlist/Commit/validation excluded; process managed allocation includes workers",ResidentState="warm; corpus fits the configured log",
                BudgetPeakInstrumentation="benchmark-only reflection resets the caller-owned parent peak counters to current values while idle; reservations and limits remain intact",
                LinqBudgetCoverage="BCL lookup/sort/results are not registered in the shared budget; reserved bytes do not measure their actual memory",
                OsMemoryScope="before/after memory pass and cumulative process peak include DB/runtime/warmup; not per-query peak; forced GC is outside timing samples",
                TieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation")
            },new JsonSerializerOptions{WriteIndented=true}));
        }
        if(budget.MemoryBytes!=0||budget.TemporaryStorageBytes!=0){throw new InvalidDataException("Database disposal leaked parent reservations.");}
        Console.WriteLine($"Ordered comparison complete: {storage}, {rows} rows, workers={workers}, {flavor}, {records.Count} timed samples");
    }
    private static object RunOrdered(Database.TypedTransaction tx,int query,string flavor){if(flavor=="FLinq"){return OrderedComparisonQueries.Expanded(tx,query);}return OrderedComparisonQueries.Linq(tx.Accounts.Rows,query);}
    private static void ValidateOrdered(object actual,object expected){
        if(actual is long[] values&&expected is long[] reference){if(!values.SequenceEqual(reference)){throw new InvalidDataException("Ordered scalar results differ.");}return;}
        var groups=(IGrouping<int,Row>[])actual;var expectedGroups=(IGrouping<int,Row>[])expected;
        if(groups.Length!=expectedGroups.Length){throw new InvalidDataException("Group count differs.");}
        for(var index=0;index<groups.Length;index++){if(groups[index].Key!=expectedGroups[index].Key||!groups[index].SequenceEqual(expectedGroups[index])){throw new InvalidDataException("Group order/member lifetime differs.");}}
    }
    private static long OrderedPayload(object result){if(result is long[] values){return values.LongLength*sizeof(long);}return ((IGrouping<int,Row>[])result).Sum(static group=>group.LongCount()*16);}
    private static void CheckOrderedIdle(Database db){var domain=db.GetTransactionDomainDiagnostics();if(domain.ActiveTransactions!=0||domain.ActiveOperations!=0||CpuScheduler.Shared.Usage.Outstanding!=0||CpuScheduler.Shared.Usage.Active!=0){throw new InvalidDataException("Query leaked transaction/worker lifetime.");}}
    private static void ResetOrderedPeaks(ResourceBudget budget){
        // 計測専用。実装の非公開名が変われば失敗させる。資源の現在値/上限には触れず、他の処理がない親だけを使う。
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var gate=typeof(ResourceBudget).GetProperty("Gate",flags)?.GetValue(budget)??throw new MissingMemberException("ResourceBudget.Gate");
        lock(gate){
            (typeof(ResourceBudget).GetField("peakMemory",flags)??throw new MissingFieldException("peakMemory")).SetValue(budget,budget.MemoryBytes);
            (typeof(ResourceBudget).GetField("peakStorage",flags)??throw new MissingFieldException("peakStorage")).SetValue(budget,budget.TemporaryStorageBytes);
        }
    }
}
