namespace Test.System.Linq.Enumerable.FLinq;

public sealed class GroupByOptimizationTests{
    private const int TopKCount=5;

    public readonly record struct Item(int GroupId,int CategoryId,long Amount);
    public readonly record struct GroupSumResult(int GroupId,long TotalAmount);
    public readonly record struct CategoryCountResult(int CategoryId,int Count);

    [Fact,Trait("Category","Unit")]public void GroupBySelectSumMatchesSystemLinq(){
        var items=global::System.Linq.Enumerable.Range(0,1_000).Select(i=>new Item(i%20,i%5,i*10L)).ToArray();
        {
            var expected=items.GroupBy(x=>x.GroupId,x=>x.Amount).Select(g=>new GroupSumResult(g.Key,g.Sum())).ToArray();
            var actual=RunGroupBySum(items);
            Assert.Equal(expected.OrderBy(x=>x.GroupId),actual.OrderBy(x=>x.GroupId));
        }
    }

    [Fact,Trait("Category","Unit")]public void GroupBySelectCountMatchesSystemLinq(){
        var items=global::System.Linq.Enumerable.Range(0,1_000).Select(i=>new Item(i%20,i%5,i*10L)).ToArray();
        {
            var expected=items.GroupBy(x=>x.CategoryId).Select(g=>new CategoryCountResult(g.Key,g.Count())).ToArray();
            var actual=RunGroupByCount(items);
            Assert.Equal(expected.OrderBy(x=>x.CategoryId),actual.OrderBy(x=>x.CategoryId));
        }
    }

    [Fact,Trait("Category","Unit")]public void GroupByOrderByTakeMatchesSystemLinq(){
        var items=global::System.Linq.Enumerable.Range(0,5_000).Select(i=>new Item(i%100,i%10,(i*37)%1000)).ToArray();
        {
            var expected=items.GroupBy(x=>x.GroupId,x=>x.Amount).Select(g=>new GroupSumResult(g.Key,g.Sum()))
                .OrderByDescending(x=>x.TotalAmount).ThenBy(x=>x.GroupId).Take(TopKCount).ToArray();
            var actual=RunGroupByOrderTake(items);
            Assert.Equal(expected,actual);
            Assert.Equal(TopKCount,actual.Length);
        }
    }

    [Fact,Trait("Category","Unit")]public void GroupByAllocationIsSignificantlyLowerThanSystemLinq(){
        var items=global::System.Linq.Enumerable.Range(0,20_000).Select(i=>new Item(i%500,i%10,(i*13)%5000)).ToArray();
        {
            RunGroupByOrderTake(items);
            items.GroupBy(x=>x.GroupId,x=>x.Amount).Select(g=>new GroupSumResult(g.Key,g.Sum()))
                .OrderByDescending(x=>x.TotalAmount).ThenBy(x=>x.GroupId).Take(TopKCount).ToArray();

            var sysAllocBefore=GC.GetAllocatedBytesForCurrentThread();
            var expected=items.GroupBy(x=>x.GroupId,x=>x.Amount).Select(g=>new GroupSumResult(g.Key,g.Sum()))
                .OrderByDescending(x=>x.TotalAmount).ThenBy(x=>x.GroupId).Take(TopKCount).ToArray();
            var sysAlloc=GC.GetAllocatedBytesForCurrentThread()-sysAllocBefore;

            var flinqAllocBefore=GC.GetAllocatedBytesForCurrentThread();
            var actual=RunGroupByOrderTake(items);
            var flinqAlloc=GC.GetAllocatedBytesForCurrentThread()-flinqAllocBefore;

            Assert.Equal(expected,actual);
            Assert.True(flinqAlloc<sysAlloc,$"FLinq ({flinqAlloc:N0} bytes) は通常LINQ ({sysAlloc:N0} bytes) より少メモリであること");
        }
    }

    [Fact,Trait("Category","Unit")]public void OptimizedVsUnoptimizedComparison(){
        var items=global::System.Linq.Enumerable.Range(0,10_000).Select(i=>new Item(i%200,i%10,(i*17)%3000)).ToArray();

        // 1. 結果の一致確認 (System.Linq vs [LinqIntercept(false)] vs [LinqIntercept(true)])
        var expectedSystem=items.GroupBy(x=>x.GroupId,x=>x.Amount).Select(g=>new GroupSumResult(g.Key,g.Sum())).OrderBy(x=>x.GroupId).ToArray();
        var unoptimizedResult=RunGroupBySumUnoptimized(items).OrderBy(x=>x.GroupId).ToArray();
        var optimizedResult=RunGroupBySum(items).OrderBy(x=>x.GroupId).ToArray();

        Assert.Equal(expectedSystem,unoptimizedResult);
        Assert.Equal(expectedSystem,optimizedResult);

        // 2. メモリ割り当て比較: 改善後(1パス融合)は改善前(非融合)より確実にアロケーションが少ないこと
        var unoptAllocBefore=GC.GetAllocatedBytesForCurrentThread();
        RunGroupBySumUnoptimized(items);
        var unoptAlloc=GC.GetAllocatedBytesForCurrentThread()-unoptAllocBefore;

        var optAllocBefore=GC.GetAllocatedBytesForCurrentThread();
        RunGroupBySum(items);
        var optAlloc=GC.GetAllocatedBytesForCurrentThread()-optAllocBefore;

        Assert.True(optAlloc<unoptAlloc,$"改善後融合FLinq ({optAlloc:N0} bytes) は改善前非融合 ({unoptAlloc:N0} bytes) よりメモリ割り当てが少ないこと");

        // 3. 実行時間比較 (JIT/GCウォームアップおよび5%以上遅延時の再実行リトライ機構付き)
        var maxAttempts=5;
        var passed=false;
        var lastUnoptTicks=0L;
        var lastOptTicks=0L;

        for(var attempt=0;attempt<maxAttempts;attempt++){
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var swUnopt=global::System.Diagnostics.Stopwatch.StartNew();
            for(var i=0;i<50;i++){
                RunGroupBySumUnoptimized(items);
            }
            swUnopt.Stop();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var swOpt=global::System.Diagnostics.Stopwatch.StartNew();
            for(var i=0;i<50;i++){
                RunGroupBySum(items);
            }
            swOpt.Stop();

            lastUnoptTicks=swUnopt.ElapsedTicks;
            lastOptTicks=swOpt.ElapsedTicks;

            // 改善後が改善前より高速、または5%以内の微差であれば合格
            if(lastOptTicks<=lastUnoptTicks*1.05){
                passed=true;
                break;
            }
        }

        Assert.True(passed,$"改善後 ({lastOptTicks} ticks) は改善前 ({lastUnoptTicks} ticks) より高速であること (リトライ後)");
    }

    [Fact,Trait("Category","Unit")]public void TopKOptimizedVsUnoptimizedComparison(){
        var items=global::System.Linq.Enumerable.Range(0,10_000).Select(i=>new Item(i%300,i%10,(i*23)%4000)).ToArray();

        var expectedSystem=items.GroupBy(x=>x.GroupId,x=>x.Amount).Select(g=>new GroupSumResult(g.Key,g.Sum()))
            .OrderByDescending(x=>x.TotalAmount).ThenBy(x=>x.GroupId).Take(TopKCount).ToArray();
        var unoptimizedResult=RunGroupByOrderTakeUnoptimized(items);
        var optimizedResult=RunGroupByOrderTake(items);

        Assert.Equal(expectedSystem,unoptimizedResult);
        Assert.Equal(expectedSystem,optimizedResult);

        var unoptAllocBefore=GC.GetAllocatedBytesForCurrentThread();
        RunGroupByOrderTakeUnoptimized(items);
        var unoptAlloc=GC.GetAllocatedBytesForCurrentThread()-unoptAllocBefore;

        var optAllocBefore=GC.GetAllocatedBytesForCurrentThread();
        RunGroupByOrderTake(items);
        var optAlloc=GC.GetAllocatedBytesForCurrentThread()-optAllocBefore;

        Assert.True(optAlloc<unoptAlloc,$"Top-K改善後融合FLinq ({optAlloc:N0} bytes) は改善前非融合 ({unoptAlloc:N0} bytes) よりメモリ割り当てが少ないこと");
    }

    [LinqIntercept(true)]
    private static GroupSumResult[]RunGroupBySum(Item[]source)=>
        source.GroupBy(x=>x.GroupId,x=>x.Amount).Select(g=>new GroupSumResult(g.Key,g.Sum())).ToArray();

    [LinqIntercept(false)]
    private static GroupSumResult[]RunGroupBySumUnoptimized(Item[]source)=>
        source.GroupBy(x=>x.GroupId,x=>x.Amount).Select(g=>new GroupSumResult(g.Key,g.Sum())).ToArray();

    [LinqIntercept(true)]
    private static CategoryCountResult[]RunGroupByCount(Item[]source)=>
        source.GroupBy(x=>x.CategoryId).Select(g=>new CategoryCountResult(g.Key,g.Count())).ToArray();

    [LinqIntercept(true)]
    private static GroupSumResult[]RunGroupByOrderTake(Item[]source)=>
        source.GroupBy(x=>x.GroupId,x=>x.Amount).Select(g=>new GroupSumResult(g.Key,g.Sum()))
            .OrderByDescending(x=>x.TotalAmount).ThenBy(x=>x.GroupId).Take(TopKCount).ToArray();

    [LinqIntercept(false)]
    private static GroupSumResult[]RunGroupByOrderTakeUnoptimized(Item[]source)=>
        source.GroupBy(x=>x.GroupId,x=>x.Amount).Select(g=>new GroupSumResult(g.Key,g.Sum()))
            .OrderByDescending(x=>x.TotalAmount).ThenBy(x=>x.GroupId).Take(TopKCount).ToArray();
}
