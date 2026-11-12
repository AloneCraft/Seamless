using System.Reflection;
using System.Runtime.Loader;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Transactions;
using Seamless.Transactions.Collections;
using Store=Seamless.Execution.Store;
using WideRow=(int Id,long Balance,long A,long B,long C,long D,long E,long F);
namespace Seamless.Database.Transactions.Tests;

public sealed partial class DatabaseTransactionExpansionTests{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task EscapingTransactionGroupsRespectTheWorkingBudgetAndRemainUsableAfterTheScopeEnds(int shape){
        const string source="""
            using System.Linq;
            using System.Collections.Generic;
            using Seamless.FLinq;
            using Seamless.Transactions.Collections;
            namespace EscapingGroupCase;
            [LinqIntercept(true)]public sealed class Reader{
                public IGrouping<int,(int Id,long Balance)>[] Groups(TransactionRows<int,(int Id,long Balance)> rows)=>rows.GroupBy(static row=>0).ToArray();
                public (int Id,long Balance,long A,long B,long C,long D,long E,long F)[][] Arrays(TransactionRows<int,(int Id,long Balance)> rows)=>rows.GroupBy(static row=>0,static row=>(row.Id,row.Balance,row.Balance,row.Balance,row.Balance,row.Balance,row.Balance,row.Balance)).Select(static group=>group.ToArray()).ToArray();
                public (int Id,long Balance,long A,long B,long C,long D,long E,long F)[][] OrderedArrays(TransactionRows<int,(int Id,long Balance)> rows)=>rows.GroupBy(static row=>0,static row=>(row.Id,row.Balance,row.Balance,row.Balance,row.Balance,row.Balance,row.Balance,row.Balance)).OrderBy(static group=>group.Key).Select(static group=>group.ToArray()).ToArray();
                public IGrouping<int,(int Id,long Balance,long A,long B,long C,long D,long E,long F)>[] OrderedTwiceArrays(TransactionRows<int,(int Id,long Balance)> rows)=>rows.GroupBy(static row=>0,static row=>(row.Id,row.Balance,row.Balance,row.Balance,row.Balance,row.Balance,row.Balance,row.Balance)).OrderBy(static group=>group.Key).OrderByDescending(static group=>group.Key).ToArray();
                private static IComparer<int> MakeComparer()=>Comparer<int>.Default;
                public IGrouping<int,(int Id,long Balance,long A,long B,long C,long D,long E,long F)>[] FactoryComparerArrays(TransactionRows<int,(int Id,long Balance)> rows)=>rows.GroupBy(static row=>0,static row=>(row.Id,row.Balance,row.Balance,row.Balance,row.Balance,row.Balance,row.Balance,row.Balance)).OrderBy(static group=>group.Key,MakeComparer()).ToArray();
                public int JoinedCount(TransactionRows<int,(int Id,long Balance)> rows)=>rows.Join(rows.Where(static row=>row.Id==0),static row=>0,static row=>0,static(left,right)=>left.Id).Count();
                public (int Id,long Balance,long A,long B,long C,long D,long E,long F,long G,long H,long I,long J,long K,long L,long M,long N)[] JoinedOrderedArrays(TransactionRows<int,(int Id,long Balance)> rows)=>rows.Join(rows.Where(static row=>row.Id==0),static row=>0,static row=>0,static(left,right)=>(left.Id,left.Balance,left.Balance,left.Balance,left.Balance,left.Balance,left.Balance,left.Balance,left.Balance,left.Balance,left.Balance,left.Balance,left.Balance,left.Balance,left.Balance,left.Balance)).OrderBy(static row=>row.Id).ToArray();
            }
            public sealed class Router{
                public IGrouping<int,(int Id,long Balance)>[] Groups(TransactionRows<int,(int Id,long Balance)> rows)=>new Reader().Groups(rows);
                public (int Id,long Balance,long A,long B,long C,long D,long E,long F)[][] Arrays(TransactionRows<int,(int Id,long Balance)> rows)=>new Reader().Arrays(rows);
                public (int Id,long Balance,long A,long B,long C,long D,long E,long F)[][] OrderedArrays(TransactionRows<int,(int Id,long Balance)> rows)=>new Reader().OrderedArrays(rows);
                public IGrouping<int,(int Id,long Balance,long A,long B,long C,long D,long E,long F)>[] OrderedTwiceArrays(TransactionRows<int,(int Id,long Balance)> rows)=>new Reader().OrderedTwiceArrays(rows);
                public IGrouping<int,(int Id,long Balance,long A,long B,long C,long D,long E,long F)>[] FactoryComparerArrays(TransactionRows<int,(int Id,long Balance)> rows)=>new Reader().FactoryComparerArrays(rows);
                public int JoinedCount(TransactionRows<int,(int Id,long Balance)> rows)=>new Reader().JoinedCount(rows);
                public (int Id,long Balance,long A,long B,long C,long D,long E,long F,long G,long H,long I,long J,long K,long L,long M,long N)[] JoinedOrderedArrays(TransactionRows<int,(int Id,long Balance)> rows)=>new Reader().JoinedOrderedArrays(rows);
            }
            """;
        var(output,generated)=Generate(source,"EscapingGroupCase");
        Assert.Contains(generated,static text=>text.Contains("OptimizedLinq_Groups_",StringComparison.Ordinal));
        using var image=new MemoryStream();var emitted=output.Emit(image);Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));image.Position=0;
        var load=new AssemblyLoadContext("escaping-groups-"+Guid.NewGuid(),isCollectible:true);load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var assembly=load.LoadFromStream(image);var type=assembly.GetType("EscapingGroupCase.Router")!;var instance=Activator.CreateInstance(type);var method=type.GetMethod("Groups")!;var arrays=type.GetMethod("Arrays")!;
            if(shape==1){arrays=type.GetMethod("OrderedArrays")!;}else if(shape==2){arrays=type.GetMethod("OrderedTwiceArrays")!;}else if(shape==3){arrays=type.GetMethod("FactoryComparerArrays")!;}else if(shape==4){arrays=type.GetMethod("JoinedOrderedArrays")!;}
            var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"escaping-groups-"+Guid.NewGuid().ToString("N"));
            using var resources=new ExecutionScope(new(){MemoryLimitBytes=32L<<20,TemporaryStorageLimitBytes=16L<<20,TemporaryDirectory=directory});
            using var store=Store.CreateMemory(new(){MemoryBytes=24L<<20,DiskBytes=0},parentBudget:resources.Budget);
            const string codec="escaping-groups";
            var data=store.CreateTsavoriteHistory("rows",new TsavoriteTuning{IndexSize=32768,PageSize=4096,SegmentSize=65536,LogMemorySize=8L<<20,Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>()},VersionedTransactionalTable<int,(int,long)>.StorageIdentity(codec,VersionedRecordEncoding.Canonical));
            using var table=new VersionedTransactionalTable<int,(int Id,long Balance)>(0,1,codec,data,resources,64<<10,static row=>row.Id);
            await using var owner=new TransactionCoordinator(null,null,null,new(){Concurrency=TransactionConcurrencyMode.Optimistic,ParentBudget=resources.Budget,
                History=new(){MaxRetainedMemoryBytes=8L<<20,MaxRetainedStorageBytes=0,MaxMetadataBytes=2L<<20,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}});owner.Register(table);
            const int count=16384;
            for(var start=0;start<count;start+=256){await using var seed=await owner.BeginAsync();for(var id=start;id<start+256;id++){table.For(seed).Add((id,100+id));}await seed.CommitAsync();}
            var read=await owner.BeginReadOnlyAsync();
            try{
                var rows=table.For(read).Rows;Assert.Equal(count,global::System.Linq.Enumerable.Count(rows));
                var baseline=resources.Budget.MemoryBytes;
                // 実配列は確保せず残予算だけを予約。逐次走査の約516KiBは収まるが、要求した行配列だけで1MiBある。
                var available=768<<10;if(shape==4){available=1536<<10;}
                using(var pressure=resources.Budget.ReserveMemory(resources.Budget.AvailableMemoryBytes-available)){
                    Assert.Equal(count,global::System.Linq.Enumerable.Count(rows));
                    if(shape==4){Assert.Equal(count,(int)type.GetMethod("JoinedCount")!.Invoke(instance,[rows])!);}
                    var error=Assert.Throws<TargetInvocationException>(()=>arrays.Invoke(instance,[rows]));Assert.IsType<ResourceLimitException>(error.InnerException);
                }
                Assert.Equal(baseline,resources.Budget.MemoryBytes);Assert.Null(owner.Failure.Error);
                if(shape==4){
                    var projected=(Array)arrays.Invoke(instance,[rows])!;Assert.Equal(count,projected.Length);
                    var id=projected.GetType().GetElementType()!.GetField("Item1")!;
                    Assert.Equal(Enumerable.Range(0,count),projected.Cast<object>().Select(value=>(int)id.GetValue(value)!));
                }else{
                    WideRow[][] projected;
                    if(shape>=2){projected=((IGrouping<int,WideRow>[])arrays.Invoke(instance,[rows])!).Select(static group=>group.ToArray()).ToArray();}
                    else{projected=(WideRow[][])arrays.Invoke(instance,[rows])!;}
                    Assert.Equal(count,Assert.Single(projected).Length);Assert.Equal(Enumerable.Range(0,count),projected[0].Select(static row=>row.Id).Order());
                    Assert.All(projected[0],static row=>Assert.Equal((long)100+row.Id,row.Balance));
                }
                Assert.Equal(baseline,resources.Budget.MemoryBytes);
                var groups=(IGrouping<int,(int Id,long Balance)>[])method.Invoke(instance,[rows])!;
                Assert.Equal(baseline,resources.Budget.MemoryBytes);
                await read.DisposeAsync();Assert.Equal(0,owner.GetDomainDiagnostics().ActiveTransactions);
                var group=Assert.Single(groups);Assert.Equal(0,group.Key);Assert.Equal(count,group.Count());Assert.Equal((long)count*100+(long)count*(count-1)/2,group.Sum(static row=>row.Balance));
                Assert.Equal(Enumerable.Range(0,count),group.Select(static row=>row.Id).Order());
            }finally{await read.DisposeAsync();}
        }finally{load.Unload();}
    }
}
