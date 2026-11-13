using Benchmark.Dynamic.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Benchmark.Dynamic.Worker;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RegressionContractAttribute:Attribute{
    public string? Skip{get;set;}
}
[AttributeUsage(AttributeTargets.Method,AllowMultiple=true)]
public sealed class RegressionDataAttribute(params object?[] values):Attribute{
    public object?[] Values{get;}=values;
}

/// <summary>独自型、評価回数、例外、解放の元のassertionを同じWorkerで再実行する。</summary>
public sealed class RegressionSuite:IDisposable{
    private DynamicAssemblyLoadContext? context;
    private Assembly? assembly;
    private Dictionary<string,(MethodInfo Method,object?[] Arguments)>? cases;
    public IReadOnlyList<RegressionCaseDescriptor> Cases{get;}
    public string ExpandedSource{get;}
    public IReadOnlyList<string> Diagnostics{get;}
    internal bool AssertionsAreCollectible=>this.context?.Assemblies.Any(assembly=>assembly.GetName().Name=="xunit.assert"&&assembly.IsCollectible)==true;
    private readonly bool requireNoFallback;
    private IReadOnlyList<string> generatedSources=[];
    private RegressionSuite(DynamicAssemblyLoadContext? context,Assembly assembly,string expanded,IReadOnlyList<string> diagnostics,bool contracts=false,IReadOnlyDictionary<string,string[]>? sourcePaths=null){
        this.context=context;this.assembly=assembly;this.ExpandedSource=expanded;this.Diagnostics=diagnostics;
        this.requireNoFallback=!contracts;
        this.cases=new(StringComparer.Ordinal);
        var descriptors=new List<RegressionCaseDescriptor>();
        foreach(var type in assembly.GetTypes().Where(type=>!type.IsAbstract).OrderBy(type=>type.FullName)){
            foreach(var method in type.GetMethods().Where(method=>IsCaseMethod(method,contracts)).OrderBy(method=>method.Name)){
                string? skip;
                if(contracts){skip=method.GetCustomAttribute<RegressionContractAttribute>()!.Skip;}
                else{skip=method.GetCustomAttribute<FactAttribute>()!.Skip;}
                var rows=method.GetCustomAttributes<DataAttribute>().SelectMany(data=>data.GetData(method)).Concat(method.GetCustomAttributes<RegressionDataAttribute>().Select(data=>data.Values)).ToArray();
                if(method.GetParameters().Length==0){rows=[[]];}
                if(rows.Length==0){throw new InvalidDataException(type.FullName+"."+method.Name+": theoryにデータがありません。");}
                for(var row=0;row<rows.Length;row++){
                    var id=type.FullName+"."+method.Name;
                    if(method.GetParameters().Length!=0){id+="["+row+"]";}
                    string[] comparisons=[];
                    if(type.FullName=="Test.System.Linq.Intercept.EnumerableComparisonCatalogTests"&&method.Name=="BenchmarkMethodNames_match_enumerable_result"){
                        comparisons=["System.Linq","FLinqUnoptimized","FLinq","ZLinq"];
                    }
                    this.cases.Add(id,(method,rows[row]));descriptors.Add(new(id,type.FullName!,method.Name,row,skip){
                        SourcePaths=sourcePaths?.GetValueOrDefault(type.FullName!.Replace('+','.'))??[],
                        Arguments=string.Join(", ",rows[row].Select(argument=>argument?.ToString()??"null")),
                        Comparisons=comparisons,
                    });
                }
            }
        }
        if(descriptors.Count==0){throw new InvalidDataException("回帰ケースがありません。");}
        this.Cases=descriptors;
    }
    public static RegressionSuite Contracts(Assembly assembly)=>new(null,assembly,"",[],contracts:true);
    private static bool IsCaseMethod(MethodInfo method,bool contracts){
        if(contracts){return method.GetCustomAttribute<RegressionContractAttribute>() is not null;}
        return method.GetCustomAttribute<FactAttribute>() is not null;
    }
    public static RegressionSuite Compile(RegressionSourceBundle? bundle=null,CancellationToken cancellationToken=default){
        bundle??=RegressionCaseSources.Legacy;
        // 実行時に必要なassertion・出力・fixtureのアセンブリを参照集合へ含める。
        _=typeof(Assert).Assembly;_=typeof(FactAttribute).Assembly;_=typeof(ITestOutputHelper).Assembly;_=typeof(global::Seamless.TestKit.ClassInt32).Assembly;
        var options=new CSharpParseOptions(LanguageVersion.Preview).WithFeatures([new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")]);
        const string globals="global using System;global using System.Collections;global using System.Collections.Generic;global using System.IO;global using System.Linq;global using System.Threading;global using System.Threading.Tasks;global using Xunit;global using Xunit.Abstractions;global using Seamless.Collections;global using Seamless.Collections.Tsavorite;global using Seamless.TestKit;global using LinqCounter=Seamless.Linq.LinqCounter;global using LinqCounterScope=Seamless.Linq.LinqCounterScope;";
        var documents=bundle.Documents.Append(new("RegressionGlobalUsings.cs",globals));
        var trees=documents.Select(document=>CSharpSyntaxTree.ParseText(SourceText.From(document.Text,Encoding.UTF8),options,document.Path,cancellationToken)).ToArray();
        var compilation=CSharpCompilation.Create("Dynamic.RegressionCases",trees,new MetadataReferenceProvider().Get().References,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,optimizationLevel:OptimizationLevel.Release,allowUnsafe:true).WithConcurrentBuild(false));
        compilation=RegressionExpectedTypeResolver.Resolve(compilation);
        var sourcePaths=compilation.SyntaxTrees.SelectMany(tree=>{
            var model=compilation.GetSemanticModel(tree);
            return tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                .Select(node=>(Type:model.GetDeclaredSymbol(node)!.ToDisplayString(),Path:tree.FilePath));
        }).GroupBy(item=>item.Type).ToDictionary(group=>group.Key,group=>group.Select(item=>item.Path).Distinct().ToArray());
        GeneratorDriver driver=CSharpGeneratorDriver.Create([new global::Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:options);
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var diagnostics,cancellationToken);
        var generatedSources=driver.GetRunResult().Results.SelectMany(result=>result.GeneratedSources).Select(source=>source.SourceText.ToString()).ToArray();
        var generated=string.Join(Environment.NewLine,generatedSources);
        // [調査] 生成コードのコンパイルエラーは行番号しか出ないので、指定があれば生成ファイルを書き出す。
        var generatedRoot=Environment.GetEnvironmentVariable("SEAMLESS_TEST_GENERATED_ROOT");
        if(!string.IsNullOrWhiteSpace(generatedRoot)){
            Directory.CreateDirectory(generatedRoot);
            foreach(var source in driver.GetRunResult().Results.SelectMany(result=>result.GeneratedSources)){File.WriteAllText(Path.Combine(generatedRoot,source.HintName),source.SourceText.ToString());}
        }
        using var stream=new MemoryStream();var emit=output.Emit(stream,cancellationToken:cancellationToken);
        var errors=diagnostics.Concat(emit.Diagnostics).Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).ToArray();
        if(!emit.Success||errors.Length!=0){throw new InvalidDataException(string.Join(Environment.NewLine,errors.Select(error=>error.ToString())));}
        var expansionErrors=FLinqExpansionValidator.Validate(diagnostics.Select(d=>new DynamicCompilationDiagnostic("Generator",d.Severity.ToString(),d.Id,d.GetMessage(),d.Location.ToString())).ToArray(),generated).ToArray();
        if(expansionErrors.Length!=0){throw new InvalidDataException(string.Join(Environment.NewLine,expansionErrors.Select(error=>error.Message)));}
        var context=new DynamicAssemblyLoadContext("regression-"+Guid.NewGuid().ToString("N"),isolateAssertions:true);
        try{stream.Position=0;return new(context,context.LoadFromStream(stream),generated,diagnostics.Select(d=>d.ToString()).ToArray(),sourcePaths:sourcePaths){generatedSources=generatedSources.Select(GeneratedCodeText.Executable).ToArray()};}
        catch{context.Unload();throw;}
    }
    public RegressionCaseResult Run(string id,CancellationToken cancellationToken=default){
        cancellationToken.ThrowIfCancellationRequested();
        using var generated=RegressionGeneratedSources.Push(this.generatedSources);
        var item=(this.cases??throw new ObjectDisposedException(nameof(RegressionSuite)))[id];
        var descriptor=this.Cases.Single(item=>item.ID==id);
        if(descriptor.Skip is{} skip){return new(id,"Skipped","",skip,0);}
        var output=new CaseOutput();object? instance=null;
        var observations=new List<string>();using var evidence=RegressionEvidence.Push(observations);
        var result=DynamicFallbackCapture.Run(()=>{
            try{
                var constructor=item.Method.DeclaringType!.GetConstructors().Single();
                instance=constructor.Invoke(constructor.GetParameters().Select(parameter=>{
                    if(parameter.ParameterType==typeof(ITestOutputHelper)){return (object)output;}
                    throw new InvalidDataException("未対応のfixture: "+parameter.ParameterType);
                }).ToArray());
                var returned=item.Method.Invoke(instance,item.Arguments);
                if(returned is Task task){task.GetAwaiter().GetResult();}
                else if(returned is ValueTask valueTask){valueTask.GetAwaiter().GetResult();}
                return true;
            }finally{
                if(instance is IAsyncDisposable asyncDisposable){asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();}
                else if(instance is IDisposable disposable){disposable.Dispose();}
                instance=null;
            }
        });
        var error=result.Exception;
        foreach(var observation in observations){output.WriteLine(observation);}
        while(error is TargetInvocationException{InnerException:{} inner}){error=inner;}
        if(this.requireNoFallback&&result.FallbackCount!=0){
            var message="FLinqRuntimeFallback="+result.FallbackCount;
            if(error is not null){message+="\n"+error;}
            return new(id,"Failed",output.ToString(),message,result.FallbackCount);
        }
        var status="Passed";
        if(error is not null){status="Failed";}
        return new(id,status,output.ToString(),error?.ToString(),result.FallbackCount);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference? Release(){
        var context=this.context;if(context is null){return null;}
        this.context=null;this.cases=null;this.assembly=null;return DynamicAssemblyUnloader.Release(context);
    }
    public void Dispose(){var reference=this.Release();if(reference is not null&&!DynamicAssemblyUnloader.Verify(reference).Unloaded){throw new InvalidOperationException("回帰アセンブリを解放できませんでした。");}}
    private sealed class CaseOutput:ITestOutputHelper{
        private readonly StringBuilder text=new();
        public void WriteLine(string message)=>this.text.AppendLine(message);
        public void WriteLine(string format,params object[] args)=>this.text.AppendFormat(format,args).AppendLine();
        public override string ToString()=>this.text.ToString();
    }
}
