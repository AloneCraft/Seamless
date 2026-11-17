using System.Runtime.InteropServices;

namespace Benchmark.Dynamic.Worker;

public static class Program{
    public static Task<int> Main(string[] args){
        Console.OutputEncoding=System.Text.Encoding.UTF8;
        if(args.Length>0&&args[0]=="scalable"){
            return ScalableCommand.RunAsync(args[1..]);
        }
        if(args.Length>0&&args[0]=="parameter-sweep"){
            return ParameterSweepCommand.RunAsync(args[1..]);
        }
        if(args.Length>0&&args[0]=="--regressions"){
            return Task.FromResult(Regressions(args));
        }
        if(args.Length>0&&args[0]=="--check-all-fixed"){
            return Task.FromResult(CheckAllFixed());
        }
        if(args.Length>0&&args[0]=="--dump-expanded"){
            var caseId=args.Length>1?args[1]:"chain-21";
            var item=Benchmark.Dynamic.Core.FixedCaseCatalog.All.FirstOrDefault(c=>c.ID==caseId);
            if(item is null){
                var rItem=Benchmark.Dynamic.Core.Realistic.RealisticCaseCatalog.All.FirstOrDefault(c=>c.ID==caseId);
                if(rItem is null){Console.Error.WriteLine("Case not found: "+caseId);return Task.FromResult(1);}
                var rSource=Benchmark.Dynamic.Core.Realistic.RealisticCaseCatalog.Emit(rItem,Benchmark.Dynamic.Core.Realistic.RealisticSourceKind.Array,15);
                var rCompiled=new DynamicQueryCompiler().Compile(new("dump","Benchmark.Dynamic.Generated.RealisticQuery",rSource,"dump.cs"));
                Console.WriteLine(rCompiled.ExpandedSource);
                return Task.FromResult(0);
            }
            var source=Benchmark.Dynamic.Core.FixedCaseCatalog.Emit(item,15);
            var compiled=new DynamicQueryCompiler().Compile(new("dump","Benchmark.Dynamic.Generated.FixedQuery",source,"dump.cs"));
            Console.WriteLine(compiled.ExpandedSource);
            return Task.FromResult(0);
        }
        if(args.Length>0&&args[0]=="--fixed"){
            return Task.FromResult(Fixed(args));
        }
        if(args.Length>0&&args[0]=="--realistic"){
            return Task.FromResult(Realistic(args));
        }
        if(args.Length>0&&args[0]=="--measure-source"){
            return Task.FromResult(MeasureSource(args));
        }
        論理CPUへピン留め(args);
        var Host=new WorkerHost(Console.In,Console.Out,Console.Error);
        return Host.RunAsync();
    }
    private static int Regressions(string[] args){
        RegressionRunStore? store=null;
        try{
            string? Read(string name){var index=Array.IndexOf(args,name);return index<0?null:args[index+1];}
            var id=Read("--case");
            var prefix=Read("--prefix");
            if(!args.Contains("--list")&&Read("--database") is{} database){store=new(database,Read("--results")??throw new ArgumentException("--results is required with --database."),id);}
            using(var suite=RegressionSuite.Compile()){
            store?.RecordCompilation(suite.ExpandedSource);
            if(args.Contains("--list")){
                foreach(var item in suite.Cases){Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(item));}
                return 0;
            }
            var selected=suite.Cases.Where(item=>(id is null||item.ID==id)&&(prefix is null||item.ID.StartsWith(prefix,StringComparison.Ordinal))).ToArray();
            if(selected.Length==0){throw new ArgumentException("Unknown regression case: "+id);}
            var failed=false;
            foreach(var item in selected){
                var result=suite.Run(item.ID);store?.Record(result);Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));failed|=result.Status!="Passed";
            }
            if(failed){return 1;}
            }
            store?.Complete(true);return 0;
        }catch(Exception exception){Console.Error.WriteLine(exception);return 1;}
        finally{store?.Dispose();}
    }
    private static int Realistic(string[] args){
        try{
            string Read(string name,string fallback){var index=Array.IndexOf(args,name);if(index<0){return fallback;}return args[index+1];}
            var database=Read("--database",Benchmark.Infrastructure.BenchmarkDatabase.Resolve(Environment.CurrentDirectory));
            var sourceKindStr=Read("--source-kind","Array");
            var sourceKind=Enum.Parse<Benchmark.Dynamic.Core.Realistic.RealisticSourceKind>(sourceKindStr,true);
            var distStr=Read("--distribution","UniformWithDuplicates");
            var dist=Enum.Parse<Benchmark.Dynamic.Core.Realistic.RealisticDistribution>(distStr,true);
            var result=new RealisticCaseRunner(new(new(database))).Run(
                sourceKind,
                int.Parse(Read("--count","256")),
                int.Parse(Read("--seed","42")),
                int.Parse(Read("--flags","15")),
                Read("--case","*")=="*"?null:Read("--case","*"),
                dist);
            foreach(var item in result){Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(item));}
            return result.All(r=>r.Passed)?0:1;
        }catch(Exception exception){Console.Error.WriteLine(exception);return 1;}
    }

    private static int Fixed(string[] args){
        try{
            string Read(string name,string fallback){var index=Array.IndexOf(args,name);if(index<0){return fallback;}return args[index+1];}
            var database=Read("--database",Benchmark.Infrastructure.BenchmarkDatabase.Resolve(Environment.CurrentDirectory));
            var result=new FixedCaseRunner(new(new(database))).Run(int.Parse(Read("--count","256")),int.Parse(Read("--seed","42")),int.Parse(Read("--flags","15")),Read("--case","*")=="*"?null:Read("--case","*"));
            foreach(var item in result){Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{item.ID,item.RunID,item.Conditions.WorkloadID,item.Conditions.Implementation,item.MedianNanoseconds,item.MedianAllocatedBytes}));}return 0;
        }catch(Exception exception){Console.Error.WriteLine(exception);return 1;}
    }


    private static int MeasureSource(string[] args){
        try{
            string Read(string name,string fallback){var index=Array.IndexOf(args,name);if(index<0){return fallback;}return args[index+1];}
            var sourcePath=Read("--source",null!);
            if(string.IsNullOrEmpty(sourcePath)||!File.Exists(sourcePath)){Console.Error.WriteLine("Source file not found: "+sourcePath);return 1;}
            var sourceText=File.ReadAllText(sourcePath);
            var elementTypeName=Read("--element-type","Int");
            var targetName=Read("--target","SystemArray");
            var count=int.Parse(Read("--count","256"));
            var seed=int.Parse(Read("--seed","42"));

            var target=Enum.Parse<Benchmark.Dynamic.Core.SourceTarget>(targetName);
            var elementType=Enum.Parse<Benchmark.Dynamic.Core.QueryElementType>(elementTypeName);

            var compiler=new DynamicQueryCompiler();
            var compiled=compiler.Compile(new("probe-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.ProbeQuery",sourceText,"probe.cs"));
            if(args.Contains("--dump")){
                Console.WriteLine("=== EXPANDED SOURCE ===");
                Console.WriteLine(compiled.ExpandedSource);
                Console.WriteLine("=======================");
            }
            if(!compiled.Success){
                Console.Error.WriteLine("Compilation failed:\n"+string.Join("\n",compiled.Diagnostics.Where(d=>d.Severity=="Error").Select(d=>$"{d.Location}: {d.Message}")));
                return 1;
            }
            var loaded=compiled.LoadedQuery!;
            try{
                using var fixture=new SourceFixtureFactory().Create(target,count,Benchmark.Dynamic.Core.Seed256.FromUInt64((ulong)seed),elementType);
                var measured=new MeasurementEngine().Measure(loaded,fixture.Primary,fixture.Secondary,0,0);
                foreach(var m in measured.Measurements){
                    Console.WriteLine($"{m.Implementation}: {m.MedianNanoseconds:F2} ns (alloc: {m.AllocatedBytesPerOperation:F1} bytes)");
                }
                var flinq=measured.Measurements.FirstOrDefault(m=>m.Implementation==DynamicImplementationKind.FLinq);
                var sys=measured.Measurements.FirstOrDefault(m=>m.Implementation==DynamicImplementationKind.System);
                if(flinq is not null&&sys is not null&&flinq.MedianNanoseconds>0){
                    var ratio=sys.MedianNanoseconds/flinq.MedianNanoseconds;
                    var diff=flinq.MedianNanoseconds-sys.MedianNanoseconds;
                    Console.WriteLine($"RESULT: ratio={ratio:F2}x, diff={diff:+0.00;-0.00} ns, FLinq={flinq.MedianNanoseconds:F2} ns, System={sys.MedianNanoseconds:F2} ns");
                }
                return 0;
            }finally{
                loaded.UnloadAndVerify();
            }
        }catch(Exception ex){
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void 論理CPUへピン留め(string[] args){
        var 論理CPU=引数から論理CPUを読む(args);
        if(論理CPU is null||論理CPU<0||論理CPU>=Environment.ProcessorCount||論理CPU>=63){
            return;
        }

        var マスク=1L<<論理CPU.Value;
        if(!OperatingSystem.IsWindows()){
            return;
        }

        try{
            System.Diagnostics.Process.GetCurrentProcess().ProcessorAffinity=new IntPtr(マスク);
        }catch(PlatformNotSupportedException){
        }catch(InvalidOperationException){
        }catch(System.ComponentModel.Win32Exception){
        }

        SetThreadAffinityMask(GetCurrentThread(),new UIntPtr((ulong)マスク));
    }

    private static int? 引数から論理CPUを読む(string[] args){
        for(var 番号=0;番号<args.Length-1;番号++){
            if(string.Equals(args[番号],"--logical-cpu",StringComparison.OrdinalIgnoreCase)
                &&int.TryParse(args[番号+1],out var 値)){
                return 値;
            }
        }
        return null;
    }

    private static int CheckAllFixed(){
        var failed = 0;
        var compiler = new DynamicQueryCompiler();
        var primary = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var secondary = new int[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };
        foreach(var item in Benchmark.Dynamic.Core.FixedCaseCatalog.All){
            var source = Benchmark.Dynamic.Core.FixedCaseCatalog.Emit(item, 15);
            var compiled = compiler.Compile(new("check-" + item.ID, "Benchmark.Dynamic.Generated.FixedQuery", source, item.ID + ".cs"));
            if(!compiled.Success){
                Console.WriteLine($"[FAIL-COMPILE] {item.ID}: {string.Join("; ", compiled.Diagnostics.Where(d => d.Severity == "Error").Select(d => d.Message))}");
                failed++;
                continue;
            }
            if(compiled.LoadedQuery is { } loaded){
                try{
                    var exp = loaded.Run(DynamicImplementationKind.System, primary, secondary);
                    if(loaded.Has(DynamicImplementationKind.Ideal)){
                        var act = loaded.Run(DynamicImplementationKind.Ideal, primary, secondary);
                        var match = Benchmark.Dynamic.Core.DynamicQueryComparer.Compare(exp, act);
                        if(!match.IsMatch){
                            Console.WriteLine($"[MISMATCH-IDEAL] {item.ID}");
                            failed++;
                            continue;
                        }
                    }
                    if(loaded.Has(DynamicImplementationKind.FLinq)){
                        var act = loaded.Run(DynamicImplementationKind.FLinq, primary, secondary);
                        var match = Benchmark.Dynamic.Core.DynamicQueryComparer.Compare(exp, act);
                        if(!match.IsMatch){
                            Console.WriteLine($"[MISMATCH-FLINQ] {item.ID}");
                            failed++;
                            continue;
                        }
                    }
                    Console.WriteLine($"[OK] {item.ID}");
                }catch(Exception ex){
                    Console.WriteLine($"[RUNTIME-ERR] {item.ID}: {ex.Message}");
                    failed++;
                }
            }
        }
        Console.WriteLine($"Total: {Benchmark.Dynamic.Core.FixedCaseCatalog.All.Count}, Failed: {failed}");
        return failed == 0 ? 0 : 1;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll",SetLastError = true)]
    private static extern UIntPtr SetThreadAffinityMask(IntPtr スレッドハンドル,UIntPtr スレッドアフィニティマスク);

}
