using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using ProductTransactions;
using Seamless.Collections.Tsavorite;
using Seamless.Database;
using Seamless.Execution;
using Seamless.Transactions;
using Store=Seamless.Execution.Store;

internal sealed partial class Program{
    private static readonly string[] OrderedQueryNames=["JoinIdentity","JoinMultiple","GroupIdentity","GroupEscapeMultiple"];
    private static readonly string[] CarrierQueryNames=[..OrderedQueryNames,"GroupJoinCounts","GroupJoinOwned","CarrierJoin","CarrierChain","Groups16","Groups8192","GroupsSkewed","CarrierWide"];
    private static async Task CompareOrdered(string root,string report,string sourceHash,int rows,DatabaseStorageMode storage,int workers,string flavor,int repeats,string profile="Standard"){
        root=Path.GetFullPath(root);report=Path.GetFullPath(report);
        if(Path.GetPathRoot(root)!="F:\\"||Path.GetPathRoot(report)!="F:\\"||rows is not(4096 or 16384)||workers is not(1 or 4)||flavor is not("FLinq" or "Linq")||!Enum.IsDefined(storage)||repeats is<3 or>25){throw new ArgumentException("Invalid ordered comparison profile.");}
        if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()){throw new InvalidOperationException("Use a fresh private data root.");}
        Directory.CreateDirectory(root);Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        if(profile is not ("Standard" or "Carrier" or "Baseline" or "Spill" or "Cpu" or "CarrierJoinOnly" or "JoinMultipleOnly" or "GroupJoinSpill")){
            throw new ArgumentException("Unknown comparison profile.",nameof(profile));
        }
        var groupJoinSpill=profile=="GroupJoinSpill";
        var memoryLimit=64L<<20;
        if(profile=="Spill"||groupJoinSpill){memoryLimit=18L<<20;}
        var queryNames=OrderedQueryNames;
        int[] queries=[0,1,2,3];
        if(profile!="Standard"){
            queryNames=CarrierQueryNames;
            queries=Enumerable.Range(0,queryNames.Length).ToArray();
        }
        if(profile=="Spill"){queries=[2,3,8,9,10];}
        if(profile=="Cpu"){queries=[0,1,4,5,6];}
        if(groupJoinSpill){
            queryNames=[..CarrierQueryNames,"GroupJoinSpillCounts","GroupJoinSpillOwned"];
            queries=[12,13];
        }
        var isolated=profile is "CarrierJoinOnly" or "JoinMultipleOnly";
        if(isolated){queries=profile=="CarrierJoinOnly"?[6]:[1];}
        var unavailable=new Dictionary<string,string>(StringComparer.Ordinal);
        var budget=new ResourceBudget(memoryLimit,32L<<20);
        var history=new TransactionHistoryOptions{MaxRetainedMemoryBytes=8L<<20,MaxRetainedStorageBytes=8L<<20,MaxMetadataBytes=8L<<20,MaintenanceReserveMemoryBytes=256L<<10,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(10)};
        if(storage==DatabaseStorageMode.Memory){history=history with{MaxRetainedStorageBytes=0};}
        var options=ComparisonOptions(root,storage,TransactionConcurrencyMode.Optimistic,workers) with{
            ParentBudget=budget,IndexSize=1024,SegmentSize=4L<<20,MemorySize=4L<<20,ManagedMemoryLimitBytes=memoryLimit,TemporaryStorageLimitBytes=32L<<20,TransactionMemoryLimit=128L<<10,TransactionHistory=history
        };
        if(groupJoinSpill){
            // 整列する外側のsnapshotカーソルと退避Lookupが同じ親予算を使う。
            options=options with{PageSize=16L<<10};
        }
        var process=Process.GetCurrentProcess();var records=new List<object>();var observations=new List<object>();
        using(var db=new Database(options)){
            for(var start=0;start<rows;start+=512){
                await using var tx=await db.BeginTransactionAsync();
                for(var index=start;index<Math.Min(rows,start+512);index++){var id=(index*4051)&(rows-1);tx.Accounts.Add(new(id,InitialBalance(id)));}
                await tx.CommitAsync();
            }
            await db.CheckpointTransactionsAsync();
            Row[] source;
            await using(var read=await db.BeginReadOnlyTransactionAsync()){source=read.Accounts.ToArray();await read.CommitAsync();}
            if(source.Length!=rows||source.Select(static row=>row.Id).Distinct().Count()!=rows||source.Any(row=>row.Id<0||row.Id>=rows||row.Balance!=InitialBalance(row.Id))){throw new InvalidDataException("Source differs from the independent model.");}
            var expected=new object[queryNames.Length];
            foreach(var query in groupJoinSpill?queries.Prepend(0):isolated?queries:Enumerable.Range(0,expected.Length)){expected[query]=OrderedComparisonQueries.LinqCarrier(source,query,rows);}
            var joinValues=Enumerable.Range(0,rows).Select(id=>id*257L+InitialBalance(id%64));
            var modelValid=profile switch{
                "CarrierJoinOnly"=>joinValues.SequenceEqual((long[])expected[6]),
                "JoinMultipleOnly"=>joinValues.Select(static value=>value+1).OrderByDescending(static value=>value%13).ThenBy(static value=>value).SequenceEqual((long[])expected[1]),
                _=>joinValues.OrderDescending().SequenceEqual((long[])expected[0])
            };
            if(!modelValid){throw new InvalidDataException("Join cardinality/model mismatch.");}
            // 各プロセスは一つのflavorだけを測る。反復ごとにquery順を巡回し、結果検査と初期投入は時間/割当へ含めない。
            for(var repeat=-2;repeat<repeats;repeat++){
                for(var order=0;order<queries.Length;order++){
                    var query=queries[(order+repeat+queries.Length)%queries.Length];
                    var queryName=queryNames[query];
                    if(unavailable.ContainsKey(queryName)){continue;}
                    await using var tx=await db.BeginReadOnlyTransactionAsync();_ = tx.Accounts[0];
                    ResetOrderedPeaks(budget);var baselineMemory=budget.MemoryBytes;var baselineStorage=budget.TemporaryStorageBytes;
                    var gc0=GC.CollectionCount(0);var gc1=GC.CollectionCount(1);var gc2=GC.CollectionCount(2);
                    var allocated=GC.GetTotalAllocatedBytes(true);var started=Stopwatch.GetTimestamp();
                    object result;
                    try{
                        result=RunOrdered(tx,query,flavor,rows);
                    }catch(Exception error)when(profile=="Baseline"&&query>=4&&error is InvalidOperationException or NotSupportedException
                        ||profile=="Spill"&&error is ResourceLimitException){
                        unavailable.Add(queryName,error.GetType().Name+": "+error.Message);
                        continue;
                    }
                    var milliseconds=Stopwatch.GetElapsedTime(started).TotalMilliseconds;allocated=GC.GetTotalAllocatedBytes(true)-allocated;
                    var peakMemory=budget.PeakMemoryBytes-baselineMemory;var peakStorage=budget.PeakTemporaryStorageBytes-baselineStorage;
                    if(groupJoinSpill&&flavor=="FLinq"&&peakStorage<=0){throw new InvalidDataException("GroupJoin did not exercise temporary storage.");}
                    try{
                        ValidateOrdered(result,expected[query]);
                    }catch(InvalidDataException error)when(profile=="Baseline"&&query>=4){
                        unavailable.Add(queryName,error.Message);
                        continue;
                    }
                    if(repeat>=0){records.Add(new{Query=queryName,Repeat=repeat,Order=order,Milliseconds=milliseconds,AllocatedBytes=allocated,PeakAdditionalReservedMemoryBytes=peakMemory,PeakAdditionalReservedStorageBytes=peakStorage,Gc0=GC.CollectionCount(0)-gc0,Gc1=GC.CollectionCount(1)-gc1,Gc2=GC.CollectionCount(2)-gc2});}
                    result=null!;await tx.CommitAsync();CheckOrderedIdle(db);
                }
            }
            // GCを伴う保持量の観測はタイミング標本から分離。結果をKeepAliveして、返却後の寿命も検証する。
            foreach(var query in queries){
                if(unavailable.ContainsKey(queryNames[query])){continue;}
                await using var tx=await db.BeginReadOnlyTransactionAsync();_ = tx.Accounts[0];
                var managedBefore=GC.GetTotalMemory(true);process.Refresh();var privateBefore=process.PrivateMemorySize64;var workingSetBefore=process.WorkingSet64;
                ResetOrderedPeaks(budget);var baselineMemory=budget.MemoryBytes;var baselineStorage=budget.TemporaryStorageBytes;
                var result=RunOrdered(tx,query,flavor,rows);var peakMemory=budget.PeakMemoryBytes-baselineMemory;var peakStorage=budget.PeakTemporaryStorageBytes-baselineStorage;
                var managedAfter=GC.GetTotalMemory(true);process.Refresh();
                observations.Add(new{Query=queryNames[query],ManagedRetainedDeltaBytes=managedAfter-managedBefore,PrivateBytesBefore=privateBefore,PrivateBytesAfter=process.PrivateMemorySize64,WorkingSetBytesBefore=workingSetBefore,WorkingSetBytesAfter=process.WorkingSet64,PeakAdditionalReservedMemoryBytes=peakMemory,PeakAdditionalReservedStorageBytes=peakStorage,ResultPayloadBytes=OrderedPayload(result)});
                await tx.CommitAsync();ValidateOrdered(result,expected[query]);
                if(groupJoinSpill&&flavor=="FLinq"&&result is OrderedComparisonQueries.Joined[] joined
                    &&!ReferenceEquals(joined[0].Matches,joined[128].Matches)){
                    throw new InvalidDataException("Repeated outer keys did not share their owned group after commit.");
                }
                GC.KeepAlive(result);result=null!;CheckOrderedIdle(db);
            }
            process.Refresh();
            await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{
                Format="product-ordered-comparison-v1",Utc=DateTimeOffset.UtcNow,Rows=rows,StorageMode=storage.ToString(),QueryWorkers=workers,Flavor=flavor,Repeats=repeats,Warmups=2,SourceManifestSha256=sourceHash,
                AssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))),Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                Affinity=ComparisonAffinity(),Cpu=CpuScheduler.Shared.Usage,OwnerUsage=db.ResourceUsage,OwnerBudgetBytes=memoryLimit,LogLimitBytes=4L<<20,IndexBytes=65536,
                ProcessPeakWorkingSetBytes=process.PeakWorkingSet64,ProcessPrivateBytes=process.PrivateMemorySize64,QueryCount=queries.Length,Profile=profile,Unavailable=unavailable,Records=records,MemoryObservations=observations,
                Measurement="kernel only; Begin/enlist/Commit/validation excluded; process managed allocation includes workers",ResidentState="warm; corpus fits the configured log",
                BudgetPeakInstrumentation="benchmark-only reflection resets the caller-owned parent peak counters to current values while idle; reservations and limits remain intact",
                LinqBudgetCoverage="BCL lookup/sort/results are not registered in the shared budget; reserved bytes do not measure their actual memory",
                OsMemoryScope="before/after memory pass and cumulative process peak include DB/runtime/warmup; not per-query peak; forced GC is outside timing samples",
                TieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
                GeneratedCarrierTypes=typeof(Program).Assembly.GetTypes().Count(static type=>type.Name.StartsWith("__Anon",StringComparison.Ordinal)),
                AssemblyBytes=new FileInfo(typeof(Program).Assembly.Location).Length
            },new JsonSerializerOptions{WriteIndented=true}));
        }
        if(budget.MemoryBytes!=0||budget.TemporaryStorageBytes!=0){throw new InvalidDataException("Database disposal leaked parent reservations.");}
        Console.WriteLine($"Ordered comparison complete: {storage}, {rows} rows, workers={workers}, {flavor}, {records.Count} timed samples");
    }

    private static async Task CompareCollectionCarrier(string root,string report,string sourceHash,int rows,string provider,string flavor){
        root=Path.GetFullPath(root);
        report=Path.GetFullPath(report);
        if(Path.GetPathRoot(root)!="F:\\"||Path.GetPathRoot(report)!="F:\\"||rows is not (4096 or 16384)
            ||provider is not ("Array" or "List" or "Tsavorite" or "Persisted")||flavor is not ("FLinq" or "Linq")){
            throw new ArgumentException("Invalid carrier comparison profile.");
        }
        if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()){
            throw new InvalidOperationException("Use a fresh private data root.");
        }
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        var input=Enumerable.Range(0,rows).Select(index=>(index*4051)&(rows-1)).ToArray();
        var expected=Enumerable.Range(0,rows).Select(static id=>id*257L).ToArray();
        if(provider=="Array"){
            await Measure(()=>OrderedComparisonQueries.ArrayCarrier(input),()=>Ordinary(input));
        }else if(provider=="List"){
            var list=input.ToList();
            await Measure(()=>OrderedComparisonQueries.ListCarrier(list),()=>Ordinary(list));
        }else if(provider=="Persisted"){
            using var set=new global::Seamless.Collections.Persisted.Set<int>(Path.Combine(root,"values"),rows,rows*32L);
            foreach(var id in input){set.Add(id);}
            await Measure(()=>OrderedComparisonQueries.PersistedCarrier(set),()=>Ordinary(set));
        }else{
            var storeOptions=new StoreOptions{Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>()};
            using var owner=Store.CreateMemory(new StoreLimits{MemoryBytes=64L<<20,DiskBytes=64L<<20},options:storeOptions);
            using var set=owner.CreateTsavoriteSet<int,Row>("rows",new(){IndexSize=2L<<20,PageSize=65536,SegmentSize=4L<<20,LogMemorySize=4L<<20},rows).Create();
            foreach(var id in input){set.Add(new(id,InitialBalance(id)));}
            await Measure(()=>OrderedComparisonQueries.TsavoriteCarrier(set),
                ()=>set.Select(static row=>new{row.Id,Value=row.Id*257L}).OrderBy(static item=>item.Id).Select(static item=>item.Value).ToArray());
        }

        static long[] Ordinary(IEnumerable<int> input)=>input.Select(static id=>new{Id=id,Value=id*257L})
            .OrderBy(static item=>item.Id).Select(static item=>item.Value).ToArray();
        async Task Measure(Func<long[]> expanded,Func<long[]> ordinary){
            var run=ordinary;
            if(flavor=="FLinq"){run=expanded;}
            var samples=new List<object>();
            for(var repeat=-2;repeat<10;repeat++){
                var gc0=GC.CollectionCount(0);
                var gc1=GC.CollectionCount(1);
                var gc2=GC.CollectionCount(2);
                var allocated=GC.GetTotalAllocatedBytes(true);
                var started=Stopwatch.GetTimestamp();
                var result=run();
                var milliseconds=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                allocated=GC.GetTotalAllocatedBytes(true)-allocated;
                if(!expected.AsSpan().SequenceEqual(result)){
                    throw new InvalidDataException("Carrier results differ.");
                }
                if(repeat>=0){
                    samples.Add(new{Repeat=repeat,Milliseconds=milliseconds,AllocatedBytes=allocated,
                        Gc0=GC.CollectionCount(0)-gc0,Gc1=GC.CollectionCount(1)-gc1,Gc2=GC.CollectionCount(2)-gc2});
                }
            }
            var before=GC.GetTotalMemory(true);
            var held=run();
            var retained=GC.GetTotalMemory(true)-before;
            if(!expected.AsSpan().SequenceEqual(held)){throw new InvalidDataException("Retained carrier results differ.");}
            using var process=Process.GetCurrentProcess();
            await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{
                Format="collection-carrier-comparison-v1",Rows=rows,Provider=provider,Flavor=flavor,Affinity=ComparisonAffinity(),SourceManifestSha256=sourceHash,
                AssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))),
                Records=samples,ManagedRetainedDeltaBytes=retained,ProcessPeakWorkingSetBytes=process.PeakWorkingSet64
            },new JsonSerializerOptions{WriteIndented=true}));
            GC.KeepAlive(held);
        }
    }
    private static object RunOrdered(Database.TypedTransaction tx,int query,string flavor,int rows){
        if(flavor=="FLinq"){return OrderedComparisonQueries.ExpandedCarrier(tx,query,rows);}
        return OrderedComparisonQueries.LinqCarrier(tx.Accounts,query,rows);
    }
    private static void ValidateOrdered(object actual,object expected){
        if(actual is long[] values&&expected is long[] reference){if(!values.SequenceEqual(reference)){throw new InvalidDataException("Ordered scalar results differ.");}return;}
        if(actual is OrderedComparisonQueries.Joined[] joined&&expected is OrderedComparisonQueries.Joined[] expectedJoined){
            if(joined.Length!=expectedJoined.Length){throw new InvalidDataException("GroupJoin outer count differs.");}
            for(var index=0;index<joined.Length;index++){
                if(joined[index].Id!=expectedJoined[index].Id||!joined[index].Matches.SequenceEqual(expectedJoined[index].Matches)){
                    throw new InvalidDataException("GroupJoin member order/lifetime differs.");
                }
            }
            return;
        }
        var groups=(IGrouping<int,Row>[])actual;var expectedGroups=(IGrouping<int,Row>[])expected;
        if(groups.Length!=expectedGroups.Length){throw new InvalidDataException("Group count differs.");}
        for(var index=0;index<groups.Length;index++){if(groups[index].Key!=expectedGroups[index].Key||!groups[index].SequenceEqual(expectedGroups[index])){throw new InvalidDataException("Group order/member lifetime differs.");}}
    }
    private static long OrderedPayload(object result){
        if(result is long[] values){return values.LongLength*sizeof(long);}
        if(result is OrderedComparisonQueries.Joined[] joined){return joined.Sum(static row=>row.Matches.LongCount()*16);}
        return ((IGrouping<int,Row>[])result).Sum(static group=>group.LongCount()*16);
    }
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
