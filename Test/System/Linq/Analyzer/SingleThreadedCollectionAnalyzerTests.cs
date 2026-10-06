using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Seamless.Linq.Analyzers;

namespace Test.System.Linq.Analyzer;

/// <summary>シングル用のコレクションを、別のスレッドで実行されるラムダが捕捉する形の警告(SEAMTHREAD001、BUG-0016)の仕様。</summary>
[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class SingleThreadedCollectionAnalyzerTests {
    [Fact]
    public async Task TaskRunCapturingLocalIsReported(){
        var diagnostics=await Analyze("""
            class C {
                void M(){
                    var set=new Seamless.Collections.Tsavorite.HashSet<int>();
                    System.Threading.Tasks.Task.Run(()=>{foreach(var item in set){}});
                }
            }
            """);
        Assert.Single(diagnostics,diagnostic=>diagnostic.Id=="SEAMTHREAD001");
    }

    [Fact]
    public async Task ParallelForCapturingParameterIsReported(){
        var diagnostics=await Analyze("""
            class C {
                void M(Seamless.Collections.Tsavorite.HashSet<int> set){
                    System.Threading.Tasks.Parallel.For(0,4,i=>{foreach(var item in set){}});
                }
            }
            """);
        Assert.Single(diagnostics,diagnostic=>diagnostic.Id=="SEAMTHREAD001");
    }

    [Fact]
    public async Task ThreadCapturingFieldAndTableAreReported(){
        var diagnostics=await Analyze("""
            class C {
                Seamless.Collections.Tsavorite.HashSet<int> set=new();
                Seamless.Database.Table<int> table=new();
                void M(){
                    new System.Threading.Thread(()=>{foreach(var item in set){}}).Start();
                    System.Threading.Tasks.Task.Factory.StartNew(()=>{foreach(var item in table){}});
                }
            }
            """);
        Assert.Equal(2,diagnostics.Count(diagnostic=>diagnostic.Id=="SEAMTHREAD001"));
    }

    [Fact]
    public async Task CreatedInsideLambdaParallelTypeAndSameThreadAreNotReported(){
        var diagnostics=await Analyze("""
            class C {
                void M(Seamless.Collections.Tsavorite.ParallelHashSet<int> parallel,Seamless.Collections.Tsavorite.HashSet<int> single,Other other){
                    //ラムダの中で作る・並列用の型・別のオブジェクトのメンバー・別のスレッドで動かさない呼び出し
                    System.Threading.Tasks.Task.Run(()=>{var own=new Seamless.Collections.Tsavorite.HashSet<int>();foreach(var item in own){}});
                    System.Threading.Tasks.Task.Run(()=>{foreach(var item in parallel){}});
                    System.Threading.Tasks.Task.Run(()=>{foreach(var item in other.Items){}});
                    new List<int>().ForEach(x=>{foreach(var item in single){}});
                    foreach(var item in single){}
                }
            }
            class Other { public Seamless.Collections.Tsavorite.HashSet<int> Items=new(); }
            """);
        Assert.DoesNotContain(diagnostics,diagnostic=>diagnostic.Id=="SEAMTHREAD001");
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string body){
        var source="""
            global using System.Linq;
            global using System.Collections.Generic;
            namespace Seamless.Collections.Tsavorite {
                public struct ExclusiveAccess {}
                public struct ParallelAccess {}
                public abstract class Collection<TKey,TMode>:global::System.Collections.Generic.IEnumerable<TKey> {
                    public global::System.Collections.Generic.IEnumerator<TKey> GetEnumerator()=>throw null!;
                    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
                }
                public sealed class HashSet<T>:Collection<T,ExclusiveAccess> {}
                public sealed class ParallelHashSet<T>:Collection<T,ParallelAccess> {}
            }
            namespace Seamless.Database {
                public sealed class Table<T>:global::System.Collections.Generic.IEnumerable<T> {
                    public global::System.Collections.Generic.IEnumerator<T> GetEnumerator()=>throw null!;
                    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
                }
            }
            """+Environment.NewLine+body;
        var references=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path=>MetadataReference.CreateFromFile(path));
        var compilation=CSharpCompilation.Create(
            "SingleThreadedAnalyzerTests",
            [CSharpSyntaxTree.ParseText(source,new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors=compilation.GetDiagnostics().Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length==0,string.Join(Environment.NewLine,errors.Select(static error=>error.ToString())));
        return await compilation.WithAnalyzers([new SingleThreadedCollectionAnalyzer()]).GetAnalyzerDiagnosticsAsync();
    }
}
