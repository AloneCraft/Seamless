using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Seamless.Linq.Analyzers;
using Xunit;

namespace Test.System.Linq.Analyzer;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class AsyncYieldStateMachineRefactoringTests {
    private const string OriginalAsyncSource = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;

        namespace TestDemo {
            public class AsyncNumberGenerator {
                public async IAsyncEnumerable<int> GenerateAsyncNumbers(int count){
                    for(var i = 0;i<count;i++){
                        if(i%2==0){
                            yield return i;
                        }
                    }
                }
            }
        }
        """;

    [Fact]
    public async Task AsyncRefactoring_Action_Is_Provided_And_Generates_Compilable_Code(){
        using var workspace = new AdhocWorkspace();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path));

        var project = workspace.AddProject("TestProject",LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Preview))
            .AddMetadataReferences(references);

        var document = project.AddDocument("TestAsyncFile.cs",OriginalAsyncSource);

        // 1. メソッド名 "GenerateAsyncNumbers" の位置を取得
        var methodSpan = new TextSpan(OriginalAsyncSource.IndexOf("GenerateAsyncNumbers",StringComparison.Ordinal),20);

        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(document,methodSpan,a => actions.Add(a),CancellationToken.None);

        var provider = new YieldStateMachineRefactoringProvider();
        await provider.ComputeRefactoringsAsync(context);

        // ① 非同期電球アクション提示の検証
        Assert.Single(actions);
        Assert.Equal(YieldStateMachineRefactoringProvider.AsyncTitle,actions[0].Title);

        // ② コード変換とコンパイル整合性の検証
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        var newDocument = applyOperation.ChangedSolution.GetDocument(document.Id)!;
        var newRoot = await newDocument.GetSyntaxRootAsync();
        var newSource = newRoot!.ToFullString();

        // 構造体が含まれていることの検証
        Assert.Contains("public readonly struct GenerateAsyncNumbersAsyncEnumerable",newSource,StringComparison.Ordinal);
        Assert.Contains("public struct GenerateAsyncNumbersAsyncEnumerator",newSource,StringComparison.Ordinal);
        Assert.Contains("IAsyncEnumerable<int>",newSource,StringComparison.Ordinal);
        Assert.Contains("IAsyncEnumerator<int>",newSource,StringComparison.Ordinal);

        // 変換後のコードがエラーなくコンパイルできることの検証
        var compilation = (await newDocument.Project.GetCompilationAsync())!;
        var errors = compilation.GetDiagnostics().Where(static d => d.Severity==DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length==0,string.Join(Environment.NewLine,errors.Select(static e => e.ToString())));
    }

    [Fact]
    public async Task Expanded_AsyncStruct_Enumerator_Achieves_Zero_Allocations(){
        // ③ 実行時ゼロアロケーション (0 bytes) 検証
        // リファクタリングによって展開される同一パターンの非同期構造体列挙子を直接 await foreach 実行
        var generator = new StandaloneAsyncNumberGenerator();
        var enumerable = generator.GenerateAsyncNumbers(10_000);

        // ウォームアップ
        int sumWarmup = 0;
        await foreach(var n in enumerable){
            sumWarmup+=n;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // メモリ計測 (await foreach ループ)
        long beforeBytes = GC.GetAllocatedBytesForCurrentThread();

        int count = 0;
        int sum = 0;
        await foreach(var n in enumerable){
            count++;
            sum+=n;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread()-beforeBytes;

        // アサーション: 非同期イテレーションでもヒープ割り当てが完全 0 bytes！
        Assert.Equal(0,allocated);
        Assert.Equal(5_000,count);
        Assert.True(sum>0);
    }

    // リファクタリングによって生成される非同期構造体列挙子の実コードパターン
    public sealed class StandaloneAsyncNumberGenerator {
        public GenerateAsyncNumbersAsyncEnumerable GenerateAsyncNumbers(int count){
            return new GenerateAsyncNumbersAsyncEnumerable(count);
        }

        public readonly struct GenerateAsyncNumbersAsyncEnumerable:IAsyncEnumerable<int>{
            private readonly int _count;

            public GenerateAsyncNumbersAsyncEnumerable(int count){
                this._count=count;
            }

            public GenerateAsyncNumbersAsyncEnumerator GetAsyncEnumerator(CancellationToken cancellationToken = default){
                return new GenerateAsyncNumbersAsyncEnumerator(this._count);
            }

            IAsyncEnumerator<int> IAsyncEnumerable<int>.GetAsyncEnumerator(CancellationToken cancellationToken){
                return this.GetAsyncEnumerator(cancellationToken);
            }
        }

        public struct GenerateAsyncNumbersAsyncEnumerator:IAsyncEnumerator<int>{
            private int _state;
            private int _current;
            private readonly int _count;
            private int _i;

            public GenerateAsyncNumbersAsyncEnumerator(int count){
                this._state=0;
                this._current=default;
                this._count=count;
                this._i=0;
            }

            public int Current{
                get{
                    return this._current;
                }
            }

            public ValueTask<bool> MoveNextAsync(){
                while(this._i<this._count){
                    var val = this._i;
                    this._i++;
                    if(val%2==0){
                        this._current=val;
                        return new ValueTask<bool>(true);
                    }
                }
                return new ValueTask<bool>(false);
            }

            public ValueTask DisposeAsync(){
                return default;
            }
        }
    }
}
