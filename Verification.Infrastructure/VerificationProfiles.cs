using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Benchmark.Dynamic.Core;
using Benchmark.Tsavorite.ParameterSweep;
using Verification.Core;
namespace Verification.Infrastructure;
public static class VerificationProfiles{
    public static VerificationPlan Dynamic(string root,string profile,string seed="1",int queries=1,SourceTarget source=SourceTarget.SystemArray,int inputCount=256,int operators=4,int depth=4){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(queries);
        ArgumentOutOfRangeException.ThrowIfNegative(inputCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(operators);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        if(depth>operators){throw new ArgumentOutOfRangeException(nameof(depth),"Depth cannot exceed the requested operator count.");}
        if(profile is not ("quick" or "full")){throw new ArgumentException("Unknown profile.",nameof(profile));}
        var project="Benchmark/Benchmark.Dynamic.Worker/Benchmark.Dynamic.Worker.csproj";
        var basis=RepositoryInspection.Plan(root,profile);
        var environment=basis.Cases.FirstOrDefault()?.Conditions??throw new InvalidOperationException("No verification environment was detected.");
        var cases=new List<VerificationCase>();
        for(var query=0;query<queries;query++){
            var request=new WorkerRunRequest("verification",Seed256.Parse(seed),1,operators,depth,inputCount,OperatorSetMode.Common,[source],new(),
                RandomQueryGenerator.GeneratorVersion,QueryCapabilityProfile.CurrentProfileVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                BuiltInOperatorCatalog.OperatorSetVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),QueryIndexOffset:query);
            var json=JsonSerializer.Serialize(request,DynamicWorkerProtocol.JsonOptions);
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            var conditions=environment with{CaseVersion="dynamic|"+RandomQueryGenerator.GeneratorVersion,InputHash=hash,
                Semantics="dotnet-v1|"+source,CodecVersion="dynamic-ir-"+QueryIrDocument.CurrentSchemaVersion,
                MeasurementVersion="dynamic|"+JsonSerializer.Serialize(request.MeasurementProfile,DynamicWorkerProtocol.JsonOptions)};
            cases.Add(new("dynamic:"+source+":"+seed+":"+query,"Dynamic "+source+" #"+query,"クエリの正しさ検証","dynamic-worker",project,
                ["build",project,"-c","Release","--nologo"],[json],conditions,"固定Seedのクエリを全実装経路で比較します。",true));
        }
        return basis with{Cases=cases.ToArray()};
    }
    public static VerificationPlan Benchmark(string root,string profile,string tab="System.Array",string type="Int32",string method="Where_predicate"){
        var basis=RepositoryInspection.Plan(root,profile,includeRepresentativeBenchmarks:false);
        var environment=basis.Cases.FirstOrDefault()?.Conditions??EnvironmentConditions();
        var item=BenchmarkCase(environment,tab,type,method,"明示指定された代表ケースをpaired Workerで反復測定します。");
        return basis with{Cases=[item]};
    }
    public static VerificationPlan Sweep(string root,string profile){
        var basis=RepositoryInspection.Plan(root,profile,includeRepresentativeBenchmarks:false);
        var environment=basis.Cases.FirstOrDefault()?.Conditions??EnvironmentConditions();
        return basis with{Cases=SweepCases(environment,profile,representativeOnly:false)};
    }
    public static VerificationPlan Scalable(string root,string profile){
        var basis=RepositoryInspection.Plan(root,profile,includeRepresentativeBenchmarks:false);
        var environment=basis.Cases.FirstOrDefault()?.Conditions??EnvironmentConditions();
        return basis with{Cases=ScalableCases(environment,profile,representativeOnly:false)};
    }
    internal static VerificationCase[] ScalableCases(ComparisonConditions environment,string profile,bool representativeOnly){
        if(profile is not ("quick" or "full")){throw new ArgumentException("Unknown profile.",nameof(profile));}
        var cases=new List<VerificationCase>();
        AddScalableCase(cases,environment,"Benchmark.Scalable.Persisted.Collections","Shared.Scalable.Persisted.Collections.Set.SetInt32","Add",
            new Dictionary<string,string>{{"Count","65536"},{"Initial","1"},{"Data","4096"}},65536);
        AddScalableCase(cases,environment,"Benchmark.Scalable.Tsavorite.Collections","Shared.Scalable.Tsavorite.Collections.Set.SetInt32","Add",
            new Dictionary<string,string>{{"Count","65536"},{"Index","64"},{"Page","4096"},{"Segment","4096"},{"Memory","8388608"},{"Fraction","1.0"}},65536);
        if(profile=="quick"||representativeOnly){return cases.ToArray();}
        cases.Clear();
        var persistedCounts=new[]{65536,131072,262144,524288,1048576,2097152,4194304,8388608};
        var persistedGroups=new[]{
            (Namespace:"Set",Types:new[]{"SetInt32","SetString","SetUnmanagedStruct","SetManagedStruct","SetManagedClass"},Methods:new[]{"Add","Contains"}),
            (Namespace:"List",Types:new[]{"ListInt32","ListString"},Methods:new[]{"Add","Remove","Indexer"}),
            (Namespace:"Dictionary",Types:new[]{"DictionaryStringString","DictionaryStringInt32","DictionaryInt32String","DictionaryInt32Int32","DictionaryUnmanagedStructUnmanagedStruct","DictionaryManagedStructManagedStruct","DictionaryManagedClassManagedClass"},Methods:new[]{"Add","TryGetValue"}),
        };
        foreach(var group in persistedGroups){
            foreach(var type in group.Types){
                foreach(var method in group.Methods){
                    foreach(var count in persistedCounts){
                        AddScalableCase(cases,environment,"Benchmark.Scalable.Persisted.Collections","Shared.Scalable.Persisted.Collections."+group.Namespace+"."+type,method,
                            new Dictionary<string,string>{{"Count",count.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"Initial","1"},{"Data","4096"}},count);
                    }
                }
            }
        }
        var tsavoriteCounts=new[]{65536,131072,262144,524288,1048576};
        var tsavoriteGroups=new[]{
            (Namespace:"Set",Types:new[]{"SetInt32","SetString","SetUnmanagedStruct","SetManagedStruct","SetManagedClass"},Methods:new[]{"Add","Contains"}),
            (Namespace:"Dictionary",Types:new[]{"DictionaryStringString","DictionaryStringInt32","DictionaryInt32String","DictionaryInt32Int32","DictionaryUnmanagedStructUnmanagedStruct","DictionaryManagedStructManagedStruct","DictionaryManagedClassManagedClass"},Methods:new[]{"Add","TryGetValue"}),
        };
        foreach(var group in tsavoriteGroups){
            foreach(var type in group.Types){
                foreach(var method in group.Methods){
                    foreach(var count in tsavoriteCounts){
                        foreach(var index in new[]{64,512}){
                            foreach(var factor in new[]{128,1024}){
                                var memory=checked((long)count*factor);
                                AddScalableCase(cases,environment,"Benchmark.Scalable.Tsavorite.Collections","Shared.Scalable.Tsavorite.Collections."+group.Namespace+"."+type,method,
                                    new Dictionary<string,string>{{"Count",count.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"Index",index.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"Page","4096"},{"Segment","4096"},{"Memory",memory.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"Fraction","1.0"}},count);
                            }
                        }
                    }
                }
            }
        }
        return cases.ToArray();
    }
    private static void AddScalableCase(List<VerificationCase> cases,ComparisonConditions environment,string assembly,string type,string method,IReadOnlyDictionary<string,string> arguments,int elementCount){
        var request=JsonSerializer.Serialize(new{AssemblyName=assembly,TypeName=type,MethodName=method,Arguments=arguments,ElementCount=elementCount,LoopCount=1,LogicalCpu=0});
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request)));
        var conditions=environment with{
            CaseVersion="scalable-worker-v2|"+assembly+"|"+type+"|"+method,
            InputHash=hash,
            Semantics="scalable-collection-v1",
            CodecVersion="benchmark-fixture-v1",
            MeasurementVersion="scalable-fast-worker-v2|discard2|samples7|warmup300ms|cpu0",
            Configuration="Release",
        };
        var project="Benchmark/Benchmark.Scalable.Headless/Benchmark.Scalable.Headless.csproj";
        cases.Add(new("scalable:"+hash[..20],type+"."+method+" ("+string.Join(", ",arguments.Select(static pair=>pair.Key+"="+pair.Value))+")","容量・設定探索","scalable-worker",project,
            ["build",project,"-c","Release","--nologo"],[request],conditions,"大規模コレクションを固定CPUの独立Workerで反復測定します。",true));
    }
    internal static VerificationCase[] SweepCases(ComparisonConditions environment,string profile,bool representativeOnly){
        IReadOnlyList<パラメータスイープケース> source;
        if(profile=="quick"){source=パラメータスイープ計画.スモークケース一覧を作成();}
        else if(profile=="full"){source=パラメータスイープ計画.既定ケース一覧を作成();}
        else{throw new ArgumentException("Unknown profile.",nameof(profile));}
        if(representativeOnly&&profile=="quick"){source=source.Take(1).ToArray();}
        var project="Benchmark/Benchmark.Tsavorite.ParameterSweep.Headless/Benchmark.Tsavorite.ParameterSweep.Headless.csproj";
        var results=new List<VerificationCase>(source.Count);
        foreach(var sweepCase in source){
            var request=JsonSerializer.Serialize(new{Profile=profile,Case=sweepCase});
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request)));
            var conditions=environment with{
                CaseVersion="parameter-sweep-v1|"+sweepCase.ケース名,
                InputHash=hash,
                Semantics="tsavorite-capacity-v1",
                CodecVersion="memorypack-1",
                MeasurementVersion="capacity-sweep-v1",
                Configuration="Release",
            };
            results.Add(new("sweep:"+hash[..20],sweepCase.ケース名,"容量・設定探索","parameter-sweep",project,
                ["build",project,"-c","Release","--nologo"],[request],conditions,"容量とストア設定の境界を独立プロセスで測定します。",true));
        }
        return results.ToArray();
    }
    internal static VerificationCase[] RepresentativeBenchmarks(ComparisonConditions environment,string profile){
        var values=new List<VerificationCase>{
            BenchmarkCase(environment,"System.Array","Int32","Where_predicate","変更後の短時間確認に使う代表計測です。"),
        };
        if(profile=="full"){
            values.Add(BenchmarkCase(environment,"System.List","Int32","GroupBy_keySelector","完全検証のグループ化代表計測です。"));
            values.Add(BenchmarkCase(environment,"System.Array","Int32","Join_inner","完全検証の結合代表計測です。"));
            values.Add(BenchmarkCase(environment,"Seamless.Persisted.Seamless","Int32","Order","完全検証の永続化ソート代表計測です。"));
            values.Add(BenchmarkCase(environment,"Seamless.Tsavorite.Seamless","Int32","Distinct","完全検証のTsavorite集合演算代表計測です。"));
        }
        return values.ToArray();
    }
    private static VerificationCase BenchmarkCase(ComparisonConditions environment,string tab,string type,string method,string reason){
        var project="Benchmark/Benchmark.Headless/Benchmark.Headless.csproj";
        var identity=tab+'\0'+type+'\0'+method;
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        var conditions=environment with{
            CaseVersion="headless-paired-v2|"+identity.Replace('\0','|'),
            InputHash=hash,
            Semantics="system-linq-dotnet-equality",
            CodecVersion="none",
            MeasurementVersion="paired-worker-raw-v2|warmup16|samples9|element4096|seed1",
            Configuration="Release",
        };
        return new("benchmark:"+tab+":"+type+":"+method,tab+" "+type+"."+method,"性能比較","headless-benchmark",project,
            ["build",project,"-c","Release","--nologo"],[tab,type,method],conditions,reason,true);
    }
    internal static ComparisonConditions EnvironmentConditions()=>new(
        "environment-v1","none","none","none","none",
        Environment.MachineName+"|"+System.Runtime.InteropServices.RuntimeInformation.OSDescription+"|"+System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        (Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")??System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString())+"|"+Environment.ProcessorCount,
        "Release");
    public static IVerificationAdapter[] Adapters()=>[new DotnetVerificationAdapter(),new DynamicVerificationAdapter(),new DynamicShrinkVerificationAdapter(),new HeadlessBenchmarkVerificationAdapter(),new ParameterSweepVerificationAdapter(),new ScalableVerificationAdapter()];
}
