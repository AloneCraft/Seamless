using Shared.Linq;

namespace Test.Persisted.Linq.Enumerable.IEnumerable.FLinq;

public sealed class IEnumerableRootTests {
    private static global::Seamless.Collections.Persisted.Set<int>? source;
    private static global::Seamless.Collections.Persisted.Set<ClassInt32>? classSource;

    [Fact,Trait("Category","Unit")]
    public void PersistedSetRoute_UsesIEnumerableRootWithoutNativeIterateState() {
        var path=Path.Combine(Path.GetTempPath(),"Seamless-IEnumerableRoot-"+Guid.NewGuid().ToString("N"),"items.dat");
        try {
            using var set=global::Seamless.Collections.Persisted.Store.CreateSet<int>(path,InitialCapacity:1,InitialDataCapacity:128);
            foreach(var value in new[]{1,2,3,4})
                set.Add(value);
            set.CompactSave();
            source=set;

            Assert.Equal(2,CountEvenImpl());
            var generated=GeneratedFor(nameof(CountEvenImpl));
            Assert.DoesNotContain("PersistedSetIterateState",generated);
            Assert.DoesNotContain("Seamless.Collections.Iteration.IStrategy",generated);
            Assert.DoesNotContain(".Iterate(ref __PersistedSetIterateState",generated);
        } finally {
            source=null;
            var directory=Path.GetDirectoryName(path);
            if(!string.IsNullOrWhiteSpace(directory)&&Directory.Exists(directory))
                Directory.Delete(directory,recursive:true);
        }
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int CountEvenImpl()=>Source().AsX().Count(static value=>value%2==0);

    // Benchmark.db の実測(実行ID=7)では Persisted<T>/Tsavorite<T> の Contains_value が
    // FLinq 対 System.Linq で最大 142x のメモリ差・24x の時間差を示したが、同じ実行で
    // Contains_value 自体が BenchmarkDotNet の "takes too long to run" で3件失敗してもいる。
    // このテストは同一の Set<int> に対する Contains を隔離して比較する(GC.GetAllocatedBytesForCurrentThread
    // を Func<T> でラップして呼ぶと割当が計測から消えることがあるため、必ず直接呼び出しで測る)。
    // 結果: 4パターン全てで FLinq/raw の割当バイト数は一致(生成コード側に問題は無い、較正アーティファクトと判断)。
    [Fact,Trait("Category","Unit")]
    public void Contains_AllocatesNoMoreThanRawSystemLinq() {
        var path=Path.Combine(Path.GetTempPath(),"Seamless-ContainsProbe-"+Guid.NewGuid().ToString("N"),"items.dat");
        try {
            using var set=global::Seamless.Collections.Persisted.Store.CreateSet<int>(path,InitialCapacity:1,InitialDataCapacity:4096);
            const int count=2000;
            for(var i=1;i<=count;i++)
                set.Add(i);
            set.CompactSave();
            source=set;

            for(var i=0;i<5;i++) {
                _=FLinqContainsImpl(1);
                _=RawContainsImpl(1);
            }

            foreach(var target in new[]{1,count/2,count,count+1}) {
                GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                var beforeFlinq=GC.GetAllocatedBytesForCurrentThread();
                var flinqResult=FLinqContainsImpl(target);
                var flinqBytes=GC.GetAllocatedBytesForCurrentThread()-beforeFlinq;

                GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                var beforeRaw=GC.GetAllocatedBytesForCurrentThread();
                var rawResult=RawContainsImpl(target);
                var rawBytes=GC.GetAllocatedBytesForCurrentThread()-beforeRaw;

                Assert.Equal(rawResult,flinqResult);
                Assert.True(flinqBytes<=rawBytes,
                    $"target={target}: FLinq allocated {flinqBytes}B, raw System.Linq allocated {rawBytes}B.");
            }
        } finally {
            source=null;
            var directory=Path.GetDirectoryName(path);
            if(!string.IsNullOrWhiteSpace(directory)&&Directory.Exists(directory))
                Directory.Delete(directory,recursive:true);
        }
    }

    private static bool FLinqContainsImpl(int value)=>Source().AsX().Contains(value);
    [LinqIntercept(false)]
    private static bool RawContainsImpl(int value)=>Source().AsX().Contains(value);

    // Seamless.Collections.Persisted.Set<T> は System.Collections.Generic.ICollection<T> を
    // 実装しないため(Seamless.Collections.ISet<T> 経由で BCL の IReadOnlySet<T> のみ実装)、
    // 型消去された IEnumerable<T> ソースに対する Contains の汎用コレクション判定
    // (RenderGenericCollectionContainsInline)は ICollection<T> の次に IReadOnlySet<T> も
    // 判定しないと、O(1) の Set<T>.Contains(T) へ到達できず O(n) の手動走査へ落ちる。
    // このテストは生成コードに IReadOnlySet 判定が実際に出力されていることを確認する。
    [Fact,Trait("Category","Unit")]
    public void Contains_UsesIReadOnlySetFastPath() {
        var path=Path.Combine(Path.GetTempPath(),"Seamless-ContainsReadOnlySetProbe-"+Guid.NewGuid().ToString("N"),"items.dat");
        try {
            using var set=global::Seamless.Collections.Persisted.Store.CreateSet<int>(path,InitialCapacity:1,InitialDataCapacity:4096);
            const int count=64;
            for(var i=1;i<=count;i++)
                set.Add(i);
            source=set;

            Assert.True(FLinqContainsImpl(count/2));
            Assert.False(FLinqContainsImpl(count+1));
            var generated=GeneratedFor(nameof(FLinqContainsImpl));
            Assert.Contains("IReadOnlySet",generated,StringComparison.Ordinal);
        } finally {
            source=null;
            var directory=Path.GetDirectoryName(path);
            if(!string.IsNullOrWhiteSpace(directory)&&Directory.Exists(directory))
                Directory.Delete(directory,recursive:true);
        }
    }

    // Benchmark.db の実測(実行ID=7)では Persisted<int>/<ClassInt32> の Count() が 39〜4780x という
    // 異常値を示した。生成コードは TryGetNext(out _) 経由で値を捨てるにもかかわらず必ず Current を読んでいた
    // (Persisted の Current は list[index] 経由で GetItem を呼び、参照型は逆シリアライズで実コストがある)。
    // このテストで直接呼び出し(GC.GetAllocatedBytesForCurrentThread を Func<T> でラップせず素の呼び出しで
    // 前後を挟む)で計測したところ実際に再現した(ClassInt32・4096要素で約229KB、raw は約0)。
    // Scalar.cs の RenderDirectEnumerableCountInline(型消去済み IEnumerable<T> ソース向けに
    // TryGetNonEnumeratedCount → 駄目なら MoveNext だけのループへ inline、System.Linq.Enumerable.Count()
    // 自身と同じアルゴリズムを Current を読まずに FLinq 自身の生成コードとして再現したもの)で解消した。
    [Fact,Trait("Category","Unit")]
    public void Count_AllocatesNoMoreThanRawSystemLinq() {
        foreach(var compact in new[]{false,true}) {
            var path=Path.Combine(Path.GetTempPath(),"Seamless-CountProbe-"+Guid.NewGuid().ToString("N"),"items.dat");
            try {
                using var set=global::Seamless.Collections.Persisted.Store.CreateSet<int>(path,InitialCapacity:1,InitialDataCapacity:4096);
                const int count=2000;
                for(var i=1;i<=count;i++)
                    set.Add(i);
                if(compact)
                    set.CompactSave();
                source=set;

                for(var i=0;i<5;i++) {
                    _=FLinqCountImpl();
                    _=RawCountImpl();
                }

                GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                var beforeFlinq=GC.GetAllocatedBytesForCurrentThread();
                var flinqResult=FLinqCountImpl();
                var flinqBytes=GC.GetAllocatedBytesForCurrentThread()-beforeFlinq;

                GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                var beforeRaw=GC.GetAllocatedBytesForCurrentThread();
                var rawResult=RawCountImpl();
                var rawBytes=GC.GetAllocatedBytesForCurrentThread()-beforeRaw;

                Assert.Equal(rawResult,flinqResult);
                Assert.True(flinqBytes<=rawBytes,
                    $"int compact={compact}: FLinq allocated {flinqBytes}B, raw System.Linq allocated {rawBytes}B.");
            } finally {
                source=null;
                var directory=Path.GetDirectoryName(path);
                if(!string.IsNullOrWhiteSpace(directory)&&Directory.Exists(directory))
                    Directory.Delete(directory,recursive:true);
            }
        }
        foreach(var compact in new[]{false,true}) {
            var path=Path.Combine(Path.GetTempPath(),"Seamless-CountProbeClass-"+Guid.NewGuid().ToString("N"),"items.dat");
            try {
                using var set=global::Seamless.Collections.Persisted.Store.CreateSet<ClassInt32>(path,InitialCapacity:1,InitialDataCapacity:4096);
                const int count=2000;
                for(var i=1;i<=count;i++)
                    set.Add(new ClassInt32{Value=i});
                if(compact)
                    set.CompactSave();
                classSource=set;

                for(var i=0;i<5;i++) {
                    _=FLinqCountClassImpl();
                    _=RawCountClassImpl();
                }

                GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                var beforeFlinq=GC.GetAllocatedBytesForCurrentThread();
                var flinqResult=FLinqCountClassImpl();
                var flinqBytes=GC.GetAllocatedBytesForCurrentThread()-beforeFlinq;

                GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                var beforeRaw=GC.GetAllocatedBytesForCurrentThread();
                var rawResult=RawCountClassImpl();
                var rawBytes=GC.GetAllocatedBytesForCurrentThread()-beforeRaw;

                Assert.Equal(rawResult,flinqResult);
                Assert.True(flinqBytes<=rawBytes,
                    $"ClassInt32 compact={compact}: FLinq allocated {flinqBytes}B, raw System.Linq allocated {rawBytes}B.");
            } finally {
                classSource=null;
                var directory=Path.GetDirectoryName(path);
                if(!string.IsNullOrWhiteSpace(directory)&&Directory.Exists(directory))
                    Directory.Delete(directory,recursive:true);
            }
        }
    }

    private static int FLinqCountImpl()=>Source().AsX().Count();
    [LinqIntercept(false)]
    private static int RawCountImpl()=>Source().AsX().Count();
    private static int FLinqCountClassImpl()=>ClassSource().AsX().Count();
    [LinqIntercept(false)]
    private static int RawCountClassImpl()=>ClassSource().AsX().Count();

    private static global::Seamless.Collections.Persisted.Set<int> Source()=>source??throw new InvalidOperationException("Source was not initialized.");
    private static global::Seamless.Collections.Persisted.Set<ClassInt32> ClassSource()=>classSource??throw new InvalidOperationException("classSource was not initialized.");
    private static string GeneratedFor(string methodName) {
        // 出力先 bin/<構成>/<TFM>/ から、実行中の構成と TFM の obj/<構成>/<TFM>/generated を求める(構成を固定しない)。
        var output=new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        var configuration=output.Parent!;
        var project=configuration.Parent!.Parent!.FullName;
        var root=Path.Combine(project,"obj",configuration.Name,output.Name,"generated");
        foreach(var file in Directory.EnumerateFiles(root,"*.g.cs",SearchOption.AllDirectories)) {
            var source=File.ReadAllText(file);
            if(source.Contains("class IEnumerableRootTests",StringComparison.Ordinal)
                &&source.Contains("<c>"+methodName+"()</c>",StringComparison.Ordinal)) {
                return source;
            }
        }
        throw new InvalidOperationException("Generated source was not found.");
    }
}
