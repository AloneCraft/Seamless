using Seamless.Collections;

namespace Test.Seamless.Benchmarks;

[Collection("Benchmark")]
public sealed class ManagedViewSetBenchmark(ITestOutputHelper output):Benchmark(output) {
    [Theory, MemberData(nameof(Matrix))]
    public void Set_Add_Sequential(int parallel,double _,long __,long ___,int ____,int _____) {
        ConsumeMatrixParameters(_,__,___,____,_____);
        this.Drive(parallel,FileOptions.SequentialScan,
            prefill: _ => { },
            measured: set => {
                var n = Scaled;
                for(var i = 0; i<n; i++)
                    set.Add("value-"+i);
            },
            nameof(this.Set_Add_Sequential));
    }

    [Theory, MemberData(nameof(Matrix))]
    public void Set_Distinct(int parallel,double _,long __,long ___,int ____,int _____) {
        ConsumeMatrixParameters(_,__,___,____,_____);
        this.Drive(parallel,FileOptions.RandomAccess,
            prefill: set => {
                var n = Scaled;
                for(var i = 0; i<n; i++)
                    set.Add("value-"+(i%32));
            },
            measured: set => set.Distinct(),
            nameof(this.Set_Distinct));
    }

    [Theory, MemberData(nameof(Matrix))]
    public void Set_Read_Sequential(int parallel,double _,long __,long ___,int ____,int _____) {
        ConsumeMatrixParameters(_,__,___,____,_____);
        this.Drive(parallel,FileOptions.SequentialScan,
            prefill: set => {
                var n = Scaled;
                for(var i = 0; i<n; i++)
                    set.Add("value-"+i);
                set.Distinct();
            },
            measured: set => {
                long total = 0;
                for(var i = 0; i<set.Count; i++)
                    total+=set[i].Length;
                GC.KeepAlive(total);
            },
            nameof(this.Set_Read_Sequential));
    }

    private void Drive(int parallel,FileOptions accessPattern,Action<global::Seamless.Collections.Set<string>> prefill,Action<global::Seamless.Collections.Set<string>> measured,string label) {
        var tempDir = CreateTempDirectory();
        var sets = new global::Seamless.Collections.Set<string>?[parallel];
        try {
            this.RunMeasured(parallel,0,0,0,0,0,Scaled,
                setup: id => {
                    var path = Path.Combine(tempDir,$"managed_set_{id}.dat");
                    var set = new global::Seamless.Collections.Set<string>(path,initialCapacity: 16,initialDataCapacity: 4096);
                    set.SetTuning(new TempListTuning(FileAccess.ReadWrite,accessPattern));
                    sets[id]=set;
                    prefill(set);
                },
                measured: id => measured(sets[id]!),
                label: label);
        }
        finally {
            foreach(var set in sets)
                set?.Dispose();
            CleanupDirectory(tempDir);
        }
    }

    private static string CreateTempDirectory() {
        var dir = Path.Combine(Path.GetTempPath(),$"seamless_managed_set_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanupDirectory(string dir) {
        try {
            if(Directory.Exists(dir))
                Directory.Delete(dir,recursive: true);
        }
        catch {
        }
    }

    private static void ConsumeMatrixParameters(double _,long __,long ___,int ____,int _____)
        => GC.KeepAlive((_,__,___,____,_____));
}

[Collection("Benchmark")]
public sealed class UnmanagedViewSetBenchmark(ITestOutputHelper output):Benchmark(output) {
    [Theory, MemberData(nameof(Matrix))]
    public void Set_Add_Sequential(int parallel,double _,long __,long ___,int ____,int _____) {
        ConsumeMatrixParameters(_,__,___,____,_____);
        this.Drive(parallel,FileOptions.SequentialScan,
            prefill: _ => { },
            measured: set => {
                var n = Scaled;
                for(var i = 0; i<n; i++)
                    set.Add(i);
            },
            nameof(this.Set_Add_Sequential));
    }

    [Theory, MemberData(nameof(Matrix))]
    public void Set_Distinct(int parallel,double _,long __,long ___,int ____,int _____) {
        ConsumeMatrixParameters(_,__,___,____,_____);
        this.Drive(parallel,FileOptions.RandomAccess,
            prefill: set => {
                var n = Scaled;
                for(var i = 0; i<n; i++)
                    set.Add(i%32);
            },
            measured: set => set.Distinct(),
            nameof(this.Set_Distinct));
    }

    [Theory, MemberData(nameof(Matrix))]
    public void Set_Read_Sequential(int parallel,double _,long __,long ___,int ____,int _____) {
        ConsumeMatrixParameters(_,__,___,____,_____);
        this.Drive(parallel,FileOptions.SequentialScan,
            prefill: set => {
                var n = Scaled;
                for(var i = 0; i<n; i++)
                    set.Add(i);
                set.Distinct();
            },
            measured: set => {
                long total = 0;
                for(var i = 0; i<set.Count; i++)
                    total+=set[i];
                GC.KeepAlive(total);
            },
            nameof(this.Set_Read_Sequential));
    }

    private void Drive(int parallel,FileOptions accessPattern,Action<global::Seamless.Collections.Set<int>> prefill,Action<global::Seamless.Collections.Set<int>> measured,string label) {
        var tempDir = CreateTempDirectory();
        var sets = new global::Seamless.Collections.Set<int>?[parallel];
        try {
            this.RunMeasured(parallel,0,0,0,0,0,Scaled,
                setup: id => {
                    var path = Path.Combine(tempDir,$"unmanaged_set_{id}.dat");
                    var set = new global::Seamless.Collections.Set<int>(path,initialCapacity: 16);
                    set.SetTuning(new TempListTuning(FileAccess.ReadWrite,accessPattern));
                    sets[id]=set;
                    prefill(set);
                },
                measured: id => measured(sets[id]!),
                label: label);
        }
        finally {
            foreach(var set in sets)
                set?.Dispose();
            CleanupDirectory(tempDir);
        }
    }

    private static string CreateTempDirectory() {
        var dir = Path.Combine(Path.GetTempPath(),$"seamless_unmanaged_set_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanupDirectory(string dir) {
        try {
            if(Directory.Exists(dir))
                Directory.Delete(dir,recursive: true);
        }
        catch {
        }
    }

    private static void ConsumeMatrixParameters(double _,long __,long ___,int ____,int _____)
        => GC.KeepAlive((_,__,___,____,_____));
}
