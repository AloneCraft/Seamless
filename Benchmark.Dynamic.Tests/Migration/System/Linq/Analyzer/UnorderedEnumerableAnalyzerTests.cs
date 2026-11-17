using InlineDataAttribute=Benchmark.Dynamic.Worker.RegressionDataAttribute;
using Seamless.Collections;
using Seamless.Collections.Tsavorite;
using FactAttribute=Benchmark.Dynamic.Worker.RegressionContractAttribute;
using TheoryAttribute=Benchmark.Dynamic.Worker.RegressionContractAttribute;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Seamless.Linq.Analyzers;

namespace Test.System.Linq.Analyzer;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class UnorderedEnumerableAnalyzerTests {
    [Fact]
    public async Task DirectChainAndLocalAreRejected(){
        var diagnostics=await Analyze("""
            class C {
                int Direct(Seamless.Collections.Tsavorite.HashSet<int> source)=>source.Where(x=>x>0).First();
                int Local(Seamless.Collections.Tsavorite.HashSet<int> source){var values=source.Where(x=>x>0);return values.Last();}
            }
            """);
        Assert.Equal(2,diagnostics.Count(diagnostic=>diagnostic.Id=="SEAMLINQ001"));
    }

    [Fact]
    public async Task ExplicitAsEnumerableOptsIntoOrder(){
        var diagnostics=await Analyze("""
            class C {
                int M(Seamless.Collections.Tsavorite.HashSet<int> source)=>source.AsEnumerable().First();
            }
            """);
        Assert.DoesNotContain(diagnostics,diagnostic=>diagnostic.Id is "SEAMLINQ001" or "SEAMLINQ002");
    }

    [Fact]
    public async Task AttributePropagatesAcrossBoundary(){
        var diagnostics=await Analyze("""
            class C {
                [return:UnorderedEnumerable]
                IEnumerable<int> Pass([UnorderedEnumerable] IEnumerable<int> source)=>source;
                int M(Seamless.Collections.Tsavorite.HashSet<int> source)=>Pass(source).First();
            }
            """);
        Assert.Contains(diagnostics,diagnostic=>diagnostic.Id=="SEAMLINQ001");
        Assert.DoesNotContain(diagnostics,diagnostic=>diagnostic.Id=="SEAMLINQ002");
    }

    [Fact]
    public async Task UnannotatedBoundaryIsRejected(){
        var diagnostics=await Analyze("""
            class C {
                IEnumerable<int> Pass(IEnumerable<int> source)=>source;
                IEnumerable<int> M(Seamless.Collections.Tsavorite.HashSet<int> source)=>Pass(source);
            }
            """);
        Assert.Contains(diagnostics,diagnostic=>diagnostic.Id=="SEAMLINQ002");
    }

    [Fact]
    public async Task RelationTypesPreserveOriginAcrossReturnsArgumentsAndMembers(){
        var diagnostics=await Analyze("""
            class C{
                private Seamless.Collections.ISet<int> field=null!;
                public Seamless.Collections.ISet<int> Property{get;set;}=null!;
                Seamless.Collections.ISet<int> Pass(Seamless.Collections.ISet<int> source){this.field=source;this.Property=source;return source;}
                int Ordered(Seamless.Collections.ISet<int> source)=>Pass(source).First();
                IEnumerable<int> Escapes(Seamless.Collections.ISet<int> source)=>source;
            }
            """);
        Assert.Equal(1,diagnostics.Count(diagnostic=>diagnostic.Id=="SEAMLINQ001"));
        Assert.Equal(1,diagnostics.Count(diagnostic=>diagnostic.Id=="SEAMLINQ002"));
        Assert.Contains(diagnostics,diagnostic=>diagnostic.Id=="SEAMLINQ002"&&diagnostic.GetMessage().Contains("Escapes"));
    }

    [Fact]
    public async Task ProjectAndSourceOptionsPreserveAnalyzerSelection(){
        const string body="class C{int M(Seamless.Collections.Tsavorite.HashSet<int> source)=>source.First();}";
        Assert.Contains(await Analyze(body),diagnostic=>diagnostic.Id=="SEAMLINQ001"&&diagnostic.Severity==DiagnosticSeverity.Error);
        Assert.Empty(await Analyze(body,"false"));
        Assert.Empty(await Analyze(body,"false",""));
        Assert.Contains(await Analyze(body,"false","true"),diagnostic=>diagnostic.Id=="SEAMLINQ001"&&diagnostic.Severity==DiagnosticSeverity.Error);
        Assert.Empty(await Analyze(body,"true","false"));
    }

    private sealed class Options(string key,string? value):AnalyzerConfigOptions{
        public override bool TryGetValue(string name,out string result){
            if(name==key&&value is not null){result=value;return true;}
            result="";return false;
        }
    }
    internal sealed class OptionProvider(string? project,string? source):AnalyzerConfigOptionsProvider{
        public override AnalyzerConfigOptions GlobalOptions{get;}=new Options("build_property.SeamlessLinqAnalysis",project);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)=>new Options("build_metadata.Compile.SeamlessLinqAnalysis",source);
        public override AnalyzerConfigOptions GetOptions(AdditionalText text)=>new Options("",null);
    }
    private static async Task<ImmutableArray<Diagnostic>> Analyze(string body,string? project=null,string? sourceOption=null){
        var source="""
            global using System.Linq;
            global using System.Collections.Generic;
            global using Seamless.Linq;
            namespace Seamless.Linq {
                [System.AttributeUsage(System.AttributeTargets.Parameter|System.AttributeTargets.ReturnValue|System.AttributeTargets.Field|System.AttributeTargets.Property)]
                public sealed class UnorderedEnumerableAttribute:System.Attribute {}
            }
            namespace Seamless.Collections.Tsavorite {
                public sealed class HashSet<T>:System.Collections.Generic.IEnumerable<T> {
                    public System.Collections.Generic.IEnumerator<T> GetEnumerator()=>throw null!;
                    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
                }
            }
            """+Environment.NewLine+body;
        var references=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path=>MetadataReference.CreateFromFile(path));
        var compilation=CSharpCompilation.Create(
            "AnalyzerTests",
            [CSharpSyntaxTree.ParseText(source,new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors=compilation.GetDiagnostics().Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length==0,string.Join(Environment.NewLine,errors.Select(static error=>error.ToString())));
        return await compilation.WithAnalyzers([new UnorderedEnumerableAnalyzer()],new AnalyzerOptions([],new OptionProvider(project,sourceOption))).GetAnalyzerDiagnosticsAsync();
    }
}
