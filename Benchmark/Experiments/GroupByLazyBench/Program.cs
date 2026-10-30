// GroupBy の集約を、First / Take(k) の前でどう実行するかの比較(2026-10-23)。
//   S: System.Linq
//   F: 融合(1 回の走査で全グループを集約。評価回数が System.Linq より多い。比較の基準としてだけ置く)
//   L: 遅延集約(1 回目の走査でグループ番号だけを記録 → 2 回目の走査で、先頭の k グループの分だけ集約)
//   LP: Lazy の ArrayPool 版
//   Limit: 生成器に入れた形(1 回の走査。先頭の k グループが決まったら新しいグループを作らず、k グループの外の要素は集約しない)
// 源の型(値型・参照型・大きい構造体)、キーの型(int・string・Guid)、件数、グループ数を変えて、L が S より速いかを見る。
// ハードや .NET の版が変わると結果が変わりうるので、環境の情報を先頭に書く。次の版・別のマシンで同じ表を取り直して比べる。
// 使い方: dotnet run -c Release -- 出力ファイル
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace GroupByLazyBench;

public sealed record Rec(int Id,string Name);

public readonly struct Big(long id,long b,long c,long d) {
    public readonly long Id=id;
    public readonly long B=b;
    public readonly long C=c;
    public readonly long D=d;
}

public interface IElem<T> {
    static abstract int Id(T item);
}

public interface IKeyMap<K> where K : notnull {
    static abstract K Make(int id);
    static abstract void Prepare(int groups);
}

public readonly struct IntElem:IElem<int> { public static int Id(int item) => item; }
public readonly struct RecElem:IElem<Rec> { public static int Id(Rec item) => item.Id; }
public readonly struct BigElem:IElem<Big> { public static int Id(Big item) => (int)item.Id; }

public readonly struct IntKey:IKeyMap<int> {
    private static int groups=1;
    public static void Prepare(int g) => groups=g;
    public static int Make(int id) => id%groups;
}

public readonly struct StringKey:IKeyMap<string> {
    private static string[] table=[];
    public static void Prepare(int g) => table=Enumerable.Range(0,g).Select(static i => "key-"+i.ToString("D8")).ToArray();
    public static string Make(int id) => table[id%table.Length];
}

public readonly struct GuidKey:IKeyMap<Guid> {
    private static Guid[] table=[];
    public static void Prepare(int g) => table=Enumerable.Range(0,g).Select(static i => new Guid(i,0,0,[1,2,3,4,5,6,7,8])).ToArray();
    public static Guid Make(int id) => table[id%table.Length];
}

public static class Strategies {
    public static long System<T,K,TE,TK>(T[] source,int take) where K : notnull where TE : struct, IElem<T> where TK : struct, IKeyMap<K> {
        var groups = source.GroupBy(static item => TK.Make(TE.Id(item)))
            .Select(static g => g.Count()*31L+g.Sum(static item => (long)TE.Id(item)));
        return take==1 ? groups.First() : groups.Take(take).Sum();
    }

    // 融合: 全グループを 1 回の走査で集約する。
    public static long Fused<T,K,TE,TK>(T[] source,int take) where K : notnull where TE : struct, IElem<T> where TK : struct, IKeyMap<K> {
        var map = new Dictionary<K,int>();
        var counts = new List<long>();
        var sums = new List<long>();
        foreach(var item in source) {
            var key = TK.Make(TE.Id(item));
            ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(map,key,out var exists);
            if(!exists) {
                slot=counts.Count;
                counts.Add(0);
                sums.Add(0);
            }
            var index = slot;
            CollectionsMarshal.AsSpan(counts)[index]++;
            CollectionsMarshal.AsSpan(sums)[index]+=TE.Id(item);
        }
        long total = 0;
        var limit = Math.Min(take,counts.Count);
        for(var i = 0;i<limit;i++) {
            total+=counts[i]*31L+sums[i];
        }
        return total;
    }

    // 生成器が出す形: 1 回の走査。グループの数が take に達したら新しいグループを作らず、既にあるグループの要素だけを集約する。
    public static long Limit<T,K,TE,TK>(T[] source,int take) where K : notnull where TE : struct, IElem<T> where TK : struct, IKeyMap<K> {
        var map = new Dictionary<K,int>();
        var counts = new long[take];
        var sums = new long[take];
        foreach(var item in source) {
            var key = TK.Make(TE.Id(item));
            ref var slot = ref CollectionsMarshal.GetValueRefOrNullRef(map,key);
            if(Unsafe.IsNullRef(ref slot)) {
                if(map.Count>=take) { continue; }
                map.Add(key,map.Count);
                slot=ref CollectionsMarshal.GetValueRefOrNullRef(map,key);
            }
            counts[slot]++;
            sums[slot]+=TE.Id(item);
        }
        long total = 0;
        for(var i = 0;i<map.Count;i++) {
            total+=counts[i]*31L+sums[i];
        }
        return total;
    }

    // 遅延集約: 1 回目はグループ番号だけを記録し、2 回目は先頭の take グループの要素だけを集約する。
    public static long Lazy<T,K,TE,TK>(T[] source,int take) where K : notnull where TE : struct, IElem<T> where TK : struct, IKeyMap<K> {
        var map = new Dictionary<K,int>();
        var ids = new int[source.Length];
        for(var i = 0;i<source.Length;i++) {
            var key = TK.Make(TE.Id(source[i]));
            ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(map,key,out var exists);
            if(!exists) {
                slot=map.Count-1;
            }
            ids[i]=slot;
        }
        var limit = Math.Min(take,map.Count);
        var counts = new long[limit];
        var sums = new long[limit];
        for(var i = 0;i<ids.Length;i++) {
            var id = ids[i];
            if((uint)id<(uint)limit) {
                counts[id]++;
                sums[id]+=TE.Id(source[i]);
            }
        }
        long total = 0;
        for(var i = 0;i<limit;i++) {
            total+=counts[i]*31L+sums[i];
        }
        return total;
    }
}

public static class PooledStrategies {
    // 遅延集約で、グループ番号の配列を ArrayPool から借りる(大きい配列を毎回確保して LOH と GC を増やさない)。
    public static long LazyPooled<T,K,TE,TK>(T[] source,int take) where K : notnull where TE : struct, IElem<T> where TK : struct, IKeyMap<K> {
        var map = new Dictionary<K,int>();
        var ids = System.Buffers.ArrayPool<int>.Shared.Rent(source.Length);
        try {
            for(var i = 0;i<source.Length;i++) {
                var key = TK.Make(TE.Id(source[i]));
                ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(map,key,out var exists);
                if(!exists) {
                    slot=map.Count-1;
                }
                ids[i]=slot;
            }
            var limit = Math.Min(take,map.Count);
            var counts = new long[limit];
            var sums = new long[limit];
            for(var i = 0;i<source.Length;i++) {
                var id = ids[i];
                if((uint)id<(uint)limit) {
                    counts[id]++;
                    sums[id]+=TE.Id(source[i]);
                }
            }
            long total = 0;
            for(var i = 0;i<limit;i++) {
                total+=counts[i]*31L+sums[i];
            }
            return total;
        } finally {
            System.Buffers.ArrayPool<int>.Shared.Return(ids);
        }
    }
}

public static class Program {
    private static long Allocated(Func<long> run) {
        run();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for(var i = 0;i<20;i++) { run(); }
        return (GC.GetAllocatedBytesForCurrentThread()-before)/20;
    }

    private static double Measure(Func<long> run) {
        long sink = 0;
        for(var i = 0;i<10;i++) { sink+=run(); }
        var probe = Stopwatch.StartNew();
        sink+=run();
        var oneMicroseconds = Math.Max(0.5,probe.Elapsed.TotalMilliseconds*1000);
        var reps = (int)Math.Clamp(3000/oneMicroseconds,1,2000);
        var samples = new double[25];
        for(var s = 0;s<samples.Length;s++) {
            var sw = Stopwatch.StartNew();
            for(var r = 0;r<reps;r++) { sink+=run(); }
            samples[s]=sw.Elapsed.TotalMilliseconds*1000/reps;
        }
        GC.KeepAlive(sink);
        Array.Sort(samples);
        return samples[0];
    }

    private static void Cell<T,K,TE,TK>(StringBuilder report,string sourceName,string keyName,T[] source,int n,int groups) where K : notnull where TE : struct, IElem<T> where TK : struct, IKeyMap<K> {
        TK.Prepare(groups);
        foreach(var take in new[] { 1,5,groups/2 }) {
            if(take<1) { continue; }
            var expected = Strategies.System<T,K,TE,TK>(source,take);
            if(Strategies.Fused<T,K,TE,TK>(source,take)!=expected||Strategies.Lazy<T,K,TE,TK>(source,take)!=expected||Strategies.Limit<T,K,TE,TK>(source,take)!=expected) {
                report.AppendLine($"MISMATCH {sourceName}/{keyName} n={n} groups={groups} take={take}");
                continue;
            }
            var s = Measure(() => Strategies.System<T,K,TE,TK>(source,take));
            var f = Measure(() => Strategies.Fused<T,K,TE,TK>(source,take));
            var l = Measure(() => Strategies.Lazy<T,K,TE,TK>(source,take));
            var lp = Measure(() => PooledStrategies.LazyPooled<T,K,TE,TK>(source,take));
            var li = Measure(() => Strategies.Limit<T,K,TE,TK>(source,take));
            var aS = Allocated(() => Strategies.System<T,K,TE,TK>(source,take));
            var aF = Allocated(() => Strategies.Fused<T,K,TE,TK>(source,take));
            var aL = Allocated(() => PooledStrategies.LazyPooled<T,K,TE,TK>(source,take));
            report.AppendLine($"{sourceName,-5} {keyName,-7} n={n,-8} groups={groups,-5} take={take,-5} System={s,9:F1} Fused={f,9:F1} Lazy={l,9:F1} LazyPool={lp,9:F1} Limit={li,9:F1} us  LazyPool/System={lp/s,5:F2} Limit/System={li/s,5:F2} Limit/Fused={li/f,5:F2}  alloc(KB) S={aS/1024,6} F={aF/1024,6} LP={aL/1024,6}");
        }
    }

    private static void Keys<T,TE>(StringBuilder report,string sourceName,T[] source,int n,int groups) where TE : struct, IElem<T> {
        Cell<T,int,TE,IntKey>(report,sourceName,"int",source,n,groups);
        Cell<T,string,TE,StringKey>(report,sourceName,"string",source,n,groups);
        Cell<T,Guid,TE,GuidKey>(report,sourceName,"Guid",source,n,groups);
    }

    public static void Main(string[] args) {
        var report = new StringBuilder();
        report.AppendLine($"# GroupBy の First/Take(k) の遅延集約の比較(Limit = 生成器に入れた形) {DateTime.Now:yyyy-MM-dd HH:mm}");
        report.AppendLine($"# .NET {Environment.Version} / {RuntimeInformation.OSDescription} / {RuntimeInformation.ProcessArchitecture} / 論理コア {Environment.ProcessorCount}");
        report.AppendLine($"# CPU: {Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")}");
        foreach(var n in new[] { 1_000,100_000,1_000_000 }) {
            foreach(var groups in new[] { 10,1000 }) {
                if(groups>n/2) { continue; }
                if(n==1_000_000&&groups==10) { continue; }
                Keys<int,IntElem>(report,"int",Enumerable.Range(0,n).ToArray(),n,groups);
                Keys<Rec,RecElem>(report,"Rec",Enumerable.Range(0,n).Select(static i => new Rec(i,"n"+i)).ToArray(),n,groups);
                Keys<Big,BigElem>(report,"Big32",Enumerable.Range(0,n).Select(static i => new Big(i,i,i,i)).ToArray(),n,groups);
            }
        }
        var path = args.Length>0 ? args[0] : "groupby-lazy-result.txt";
        File.WriteAllText(path,report.ToString(),Encoding.UTF8);
        Console.WriteLine(report);
    }
}
