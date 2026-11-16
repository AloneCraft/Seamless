using System.Reflection;
using System.Runtime.Loader;
using Seamless.Transactions;
namespace Seamless.Database.Transactions.Tests;

public sealed partial class DatabaseTransactionExpansionTests{
    [Fact]
    public void UnsupportedParallelHintsProduceExpansionWarnings(){
        const string source="""
            using System.Linq;
            using Seamless.Linq;
            using Seamless.FLinq;
            using Tx=Seamless.Database.Transactions.Tests.BankDatabase.TypedTransaction;
            namespace UnsupportedParallelHintCase;
            [LinqIntercept(true)]
            public sealed class Reader{
                public int[] WrongPosition(int[] rows)=>rows.Where(static row=>row>0).Parallel(2).ToArray();
                public int[] Duplicate(Tx tx)=>tx.Accounts.Join(tx.Accounts,
                    static left=>left.Id,static right=>right.Id,static (left,right)=>left.Id).Parallel(2).Parallel(1).ToArray();
                public int[] UnsupportedInput(int[] rows)=>rows.GroupBy(static row=>row)
                    .Parallel(2).Select(static group=>group.Key).ToArray();
            }
            public static class Router{
                public static int[] Run(Tx tx,int[] rows)=>new Reader().WrongPosition(rows)
                    .Concat(new Reader().Duplicate(tx)).Concat(new Reader().UnsupportedInput(rows)).ToArray();
            }
            """;
        IReadOnlyList<global::Microsoft.CodeAnalysis.Diagnostic> observed=[];
        var(_,generated)=Generate(source,"UnsupportedParallelHintCase",observeDiagnostics:diagnostics=>observed=diagnostics);
        foreach(var name in new[]{"WrongPosition","Duplicate","UnsupportedInput"}){
            Assert.Contains(observed,diagnostic=>diagnostic.Id=="FLINQ002"&&diagnostic.GetMessage().Contains(name,StringComparison.Ordinal));
        }
        Assert.Contains(generated,static text=>text.Contains("Place Parallel immediately after Join",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("Use one Parallel hint per query chain",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("requires a supported transactional Lookup plan",StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExplicitParallelSlotsAreBoundToLookupConstruction(){
        const string source="""
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.Linq;
            using Seamless.FLinq;
            using Tx=Seamless.Database.Transactions.Tests.BankDatabase.TypedTransaction;
            namespace ParallelHintCase;
            [LinqIntercept(true,ConvergenceMode=LinqConvergenceMode.GlobalFixedPoint,EmitOptimizationTrace=true)]
            public sealed class Reader{
                private static int Mark(int slots,List<int> trace){trace.Add(slots);return slots;}
                public int[] Sequential(Tx tx)=>tx.Accounts.Join(tx.Accounts,
                    static x=>x.Id,static y=>y.Id,static (x,y)=>x.Id+y.Id).Parallel(1 << 0).ToArray();
                public int[] Dynamic(Tx tx,int slots,List<int> trace)=>tx.Accounts.Join(tx.Accounts,
                    static x=>x.Id,static y=>y.Id,static (x,y)=>x.Id+y.Id).Parallel(Mark(slots,trace))
                    .Where(static value=>value>0).Where(static value=>value%2==0).OrderBy(static value=>value).ToArray();
                public IGrouping<int,Seamless.Database.Transactions.Tests.Account>[] Groups(Tx tx,int slots)=>
                    tx.Accounts.GroupBy(static row=>row.Id).Parallel(slots).ToArray();
                public int[] AggregateGroups(Tx tx,int slots)=>tx.Accounts.GroupBy(static row=>row.Id)
                    .Parallel(slots).Select(static group=>group.Key*100+group.Count()).ToArray();
                public int[] Default(Tx tx)=>tx.Accounts.Join(tx.Accounts,
                    static x=>x.Id,static y=>y.Id,static (x,y)=>x.Id+y.Id).ToArray();
            }
            public static class Router{
                public static int[] Run(Tx tx,int slots,List<int> trace)=>new Reader().Sequential(tx)
                    .Concat(new Reader().Dynamic(tx,slots,trace)).Concat(new Reader().Default(tx)).ToArray();
                public static int GroupCount(Tx tx,int slots)=>new Reader().Groups(tx,slots).Length;
                public static int[] Aggregate(Tx tx,int slots)=>new Reader().AggregateGroups(tx,slots);
            }
            """;
        var evidence=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT");
        if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);evidence=Path.Combine(evidence,"parallel-hint.g.cs");}
        var(output,generated)=Generate(source,"ParallelHintCase",generatedEvidencePath:evidence);
        var methods=generated.SelectMany(static text=>global::Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(text).GetRoot()
            .DescendantNodes().OfType<global::Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()).ToArray();
        foreach(var name in new[]{"Sequential","Default"}){
            Assert.Contains(methods,method=>method.Identifier.ValueText.StartsWith("OptimizedLinq_"+name+"_",StringComparison.Ordinal)
                &&method.Body!.ToFullString().Contains("allowParallel:false",StringComparison.Ordinal)
                &&!method.Body!.ToFullString().Contains("__parallelSlots",StringComparison.Ordinal));
        }
        foreach(var name in new[]{"Dynamic","Groups","AggregateGroups"}){
            Assert.Contains(methods,method=>method.Identifier.ValueText.StartsWith("OptimizedLinq_"+name+"_",StringComparison.Ordinal)
                &&method.Body!.ToFullString().Contains("maxExecutionSlots:",StringComparison.Ordinal));
        }
        Assert.Contains(generated,static text=>text.Contains("コンパイル時に確定",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("指定なしの既定値",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("指定値はproducer込み",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("Pipeline.Where",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("GroupByConsumptionPlan",StringComparison.Ordinal));
        using var image=new MemoryStream();
        var emit=output.Emit(image);
        Assert.True(emit.Success,string.Join(Environment.NewLine,emit.Diagnostics));
        image.Position=0;
        var load=new AssemblyLoadContext("ParallelHintCase",isCollectible:true);
        try{
            var router=load.LoadFromStream(image).GetType("ParallelHintCase.Router")!;
            using var db=new BankDatabase(DatabaseContainerOptions.InMemory with{MaximumQueryWorkers=2});
            await using(var seed=await db.BeginTransactionAsync()){
                seed.Accounts.Add(new(1,100));
                seed.Accounts.Add(new(2,200));
                await seed.CommitAsync();
            }
            await using var tx=await db.BeginReadOnlyTransactionAsync();
            foreach(var slots in new[]{1,2}){
                var trace=new List<int>();
                var rows=Assert.IsType<int[]>(router.GetMethod("Run")!.Invoke(null,[tx,slots,trace]));
                Assert.Equal(new[]{2,2,2,4,4,4},rows.Order().ToArray());
                Assert.Equal(new[]{slots},trace);
                Assert.Equal(2,router.GetMethod("GroupCount")!.Invoke(null,[tx,slots]));
                var expected=global::System.Linq.Enumerable.GroupBy(tx.Accounts,static row=>row.Id)
                    .Select(static group=>group.Key*100+group.Count()).ToArray();
                Assert.Equal(expected,Assert.IsType<int[]>(router.GetMethod("Aggregate")!.Invoke(null,[tx,slots])));
            }
            var invalidTrace=new List<int>();
            var invalid=Assert.Throws<TargetInvocationException>(()=>router.GetMethod("Run")!.Invoke(null,[tx,0,invalidTrace]));
            Assert.Equal("maxExecutionSlots",Assert.IsType<ArgumentOutOfRangeException>(invalid.InnerException).ParamName);
            Assert.Equal(new[]{0},invalidTrace);
            await tx.CommitAsync();
        }finally{
            load.Unload();
        }
    }

    [Theory]
    [InlineData(false,1)]
    [InlineData(true,3)]
    public async Task GroupJoinUsesBorrowedCountsAndSharesOwnedMatches(bool versioned,int workers){
        const string source="""
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            using Account=Seamless.Database.Transactions.Tests.Account;
            using Tx=Seamless.Database.Transactions.Tests.BankDatabase.TypedTransaction;
            namespace GroupJoinLifetimeCase;
            public readonly record struct Joined(int Id,IEnumerable<Account> Matches);
            public sealed class NullZeroComparer:IEqualityComparer<int?>{
                public bool Equals(int? x,int? y)=>x.GetValueOrDefault()==y.GetValueOrDefault();
                public int GetHashCode(int? value)=>value.GetValueOrDefault();
            }
            [LinqIntercept(true)]
            public sealed class Reader{
                private static int Mark(List<int> trace,int value){trace.Add(value);return value;}
                public Joined[] Owned(Tx tx)=>tx.Accounts.Where(static row=>row.Id<4).GroupJoin(
                    tx.Accounts,static row=>row.Id==3?99:row.Id/2,static row=>row.Id%4,
                    static (row,group)=>new Joined(row.Id,group)).OrderBy(static row=>row.Id).ToArray();
                public int[] Counts(Tx tx)=>tx.Accounts.Where(static row=>row.Id<4).GroupJoin(
                    tx.Accounts,static row=>row.Id==3?99:row.Id/2,static row=>row.Id%4,
                    static (row,group)=>group.Count()).ToArray();
                public int[] SignedCounts(Tx tx,int limit,List<int> trace)=>tx.Accounts.Where(row=>row.Id<limit).GroupJoin(
                    tx.Accounts,row=>Mark(trace,100+row.Id)-102,row=>Mark(trace,row.Id-32)%4,
                    (row,group)=>Mark(trace,1000+row.Id*100+group.Count())).ToArray();
                public int[] PredicateCounts(Tx tx)=>tx.Accounts.Where(static row=>row.Id<4).GroupJoin(
                    tx.Accounts,static row=>row.Id,static row=>row.Id%4,
                    static (row,group)=>group.Count(static match=>match.Id<32)).ToArray();
                public long[] SignedLongCounts(Tx tx,int limit,List<int> trace)=>tx.Accounts.Where(row=>row.Id<limit).GroupJoin(
                    tx.Accounts,row=>Mark(trace,100+row.Id)-102,row=>Mark(trace,row.Id-32)%4,
                    static (row,group)=>group.LongCount()).ToArray();
                public long[] StaticLongCounts(Tx tx,int limit,List<int> trace)=>tx.Accounts.Where(row=>row.Id<limit).GroupJoin(
                    tx.Accounts,row=>Mark(trace,100+row.Id)-102,row=>Mark(trace,row.Id-32)%4,
                    static (row,group)=>(Enumerable.LongCount(source:(group)))).ToArray();
                public long[] PredicateLongCounts(Tx tx)=>tx.Accounts.Where(static row=>row.Id<4).GroupJoin(
                    tx.Accounts,static row=>row.Id,static row=>row.Id%4,
                    static (row,group)=>group.LongCount(static match=>match.Id<32)).ToArray();
                public int[] Empty(Tx tx)=>tx.Accounts.Where(static row=>row.Id<4).GroupJoin(
                    tx.Accounts.Where(static row=>row.Id<0),static row=>row.Id/2,static row=>row.Id%4,
                    static (row,group)=>row.Id*10+group.Count()).OrderBy(static value=>value).ToArray();
                public int[] NullableKey(Tx tx)=>tx.Accounts.Where(static row=>row.Id<4).GroupJoin(
                    tx.Accounts,static row=>row.Id==0?(int?)null:row.Id/2,
                    static row=>row.Id==0?(int?)null:row.Id%4,
                    static (row,group)=>row.Id*100+group.Count(),new NullZeroComparer()).OrderBy(static value=>value).ToArray();
            }
            public sealed class Router{
                public static Joined[] Owned(Tx tx)=>new Reader().Owned(tx);
                public static int[] Counts(Tx tx)=>new Reader().Counts(tx);
                public static int[] SignedCounts(Tx tx,int limit,List<int> trace)=>new Reader().SignedCounts(tx,limit,trace);
                public static int[] PredicateCounts(Tx tx)=>new Reader().PredicateCounts(tx);
                public static long[] SignedLongCounts(Tx tx,int limit,List<int> trace)=>new Reader().SignedLongCounts(tx,limit,trace);
                public static long[] StaticLongCounts(Tx tx,int limit,List<int> trace)=>new Reader().StaticLongCounts(tx,limit,trace);
                public static long[] PredicateLongCounts(Tx tx)=>new Reader().PredicateLongCounts(tx);
                public static int[] Empty(Tx tx)=>new Reader().Empty(tx);
                public static int[] NullableKey(Tx tx)=>new Reader().NullableKey(tx);
            }
            """;
        var(output,generated)=Generate(source,"GroupJoinLifetimeCase");
        Assert.Contains(generated,static text=>text.Contains("[GroupJoin/整列/取引]",StringComparison.Ordinal)&&text.Contains("MaterializeShared",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("[GroupJoin/借用]",StringComparison.Ordinal));
        var generatedMethods=generated.SelectMany(static text=>global::Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(text).GetRoot()
            .DescendantNodes().OfType<global::Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()).ToArray();
        Assert.Contains(generatedMethods,static method=>method.Identifier.ValueText.StartsWith("OptimizedLinq_SignedCounts_",StringComparison.Ordinal)
            &&method.Body!.ToFullString().Contains("DenseGroupCounts<",StringComparison.Ordinal));
        Assert.Contains(generatedMethods,static method=>method.Identifier.ValueText.StartsWith("OptimizedLinq_PredicateCounts_",StringComparison.Ordinal)
            &&method.Body!.ToFullString().Contains("PartitionedLookupBuilder<",StringComparison.Ordinal));
        foreach(var name in new[]{"SignedLongCounts","StaticLongCounts"}){
            Assert.Contains(generatedMethods,method=>method.Identifier.ValueText.StartsWith("OptimizedLinq_"+name+"_",StringComparison.Ordinal)
                &&method.Body!.ToFullString().Contains("DenseGroupCounts<",StringComparison.Ordinal)
                &&method.Body.ToFullString().Contains(".GetLongCount(",StringComparison.Ordinal));
        }
        Assert.Contains(generatedMethods,static method=>method.Identifier.ValueText.StartsWith("OptimizedLinq_PredicateLongCounts_",StringComparison.Ordinal)
            &&method.Body!.ToFullString().Contains("PartitionedLookupBuilder<",StringComparison.Ordinal));
        using var image=new MemoryStream();
        var emit=output.Emit(image);
        Assert.True(emit.Success,string.Join(Environment.NewLine,emit.Diagnostics));
        image.Position=0;
        var load=new AssemblyLoadContext("GroupJoinLifetimeCase",isCollectible:true);
        try{
            var router=load.LoadFromStream(image).GetType("GroupJoinLifetimeCase.Router")!;
            var options=DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536,MaximumQueryWorkers=workers};
            if(versioned){
                options=options with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,TransactionHistory=new(){MaxRetainedMemoryBytes=65536,MaxRetainedStorageBytes=0,MaxMetadataBytes=524288,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}};
            }
            using var db=new BankDatabase(options);
            await using(var seed=await db.BeginTransactionAsync()){
                for(var id=0;id<64;id++){seed.Accounts.Add(new(id,100+id));}
                await seed.CommitAsync();
            }
            Array owned;
            await using(var tx=await db.BeginTransactionAsync()){
                owned=(Array)router.GetMethod("Owned")!.Invoke(null,[tx])!;
                Assert.Equal(new[]{0,16,16,16},((int[])router.GetMethod("Counts")!.Invoke(null,[tx])!).OrderBy(static x=>x));
                Assert.Equal(new[]{0,10,20,30},(int[])router.GetMethod("Empty")!.Invoke(null,[tx])!);
                Assert.Equal(new[]{15,115,216,316},(int[])router.GetMethod("NullableKey")!.Invoke(null,[tx])!);
                Assert.Equal(new[]{8,8,8,8},(int[])router.GetMethod("PredicateCounts")!.Invoke(null,[tx])!);
                Assert.Equal(new long[]{8,8,8,8},(long[])router.GetMethod("PredicateLongCounts")!.Invoke(null,[tx])!);
                var sourceRows=tx.Accounts.ToArray();
                foreach(var limit in new[]{0,8}){
                    var expectedTrace=new List<int>();var actualTrace=new List<int>();
                    int Mark(int value){expectedTrace.Add(value);return value;}
                    var expected=sourceRows.Where(row=>row.Id<limit).GroupJoin(sourceRows,
                        row=>Mark(100+row.Id)-102,row=>Mark(row.Id-32)%4,
                        (row,group)=>Mark(1000+row.Id*100+group.Count())).ToArray();
                    Assert.Equal(expected,(int[])router.GetMethod("SignedCounts")!.Invoke(null,[tx,limit,actualTrace])!);
                    Assert.Equal(expectedTrace,actualTrace);
                    expectedTrace.Clear();actualTrace.Clear();
                    var expectedLong=sourceRows.Where(row=>row.Id<limit).GroupJoin(sourceRows,
                        row=>Mark(100+row.Id)-102,row=>Mark(row.Id-32)%4,
                        static (row,group)=>group.LongCount()).ToArray();
                    foreach(var name in new[]{"SignedLongCounts","StaticLongCounts"}){
                        actualTrace.Clear();
                        Assert.Equal(expectedLong,(long[])router.GetMethod(name)!.Invoke(null,[tx,limit,actualTrace])!);
                        Assert.Equal(expectedTrace,actualTrace);
                    }
                }
                await tx.CommitAsync();
            }
            var rows=owned.Cast<object>().Select(static box=>(IEnumerable<Account>)box.GetType().GetProperty("Matches")!.GetValue(box)!).ToArray();
            Assert.Same(rows[0],rows[1]);
            Assert.Equal(Enumerable.Range(0,16).Select(static index=>index*4),rows[0].Select(static row=>row.Id).OrderBy(static id=>id));
            Assert.Equal(Enumerable.Range(0,16).Select(static index=>index*4+1),rows[2].Select(static row=>row.Id).OrderBy(static id=>id));
            Assert.Empty(rows[3]);
        }finally{load.Unload();}
    }
    [Theory]
    [InlineData(false,1)]
    [InlineData(false,3)]
    [InlineData(true,1)]
    [InlineData(true,3)]
    public async Task EscapingGroupsKeepOrderCallbacksAndLifetimeAcrossTerminals(bool versioned,int workers){
        const string methods="""
            private static int Note(List<int> trace,int key){trace.Add(key);return key;}
            private static Box Capture(List<int> trace,int key,IEnumerable<Account> rows){trace.Add(100+key);return new Box(key,rows);}
            public IGrouping<int,Account>[] Array(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).Skip(1).Take(take).ToArray();
            public List<IGrouping<int,Account>> List(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).Take(take).ToList();
            public IGrouping<int,Account> First(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).Take(take).First();
            public IGrouping<int,Account> Last(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).Skip(1).Take(take).Last();
            public IGrouping<int,Account> LastWhere(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).Where(group=>Note(trace,10+group.Key)>10).Take(take).LastOrDefault();
            public Box ProjectLast(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).Select(group=>Capture(trace,group.Key,group)).Take(take).LastOrDefault();
            public Box[] ResultSelector(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3),(key,rows)=>Capture(trace,key,rows)).Take(take).ToArray();
            public Dictionary<int,IGrouping<int,Account>> Dictionary(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).Take(take).ToDictionary(static group=>group.Key);
            public IGrouping<int,Account>[] OrderedArray(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).OrderByDescending(group=>Note(trace,20+group.Key)).ThenBy(group=>Note(trace,30+group.Count())).Take(take).ToArray();
            public IGrouping<int,Account> OrderedFirst(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).OrderBy(group=>Note(trace,20+group.Key)).Take(take).First();
            public IGrouping<int,Account> OrderedLast(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).OrderBy(group=>Note(trace,20+group.Key)).Take(take).LastOrDefault();
            public Box OrderedProjectLast(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).Select(group=>Capture(trace,group.Key,group)).OrderBy(box=>Note(trace,20+box.Key)).Take(take).LastOrDefault();
            public Box[] OrderedWindow(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).Select(group=>Capture(trace,group.Key,group)).Take(take).Where(box=>Note(trace,40+box.Key)>41).OrderBy(box=>Note(trace,20+box.Key)).ToArray();
            public List<IGrouping<int,Account>> OrderedList(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).OrderBy(group=>Note(trace,20+group.Key)).Take(take).ToList();
            public Dictionary<int,IGrouping<int,Account>> OrderedDictionary(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).OrderByDescending(group=>Note(trace,20+group.Key)).Take(take).ToDictionary(static group=>group.Key);
            public IGrouping<int,Account>[] OrderedComparer(Tx tx,List<int> trace,int take){IComparer<int> comparer=Comparer<int>.Create((left,right)=>{trace.Add(200+left*3+right);return right.CompareTo(left);});return tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).OrderBy(group=>Note(trace,20+group.Key),comparer).Take(take).ToArray();}
            private static IComparer<int> MakeComparer(List<int> trace,int phase){trace.Add(phase);return Comparer<int>.Create((left,right)=>{trace.Add(phase+100+left*3+right);return right.CompareTo(left);});}
            private static IEqualityComparer<int> MakeEquality(List<int> trace){trace.Add(900);return EqualityComparer<int>.Default;}
            private static IComparer<int> ChangeTake(List<int> trace,ref int take){trace.Add(950);take=1;return Comparer<int>.Default;}
            public IGrouping<int,Account>[] OrderedFactory(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).OrderBy(group=>Note(trace,20+group.Key),MakeComparer(trace,700)).Take(take).ToArray();
            public IGrouping<int,Account>[] GroupFactory(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3),MakeEquality(trace)).Take(take).ToArray();
            public IGrouping<int,Account>[] OrderedTwiceArray(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).OrderBy(group=>Note(trace,20+group.Key),MakeComparer(trace,700)).ThenBy(group=>Note(trace,30+group.Count())).OrderBy(group=>Note(trace,40+group.Count()),MakeComparer(trace,800)).Take(take).ToArray();
            public IGrouping<int,Account> OrderedTwiceFirst(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).OrderBy(group=>Note(trace,20+group.Key)).OrderByDescending(group=>Note(trace,40+group.Count())).Take(take).FirstOrDefault();
            public Box[] OrderedTwiceProjection(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).OrderBy(group=>Note(trace,20+group.Key)).Select(group=>Capture(trace,group.Key,group)).Where(box=>Note(trace,50+box.Key)>50).OrderBy(box=>Note(trace,60+box.Key)).Take(take).ToArray();
            public Account[][] OrderedTwiceArrays(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).OrderBy(group=>Note(trace,20+group.Key)).Select(group=>group.ToArray()).OrderByDescending(rows=>Note(trace,60+rows.Length)).Take(take).ToArray();
            public IGrouping<int,Account>[] FactoryAfterWindow(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).Take(take).OrderBy(group=>Note(trace,20+group.Key),ChangeTake(trace,ref take)).ToArray();
            public IGrouping<int,Account>[] FactoryBetweenWindows(Tx tx,List<int> trace,int take)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3)).OrderBy(group=>Note(trace,20+group.Key)).Take(take).OrderBy(group=>Note(trace,40+group.Key),ChangeTake(trace,ref take)).Take(take).ToArray();
            """;
        var wrappers=string.Join("",global::System.Text.RegularExpressions.Regex.Matches(methods,@"public ([^\r\n]+?) (\w+)\(Tx").Select(static match=>"public "+match.Groups[1].Value+" "+match.Groups[2].Value+"(Tx tx,List<int> trace,int take)=>new Reader()."+match.Groups[2].Value+"(tx,trace,take);"));
        var source="using System;using System.Linq;using System.Collections.Generic;using Seamless.FLinq;using Account=Seamless.Database.Transactions.Tests.Account;using Tx=Seamless.Database.Transactions.Tests.BankDatabase.TypedTransaction;namespace GroupLifetimeCase;public sealed record Box(int Key,IEnumerable<Account> Rows);[LinqIntercept(true)]public sealed class Reader{"+methods+"}[LinqIntercept(false)]public sealed class Reference{"+methods+"}public sealed class Router{"+wrappers+"}";
        var(output,generated)=Generate(source,"GroupLifetimeCase");
        var emittedMethods=generated.SelectMany(static text=>global::Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<global::Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()).ToArray();
        foreach(var name in new[]{"Array","List","First","Last","LastWhere","ProjectLast","ResultSelector","Dictionary","OrderedArray","OrderedFirst","OrderedLast","OrderedProjectLast","OrderedWindow","OrderedList","OrderedDictionary","OrderedComparer","OrderedFactory","GroupFactory","OrderedTwiceArray","OrderedTwiceFirst","OrderedTwiceProjection","OrderedTwiceArrays","FactoryAfterWindow","FactoryBetweenWindows"}){
            Assert.True(emittedMethods.Any(method=>method.Identifier.ValueText.StartsWith("OptimizedLinq_"+name+"_",StringComparison.Ordinal)&&method.Body!.ToFullString().Contains("QueryGrouping.Materialize",StringComparison.Ordinal)),name+" did not use owned groups.");
        }
        using var image=new MemoryStream();var emitted=output.Emit(image);Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));image.Position=0;
        var load=new AssemblyLoadContext("group-lifetime-"+Guid.NewGuid(),isCollectible:true);load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var assembly=load.LoadFromStream(image);var reader=assembly.GetType("GroupLifetimeCase.Router")!;var reference=assembly.GetType("GroupLifetimeCase.Reference")!;var actual=Activator.CreateInstance(reader);var expected=Activator.CreateInstance(reference);
            var options=DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536,MaximumQueryWorkers=workers};
            if(versioned){options=options with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,TransactionHistory=new(){MaxRetainedMemoryBytes=65536,MaxRetainedStorageBytes=0,MaxMetadataBytes=524288,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}};}
            using var db=new BankDatabase(options);await using(var seed=await db.BeginTransactionAsync()){for(var id=0;id<9;id++){seed.Accounts.Add(new(id,100+id));}await seed.CommitAsync();}
            foreach(var method in reader.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)){
                foreach(var empty in new[]{false,true}){
                    foreach(var take in new[]{-1,0,1,2,100}){
                        await using var tx=await db.BeginTransactionAsync();tx.Accounts[1]=new(1,77);tx.Accounts.Remove(2);
                        if(empty){foreach(var row in tx.Accounts.ToArray()){tx.Accounts.Remove(row.Id);}}
                        var a=new List<int>();var e=new List<int>();object? av=null;object? ev=null;Type? ae=null;Type? ee=null;
                        try{ev=reference.GetMethod(method.Name)!.Invoke(expected,[tx,e,take]);}catch(TargetInvocationException error){ee=error.InnerException!.GetType();}
                        try{av=method.Invoke(actual,[tx,a,take]);}catch(TargetInvocationException error){ae=error.InnerException!.GetType();}
                        Assert.Equal(ee,ae);Assert.True(e.SequenceEqual(a),$"{method.Name}, empty={empty}, take={take}: expected [{string.Join(',',e)}], actual [{string.Join(',',a)}]");await tx.RollbackAsync();
                        // query scopeと取引が閉じた後に、全groupと遅延IEnumerableを初めて列挙する。
                        Assert.Equal(Snapshot(ev),Snapshot(av));
                    }
                }
            }
        }finally{load.Unload();}
        static string Snapshot(object? value){
            if(value is null){return "null";}
            if(value is Account row){return row.Id+"/"+row.Balance;}
            if(value is IGrouping<int,Account> group){return group.Key+":"+string.Join(";",group.Select(static row=>row.Id+"/"+row.Balance));}
            if(value is global::System.Collections.IDictionary dictionary){return string.Join("|",dictionary.Values.Cast<object>().Select(Snapshot));}
            if(value is global::System.Collections.IEnumerable sequence){return string.Join("|",sequence.Cast<object>().Select(Snapshot));}
            var type=value.GetType();return type.GetProperty("Key")!.GetValue(value)+":"+string.Join(";",((IEnumerable<Account>)type.GetProperty("Rows")!.GetValue(value)!).Select(static row=>row.Id+"/"+row.Balance));
        }
    }
}
