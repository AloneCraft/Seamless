using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.FLinq;

namespace Test.SourceGenerator;

[LinqIntercept(false)]
public sealed class BlockFunc{
    private static readonly (string Name,string Body,bool Comment)[] Cases=[
        ("local","var literal=\"this.X\";this.Observe(this.X);return literal;",false),
        ("return","this.Observe(this.X);return \"this.X\";",false),
        ("local-function","string Local()=>\"this.X\";this.Observe(this.X);return Local();",false),
        ("interpolation","this.Observe(this.X);return $\"this.X|{this.X:000}|{this.X:this.X}|{{this.X}}\";",false),
        ("throw","this.Observe(this.X);throw new InvalidOperationException(\"this.X\");",false),
        ("callback","var literal=this.Invoke(()=>\"this.X\");this.Observe(this.X);return literal;",false),
        ("verbatim","this.Observe(this.X);return @\"this.X|@this.X\";",false),
        ("comment","var literal= /* this.X */ \"this.X\";this.Observe(this.X);return literal;",true),
        ("escaped","this.Observe(this.X);return \"this.X \\\"quoted\\\"\";",false),
        ("verbatim-newline","""
            this.Observe(this.X);return @"this.X
            next";
            """,false),
        ("raw",""""this.Observe(this.X);return """this.X "quoted" @this.X""";"""",false),
        ("raw-newline",""""
            this.Observe(this.X);return """
                this.X
                @this.X
                """;
            """",false),
        ("raw-interpolation",""""this.Observe(this.X);return $$"""this.X {{this.X}} @this.X""";"""",false),
        ("line-comment","""
            var literal="this.X" // this.X
                +"|tail";
            this.Observe(this.X);return literal;
            """,false)
    ];
    private static readonly string[] Modes=["AllOff","NoneOff","AllCounter","NoneCounter"];

    [Fact]
    public void LiteralValuesAndThisCapture(){
        var observations=new List<object>();
        var failures=new List<string>();
        foreach(var item in Cases){
            for(var mode=0;mode<Modes.Length;mode++){
                var source=BuildSource(item.Body,mode);
                var generated=string.Empty;
                var stage="CompileUnsupported";
                var cell=new List<object>();
                var cellFailures=new List<string>();
                GeneratorIdentity? identity=null;
                try{
                    var result=Generate(source,item.Name+"-"+Modes[mode],ref identity);
                    generated=result.Generated;
                    stage="ExecutionInfrastructure";
                    ExecutableGeneration.Run(result.Compilation,"Sample.Probe",probe=>{
                        var run=probe.GetMethod("Run")!;
                        var expected=(object[])run.Invoke(null,[false])!;
                        var actual=(object[])run.Invoke(null,[true])!;
                        for(var column=0;column<4;column++){
                            if(!Equals(expected[column],actual[column])){
                                cellFailures.Add($"{item.Name}/{Modes[mode]} column{column}: expected {expected[column]}, actual {actual[column]}");
                            }
                        }
                        var logical=0L;
                        if(mode>=2){logical=1;}
                        if((long)actual[4]!=logical||(long)actual[5]!=0L){cellFailures.Add($"{item.Name}/{Modes[mode]} counter expected {logical}/0, actual {actual[4]}/{actual[5]}");}
                        if(generated.Contains("@@this",StringComparison.Ordinal)){cellFailures.Add($"{item.Name}/{Modes[mode]} has unexpected @@this text");}
                        if(item.Comment&&!generated.Contains("/* this.X */",StringComparison.Ordinal)){cellFailures.Add($"{item.Name}/{Modes[mode]} changed the retained comment");}
                        cell.Add(new{Case=item.Name,Mode=Modes[mode],Status="Executed",Expected=Snapshot(expected),Actual=Snapshot(actual)});
                        return true;
                    });
                }catch(Exception exception){
                    cellFailures.Add($"{item.Name}/{Modes[mode]} {stage}: {exception.GetType().Name}: {exception.Message}");
                    cell.Add(new{Case=item.Name,Mode=Modes[mode],Status=stage,ExceptionType=exception.GetType().FullName,exception.Message});
                }
                observations.AddRange(cell);
                failures.AddRange(cellFailures);
                Save(item.Name+"-"+Modes[mode],source,generated,cell,cellFailures,identity);
            }
        }
        Save("summary",string.Join(Environment.NewLine,Cases.Select(item=>item.Body)),string.Empty,observations,failures,null);
        Assert.Empty(failures);
    }

    private static string BuildSource(string body,int mode){
        var attribute="[LinqIntercept(true)]";
        if(mode==1){attribute="[LinqIntercept(true,Optimizations=LinqOptimizations.None)]";}
        else if(mode==2){attribute="[LinqIntercept(true,LinqInstrumentation.Counter)]";}
        else if(mode==3){attribute="[LinqIntercept(true,LinqInstrumentation.Counter,Optimizations=LinqOptimizations.None)]";}
        return """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace Sample;
            public sealed class Probe{
                public int X=7;
                public readonly List<string> Trace=new();
                public int[] Values()=>new[]{1};
                public void Observe(int value)=>this.Trace.Add("X"+value);
                public string Invoke(Func<string> callback)=>callback();
                public static object[] Run(bool actual){
                    var probe=new Probe();using var scope=LinqCounterScope.Start();
                    string value="",error="",message="";
                    try{if(actual){value=probe.Query();}else{value=probe.Reference();}}
                    catch(Exception exception){error=exception.GetType().FullName!;message=exception.Message;}
                    return new object[]{value,error,message,string.Join(",",probe.Trace),scope.Counter.LogicalDelegate,scope.Counter.PhysicalDelegate};
                }
            """+Environment.NewLine+attribute+"private string Query()=>this.Values().Select<int,string>(p=>{"+body+"}).Single();"+Environment.NewLine+
            "[LinqIntercept(false)]private string Reference()=>this.Values().Select<int,string>(p=>{"+body+"}).Single();"+Environment.NewLine+"}";
    }

    private static (CSharpCompilation Compilation,string Generated) Generate(string source,string name,ref GeneratorIdentity? identity){
        var options=new CSharpParseOptions(LanguageVersion.Preview).WithFeatures([
            new("InterceptorsPreview","true"),new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")
        ]);
        var compilation=GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]).WithAssemblyName("BlockString"+name.Replace("-","_",StringComparison.Ordinal));
        compilation=compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(),CSharpSyntaxTree.ParseText(source,options,path:"Container.cs"));
        using var handle=GeneratorHandle.Create();
        identity=handle.Identity;
        GeneratorDriver driver=CSharpGeneratorDriver.Create([handle.Generator.AsSourceGenerator()],parseOptions:options);
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var generatorDiagnostics);
        var diagnostics=generatorDiagnostics.Concat(output.GetDiagnostics()).ToArray();
        var errors=diagnostics.Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Select(diagnostic=>diagnostic.ToString()).ToList();
        var generated=string.Concat(output.SyntaxTrees.Skip(1).Select(tree=>tree.ToString()));
        var records=diagnostics.Select(diagnostic=>(object)new{diagnostic.Id,Severity=diagnostic.Severity.ToString(),Full=diagnostic.ToString()}).ToList();
        Save("generation-"+name,source,generated,records,errors,identity);
        Assert.Empty(errors);
        Assert.Contains("InterceptsLocationAttribute(1,",generated);
        return ((CSharpCompilation)output,generated);
    }

    private sealed record GeneratorIdentity(string Mode,string Path,string SHA256);

    private sealed class GeneratorContext:global::System.Runtime.Loader.AssemblyLoadContext{
        private readonly string generatorName;
        public GeneratorContext(string generatorName):base(isCollectible:true){this.generatorName=generatorName;}
        protected override global::System.Reflection.Assembly? Load(global::System.Reflection.AssemblyName name){
            if(string.Equals(name.Name,this.generatorName,StringComparison.OrdinalIgnoreCase)){
                throw new InvalidOperationException("Generator dependency cannot resolve to the default context.");
            }
            return Default.Assemblies.SingleOrDefault(assembly=>string.Equals(assembly.FullName,name.FullName,StringComparison.Ordinal));
        }
    }

    private sealed class GeneratorHandle:IDisposable{
        private readonly GeneratorContext? context;
        public IIncrementalGenerator Generator{get;}
        public GeneratorIdentity Identity{get;}
        private GeneratorHandle(IIncrementalGenerator generator,GeneratorIdentity identity,GeneratorContext? context){
            this.Generator=generator;this.Identity=identity;this.context=context;
        }
        public static GeneratorHandle Create(){
            var requested=Environment.GetEnvironmentVariable("SEAMLESS_BLOCK_GENERATOR_PATH");
            var expected=Environment.GetEnvironmentVariable("SEAMLESS_BLOCK_GENERATOR_SHA256");
            if(string.IsNullOrWhiteSpace(expected)){expected=null;}
            if(expected is not null&&(expected.Length!=64||!expected.All(Uri.IsHexDigit))){throw new InvalidOperationException("Invalid SEAMLESS_BLOCK_GENERATOR_SHA256.");}
            GeneratorContext? context=null;
            try{
                IIncrementalGenerator generator;
                string mode;
                if(string.IsNullOrWhiteSpace(requested)){
                    generator=new global::Seamless.FLinq.Generator.FLinqIncrementalGenerator();
                    mode="default";
                }else{
                    if(!Path.IsPathFullyQualified(requested)){throw new InvalidOperationException("Generator path must be absolute.");}
                    var absolute=Path.GetFullPath(requested);
                    var beforeLoadHash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(absolute)));
                    if(expected is not null&&!string.Equals(expected,beforeLoadHash,StringComparison.OrdinalIgnoreCase)){throw new InvalidOperationException($"Generator SHA256 mismatch before load: {absolute}; actual={beforeLoadHash}.");}
                    var name=global::System.Reflection.AssemblyName.GetAssemblyName(absolute);
                    context=new GeneratorContext(name.Name!);
                    var assembly=context.LoadFromAssemblyPath(absolute);
                    if(!string.Equals(Path.GetFullPath(assembly.Location),absolute,StringComparison.OrdinalIgnoreCase)){throw new InvalidOperationException("Loaded generator path differs from requested path.");}
                    generator=(IIncrementalGenerator)(Activator.CreateInstance(assembly.GetType("Seamless.FLinq.Generator.FLinqIncrementalGenerator",throwOnError:true)!)
                        ??throw new InvalidOperationException("Generator instance is null."));
                    mode="external";
                }
                var location=generator.GetType().Assembly.Location;
                if(!Path.IsPathFullyQualified(location)){throw new InvalidOperationException("Loaded generator location must be absolute.");}
                var path=Path.GetFullPath(location);
                var hash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
                if(expected is not null&&!string.Equals(expected,hash,StringComparison.OrdinalIgnoreCase)){throw new InvalidOperationException($"Generator SHA256 mismatch: {path}; actual={hash}.");}
                return new(generator,new(mode,path,hash),context);
            }catch{
                context?.Unload();
                throw;
            }
        }
        public void Dispose(){this.context?.Unload();}
    }

    private static object Snapshot(object[] value)=>new{Value=value[0],ExceptionType=value[1],ExceptionMessage=value[2],Trace=value[3],LogicalDelegate=value[4],PhysicalDelegate=value[5]};

    private static void Save(string stage,string source,string generated,List<object> observations,List<string> failures,GeneratorIdentity? identity){
        var target=Environment.GetEnvironmentVariable("SEAMLESS_BLOCK_STRING_ARTIFACT_DIR");
        if(string.IsNullOrWhiteSpace(target)){return;}
        var inputHash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        var generatedHash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(generated)));
        var directory=Path.Combine(Path.GetFullPath(target),stage,inputHash);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"input.cs"),source);
        File.WriteAllText(Path.Combine(directory,"generated.cs"),generated);
        File.WriteAllText(Path.Combine(directory,"observations.json"),JsonSerializer.Serialize(new{Stage=stage,Generator=identity,Observations=observations,Failures=failures},new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(directory,"hashes.json"),JsonSerializer.Serialize(new{InputSHA256=inputHash,GeneratedSHA256=generatedHash,LoadedGeneratorSHA256=identity?.SHA256,Generator=identity}));
    }
}
