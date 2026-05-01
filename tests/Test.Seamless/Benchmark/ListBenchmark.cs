using global::Seamless.Collections;
namespace Test.Seamless.Benchmarks;

/// <summary>
/// List ベンチマークの共通土台。
/// Sequential vs RandomAccess の最適化効果を計測する。
/// </summary>
public abstract class ListBenchmark(ITestOutputHelper output):Benchmark(output) {
    /// <summary>1 スレッド = 1 List のスコープ。呼び出し側で using する。</summary>
    protected abstract Handle CreateHandle(string listPath,FileOptions accessPattern);

    protected abstract class Handle:IDisposable {
        public abstract global::Seamless.Collections.List<int> List { get; }
        public abstract void Dispose();
    }

    // ---- テンポラリディレクトリ管理 ----
    protected static string CreateTempDirectory() {
        var dir = Path.Combine(Path.GetTempPath(),$"seamless_list_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
    //static ISeamlessSet<TResult> Select<T,TResult>(this ISeamlessSet<T> source,Func<T,TResult> selector){
    //    var result=HashStore.Create().Create<TResult>();
    //    foreach(var x1 in source){
            
    //    }
    //    foreach(var a in source){

    //    }
    //}

    protected static void CleanupDirectory(string dir) {
        try {
            if(Directory.Exists(dir)) Directory.Delete(dir,recursive: true);
        }
        catch { }
    }

    // ---- 各スレッドが自身で Setup して計測部へ入る共通ドライバ ----
    private void Drive(
        int parallel,
        FileOptions accessPattern,
        long itemCount,
        Action<global::Seamless.Collections.List<int>> prefill,
        Action<global::Seamless.Collections.List<int>> measured,
        string label
    ) {
        var tempDir = CreateTempDirectory();
        var handles = new Handle?[parallel];
        try {
            this.RunMeasured(
                parallel,
                0.0, // unused for List
                0,   // unused for List
                0,   // unused for List
                0,   // unused for List
                0,   // unused for List
                itemCount,
                setup: id => {
                    var listPath = Path.Combine(tempDir,$"list_{id}.data");
                    handles[id]=this.CreateHandle(listPath,accessPattern);
                    prefill(handles[id]!.List);
                },
                measured: id => measured(handles[id]!.List),
                label: label
            );
        }
        finally {
            foreach(var h in handles) h?.Dispose();
            CleanupDirectory(tempDir);
        }
    }

    private static void PrefillNone(global::Seamless.Collections.List<int> _) { }

    private void PrefillAll(global::Seamless.Collections.List<int> list) {
        var n = Scaled;
        for(var i = 0; i<n; i++) {
            list.Add(i);
        }
    }

    // =============================================================
    // Sequential Write ベンチマーク
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void List_Add_Sequential(int parallel,double _,long __,long ___,int ____,int _____)
        => this.Drive(
            parallel,
            FileOptions.SequentialScan,
            Scaled,
            PrefillNone,
            list => {
                var n = Scaled;
                for(var i = 0; i<n; i++) {
                    list.Add(i);
                }
            },
            nameof(this.List_Add_Sequential));

    [Theory, MemberData(nameof(Matrix))]
    public void List_Add_RandomAccess(int parallel,double _,long __,long ___,int ____,int _____)
        => this.Drive(
            parallel,
            FileOptions.RandomAccess,
            Scaled,
            PrefillNone,
            list => {
                var n = Scaled;
                for(var i = 0; i<n; i++) {
                    list.Add(i);
                }
            },
            nameof(this.List_Add_RandomAccess));

    // =============================================================
    // Sequential Read ベンチマーク
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void List_Read_Sequential(int parallel,double _,long __,long ___,int ____,int _____)
        => this.Drive(
            parallel,
            FileOptions.SequentialScan,
            Scaled,
            this.PrefillAll,
            list => {
                var n = Scaled;
                long sum = 0;
                for(var i = 0; i<n; i++) {
                    sum+=list[i];
                }
                GC.KeepAlive(sum);
            },
            nameof(this.List_Read_Sequential));

    [Theory, MemberData(nameof(Matrix))]
    public void List_Read_RandomAccess(int parallel,double _,long __,long ___,int ____,int _____)
        => this.Drive(
            parallel,
            FileOptions.RandomAccess,
            Scaled,
            this.PrefillAll,
            list => {
                var n = Scaled;
                long sum = 0;
                for(var i = 0; i<n; i++) {
                    sum+=list[i];
                }
                GC.KeepAlive(sum);
            },
            nameof(this.List_Read_RandomAccess));

    // =============================================================
    // Mixed Access (Sequential)
    // =============================================================
    [Theory, MemberData(nameof(Matrix))]
    public void List_Mixed_Sequential(int parallel,double _,long __,long ___,int ____,int _____)
        => this.Drive(
            parallel,
            FileOptions.SequentialScan,
            Scaled/2,
            PrefillNone,
            list => {
                var n = Scaled/2;
                for(var i = 0; i<n; i++) {
                    list.Add(i);
                }
                long sum = 0;
                for(var i = 0; i<n; i++) {
                    sum+=list[i];
                }
                GC.KeepAlive(sum);
            },
            nameof(this.List_Mixed_Sequential));

    [Theory, MemberData(nameof(Matrix))]
    public void List_Mixed_RandomAccess(int parallel,double _,long __,long ___,int ____,int _____)
        => this.Drive(
            parallel,
            FileOptions.RandomAccess,
            Scaled/2,
            PrefillNone,
            list => {
                var n = Scaled/2;
                for(var i = 0; i<n; i++) {
                    list.Add(i);
                }
                long sum = 0;
                for(var i = 0; i<n; i++) {
                    sum+=list[i];
                }
                GC.KeepAlive(sum);
            },
            nameof(this.List_Mixed_RandomAccess));
}

/// <summary>
/// ManagedList ベンチマーク実装。
/// </summary>
[Collection("Benchmark")]
public class ManagedListBenchmark(ITestOutputHelper output):ListBenchmark(output) {
    protected override Handle CreateHandle(string listPath,FileOptions accessPattern) {
        return new ManagedListHandle(listPath,accessPattern);
    }

    private class ManagedListHandle:Handle {
        private readonly ManagedList<int> list;

        public override global::Seamless.Collections.List<int> List => this.list;

        public ManagedListHandle(string listPath,FileOptions accessPattern) {
            this.list=new ManagedList<int>(listPath,initialCapacity: 16,initialDataCapacity: 4096);
            this.list.SetTuning(new TempListTuning(FileAccess.ReadWrite,accessPattern));
        }

        public override void Dispose() {
            this.list?.Dispose();
        }
    }
}

/// <summary>
/// UnmanagedList ベンチマーク実装。
/// </summary>
[Collection("Benchmark")]
public class UnmanagedListBenchmark(ITestOutputHelper output):ListBenchmark(output) {
    protected override Handle CreateHandle(string listPath,FileOptions accessPattern) {
        return new UnmanagedListHandle(listPath,accessPattern);
    }

    private class UnmanagedListHandle:Handle {
        private readonly UnmanagedList<int> list;

        public override global::Seamless.Collections.List<int> List => this.list;

        public UnmanagedListHandle(string listPath,FileOptions accessPattern) {
            this.list=new UnmanagedList<int>(listPath,initialCapacity: 16);
            this.list.SetTuning(new TempListTuning(FileAccess.ReadWrite,accessPattern));
        }

        public override void Dispose() {
            this.list?.Dispose();
        }
    }
}
