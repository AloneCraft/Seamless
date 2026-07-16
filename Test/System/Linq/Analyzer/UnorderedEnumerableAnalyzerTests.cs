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
                int Direct(Seamless.Tsavorite.Collections.HashSet<int> source)=>source.Where(x=>x>0).First();
                int Local(Seamless.Tsavorite.Collections.HashSet<int> source){var values=source.Where(x=>x>0);return values.Last();}
            }
            """);
        Assert.Equal(2,diagnostics.Count(diagnostic=>diagnostic.Id=="SEAMLINQ001"));
    }

    [Fact]
    public async Task ExplicitAsEnumerableOptsIntoOrder(){
        var diagnostics=await Analyze("""
            class C {
                int M(Seamless.Tsavorite.Collections.HashSet<int> source)=>source.AsEnumerable().First();
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
                int M(Seamless.Tsavorite.Collections.HashSet<int> source)=>Pass(source).First();
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
                IEnumerable<int> M(Seamless.Tsavorite.Collections.HashSet<int> source)=>Pass(source);
            }
            """);
        Assert.Contains(diagnostics,diagnostic=>diagnostic.Id=="SEAMLINQ002");
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string body){
        var source="""
            global using System.Linq;
            global using System.Collections.Generic;
            global using Seamless.Linq;
            namespace Seamless.Linq {
                [System.AttributeUsage(System.AttributeTargets.Parameter|System.AttributeTargets.ReturnValue|System.AttributeTargets.Field|System.AttributeTargets.Property)]
                public sealed class UnorderedEnumerableAttribute:System.Attribute {}
            }
            namespace Seamless.Tsavorite.Collections {
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
        return await compilation.WithAnalyzers([new UnorderedEnumerableAnalyzer()]).GetAnalyzerDiagnosticsAsync();
    }
}
