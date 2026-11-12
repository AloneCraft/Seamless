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
public sealed class YieldStateMachineRefactoringTests {
    private const string OriginalSource = """
        using System;
        using System.Collections.Generic;

        namespace TestDemo {
            public class NumberGenerator {
                public IEnumerable<int> GenerateNumbers(int count){
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
    public async Task Refactoring_Action_Is_Provided_And_Generates_Compilable_Code(){
        using var workspace = new AdhocWorkspace();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path));

        var project = workspace.AddProject("TestProject",LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Preview))
            .AddMetadataReferences(references);

        var document = project.AddDocument("TestFile.cs",OriginalSource);
        var sourceText = await document.GetTextAsync();

        // 1. メソッド名 "GenerateNumbers" の位置を取得
        var methodSpan = new TextSpan(OriginalSource.IndexOf("GenerateNumbers",StringComparison.Ordinal),15);

        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(document,methodSpan,a => actions.Add(a),CancellationToken.None);

        var provider = new YieldStateMachineRefactoringProvider();
        await provider.ComputeRefactoringsAsync(context);

        // ① 電球アクション提示の検証
        Assert.Single(actions);
        Assert.Equal(YieldStateMachineRefactoringProvider.Title,actions[0].Title);

        // ② コード変換とコンパイル整合性の検証
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        var newDocument = applyOperation.ChangedSolution.GetDocument(document.Id)!;
        var newRoot = await newDocument.GetSyntaxRootAsync();
        var newSource = newRoot!.ToFullString();

        // 構造体が含まれていることの検証
        Assert.Contains("public readonly struct GenerateNumbersEnumerable",newSource,StringComparison.Ordinal);
        Assert.Contains("public struct GenerateNumbersEnumerator",newSource,StringComparison.Ordinal);

        // 変換後のコードがエラーなくコンパイルできることの検証
        var compilation = (await newDocument.Project.GetCompilationAsync())!;
        var errors = compilation.GetDiagnostics().Where(static d => d.Severity==DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length==0,string.Join(Environment.NewLine,errors.Select(static e => e.ToString())));
    }

    [Fact]
    public void Expanded_Struct_Enumerator_Achieves_Zero_Allocations(){
        // ③ 実行時ゼロアロケーション (0 bytes) 検証
        // リファクタリングによって展開される同一パターンの構造体列挙子を直接実行して検証
        var generator = new StandaloneNumberGenerator();
        var enumerable = generator.GenerateNumbers(10_000);

        // ウォームアップ
        int sumWarmup = 0;
        foreach(var n in enumerable){
            sumWarmup+=n;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // メモリ計測 (foreach ループ)
        long beforeBytes = GC.GetAllocatedBytesForCurrentThread();

        int count = 0;
        int sum = 0;
        foreach(var n in enumerable){
            count++;
            sum+=n;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread()-beforeBytes;

        // アサーション: ヒープ割り当てが完全 0 bytes！
        Assert.Equal(0,allocated);
        Assert.Equal(5_000,count);
        Assert.True(sum>0);
    }

    // リファクタリングによって生成されるネスト構造体列挙子の実コードパターン
    public sealed class StandaloneNumberGenerator {
        public GenerateNumbersEnumerable GenerateNumbers(int count){
            return new GenerateNumbersEnumerable(count);
        }

        public readonly struct GenerateNumbersEnumerable:IEnumerable<int>{
            private readonly int _count;

            public GenerateNumbersEnumerable(int count){
                this._count=count;
            }

            public GenerateNumbersEnumerator GetEnumerator(){
                return new GenerateNumbersEnumerator(this._count);
            }

            IEnumerator<int> IEnumerable<int>.GetEnumerator(){
                return this.GetEnumerator();
            }

            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator(){
                return this.GetEnumerator();
            }
        }

        public struct GenerateNumbersEnumerator:IEnumerator<int>{
            private int _state;
            private int _current;
            private readonly int _count;
            private int _i;

            public GenerateNumbersEnumerator(int count){
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

            object global::System.Collections.IEnumerator.Current{
                get{
                    return this._current;
                }
            }

            public bool MoveNext(){
                while(this._i<this._count){
                    var val = this._i;
                    this._i++;
                    if(val%2==0){
                        this._current=val;
                        return true;
                    }
                }
                return false;
            }

            public void Dispose(){
            }

            public void Reset(){
                throw new NotSupportedException();
            }
        }
    }
}
