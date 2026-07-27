namespace Test.Persisted.Linq.Enumerable.FLinq;

public sealed class ToHashSetPresizeTests {
    private static global::Seamless.Collections.Persisted.Set<int>? source;
    internal static readonly global::Common.Consumer sink = new();

    // Benchmark.db の実測(実行ID=7)では Persisted<T>/Tsavorite<T> の ToHashSet が
    // FLinq 対 ValueLinq で 1.7〜2.8x 多く割り当てていた。ToHashSet.cs の Compute は
    // source.TryGetNonEnumeratedCount で事前サイズ決定を試みるが、Persisted/Tsavorite の
    // 直接型ルート(ProviderNativeRootBase)が生成する汎用 struct の TryGetNonEnumeratedCount は
    // どのソースでも安全に使える汎用実装のため、Set<T>.Count が O(1) であることを知らず常に false を返す。
    // このため HashSet<T> が空から成長し、その分だけ余分に確保していた(このテストで実測: 2000要素で 1.85x)。
    // ToHashSet.cs に RenderProviderNativeToHashSetInline を追加し、NativeCountExpression で
    // 事前サイズ決定してから詰める inline を生成するよう修正した。
    [Fact,Trait("Category","Unit")]
    public void ToHashSet_AllocatesNoMoreThanPresizedIdeal() {
        var path=Path.Combine(Path.GetTempPath(),"Seamless-ToHashSetProbe-"+Guid.NewGuid().ToString("N"),"items.dat");
        try {
            using var set=global::Seamless.Collections.Persisted.Store.CreateSet<int>(path,InitialCapacity:1,InitialDataCapacity:4096);
            const int count=2000;
            for(var i=1;i<=count;i++)
                set.Add(i);
            source=set;

            for(var i=0;i<5;i++) {
                _=FLinqToHashSetImpl().Count;
                _=PresizedIdealImpl().Count;
            }

            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            var beforeFlinq=GC.GetAllocatedBytesForCurrentThread();
            var flinqResult=FLinqToHashSetImpl();
            var flinqBytes=GC.GetAllocatedBytesForCurrentThread()-beforeFlinq;

            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            var beforeIdeal=GC.GetAllocatedBytesForCurrentThread();
            var idealResult=PresizedIdealImpl();
            var idealBytes=GC.GetAllocatedBytesForCurrentThread()-beforeIdeal;

            Assert.Equal(count,flinqResult.Count);
            Assert.Equal(count,idealResult.Count);
            Assert.True(flinqBytes<=idealBytes,
                $"FLinq allocated {flinqBytes}B, presized ideal allocated {idealBytes}B.");
        } finally {
            source=null;
            var directory=Path.GetDirectoryName(path);
            if(!string.IsNullOrWhiteSpace(directory)&&Directory.Exists(directory))
                Directory.Delete(directory,recursive:true);
        }
    }

    // 上のテストは Source().ToHashSet() の形で inline 経路(RenderProviderNativeToHashSetInline)を通る。
    // Benchmark の ToHashSet_Impl は結果をさらに列挙する `.AsX<T>().ToHashSet().Consume(consumer)` の形で、
    // こちらは struct 経路(ComputeMembers)になる。inline だけ直しても Benchmark は改善しないため
    // (実測で 1.86x 負けたままだった)、struct 経路も同じ理想値と比較して固定する。
    [Fact,Trait("Category","Unit")]
    public void ToHashSet_StructPathAllocatesNoMoreThanPresizedIdeal() {
        var path=Path.Combine(Path.GetTempPath(),"Seamless-ToHashSetStructProbe-"+Guid.NewGuid().ToString("N"),"items.dat");
        try {
            using var set=global::Seamless.Collections.Persisted.Store.CreateSet<int>(path,InitialCapacity:1,InitialDataCapacity:4096);
            const int count=2000;
            for(var i=1;i<=count;i++)
                set.Add(i);
            source=set;

            for(var i=0;i<5;i++) {
                FLinqToHashSetConsumeImpl();
                PresizedIdealConsumeImpl();
            }

            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            var beforeFlinq=GC.GetAllocatedBytesForCurrentThread();
            FLinqToHashSetConsumeImpl();
            var flinqBytes=GC.GetAllocatedBytesForCurrentThread()-beforeFlinq;

            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            var beforeIdeal=GC.GetAllocatedBytesForCurrentThread();
            PresizedIdealConsumeImpl();
            var idealBytes=GC.GetAllocatedBytesForCurrentThread()-beforeIdeal;

            Assert.True(flinqBytes<=idealBytes,
                $"FLinq allocated {flinqBytes}B, presized ideal allocated {idealBytes}B.");
        } finally {
            source=null;
            var directory=Path.GetDirectoryName(path);
            if(!string.IsNullOrWhiteSpace(directory)&&Directory.Exists(directory))
                Directory.Delete(directory,recursive:true);
        }
    }

    private static global::System.Collections.Generic.HashSet<int> FLinqToHashSetImpl()=>Source().ToHashSet();

    // 傍受メソッドの本文は生成コードへコピーされ、囲むクラスのスコープが失われる。
    // 静的フィールドは global:: 付きで完全修飾しないと生成側で解決できない。
    // Benchmark の `.AsX<T>()` は Set<T> を素通しする定義なので、ここでは省いても同じ形になる。
    private static void FLinqToHashSetConsumeImpl()=>
        Source().ToHashSet().Consume(global::Test.Persisted.Linq.Enumerable.FLinq.ToHashSetPresizeTests.sink);

    [LinqIntercept(false)]
    private static void PresizedIdealConsumeImpl() {
        var set=Source();
        var result=new global::System.Collections.Generic.HashSet<int>(set.Count);
        foreach(var item in set)
            result.Add(item);
        foreach(var item in result)
            sink.Consume(item);
    }

    [LinqIntercept(false)]
    private static global::System.Collections.Generic.HashSet<int> PresizedIdealImpl() {
        var set=Source();
        var result=new global::System.Collections.Generic.HashSet<int>(set.Count);
        foreach(var item in set)
            result.Add(item);
        return result;
    }

    private static global::Seamless.Collections.Persisted.Set<int> Source()=>source??throw new InvalidOperationException("Source was not initialized.");
}
