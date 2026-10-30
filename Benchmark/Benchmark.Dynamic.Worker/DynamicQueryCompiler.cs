using Benchmark.Dynamic.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Benchmark.Dynamic.Worker;

public enum DynamicImplementationKind {
    System,
    FLinqUnoptimized,
    FLinq,
    ZLinq,
}

public sealed record DynamicCompilationTimings(
    long SourceTextNanoseconds,
    long SyntaxTreeNanoseconds,
    long CompilationNanoseconds,
    long GeneratorNanoseconds,
    long EmitNanoseconds,
    long LoadNanoseconds,
    long BindNanoseconds) {
    public long TotalNanoseconds=>
        this.SourceTextNanoseconds+
        this.SyntaxTreeNanoseconds+
        this.CompilationNanoseconds+
        this.GeneratorNanoseconds+
        this.EmitNanoseconds+
        this.LoadNanoseconds+
        this.BindNanoseconds;
}

public sealed record DynamicCompilationDiagnostic(
    string Stage,
    string Severity,
    string ID,
    string Message,
    string Location);

/// <summary>
/// FLinq は展開できない呼び出しを黙って System.Linq へ切り替えず、警告 FLINQ002 を出して
/// 実行すると例外で止まるコードを生成する。実行前に、その兆候が生成物に無いことを確かめる。
/// </summary>
public static class FLinqExpansionValidator {
    public const string DiagnosticId="FLinqExpansionIncomplete";

    private static readonly string[] FailureMarkers=["LinqInterceptorFailure(","UnsupportedLinqExpansion("];

    public static IEnumerable<DynamicCompilationDiagnostic> Validate(
        IReadOnlyList<DynamicCompilationDiagnostic> diagnostics,
        string generatedSourceText) {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(generatedSourceText);
        foreach(var diagnostic in diagnostics.Where(static diagnostic=>diagnostic.ID=="FLINQ002")) {
            yield return new("Expansion","Error",DiagnosticId,$"FLINQ002: {diagnostic.Message}",diagnostic.Location);
        }
        foreach(var marker in FailureMarkers) {
            if(generatedSourceText.Contains(marker,StringComparison.Ordinal)) {
                yield return new("Expansion","Error",DiagnosticId,$"生成コードに{marker}が含まれます。FLinq が展開しきれませんでした。",string.Empty);
            }
        }
    }
}

public sealed record DynamicCompilationRequest(
    string AssemblyName,
    string TypeName,
    string Source,
    string SourcePath);

public sealed record DynamicCompilationResult(
    bool Success,
    DynamicCompilationTimings Timings,
    IReadOnlyList<DynamicCompilationDiagnostic> Diagnostics,
    IReadOnlyList<string> OptimizationTracePayloads,
    LoadedDynamicQuery? LoadedQuery,
    string ExpandedSource="");

public sealed class LoadedDynamicQuery {
    private DynamicAssemblyLoadContext? 読込コンテキスト;
    private Assembly? 動的アセンブリ;
    private Dictionary<DynamicImplementationKind,Func<object?,object?,DynamicQueryValue>>? 実行関数一覧;

    internal LoadedDynamicQuery(
        DynamicAssemblyLoadContext 読込コンテキスト,
        Assembly 動的アセンブリ,
        Dictionary<DynamicImplementationKind,Func<object?,object?,DynamicQueryValue>> 実行関数一覧) {
        this.読込コンテキスト=読込コンテキスト;
        this.動的アセンブリ=動的アセンブリ;
        this.実行関数一覧=実行関数一覧;
    }

    public DynamicQueryValue Run(DynamicImplementationKind 実装,object? Primary,object? Secondary) {
        var 関数=this.実行関数一覧??throw new ObjectDisposedException(nameof(LoadedDynamicQuery));
        return 関数[実装](Primary,Secondary);
    }

    /// <summary>読み込んだ動的アセンブリ。再現テストを実行するテストが、生成した型を直接呼ぶために使う。</summary>
    public Assembly? DynamicAssembly=>this.動的アセンブリ;

    public bool Has(DynamicImplementationKind 実装) {
        var 関数=this.実行関数一覧??throw new ObjectDisposedException(nameof(LoadedDynamicQuery));
        return 関数.ContainsKey(実装);
    }

    public Func<object?,object?,DynamicQueryValue> GetDelegate(DynamicImplementationKind 実装) {
        var 関数=this.実行関数一覧??throw new ObjectDisposedException(nameof(LoadedDynamicQuery));
        return 関数[実装];
    }

    public AssemblyUnloadResult UnloadAndVerify() {
        var 参照=this.ReleaseForUnload();
        return 参照 is null
            ?new(true,0)
            :DynamicAssemblyUnloader.Verify(参照);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private WeakReference? ReleaseForUnload() {
        var コンテキスト=this.読込コンテキスト;
        if(コンテキスト is null) {
            return null;
        }
        this.実行関数一覧=null;
        this.動的アセンブリ=null;
        this.読込コンテキスト=null;
        return DynamicAssemblyUnloader.Release(コンテキスト);
    }
}

public sealed class DynamicQueryCompiler {
    private static readonly HashSet<string> QueryMethodNames=new(StringComparer.Ordinal){
        "AsValueEnumerable","Range",
        "Where","Select","Distinct","DistinctBy","Take","Skip","Reverse",
        "OrderBy","OrderByDescending","ThenBy","ThenByDescending",
        "Concat","Union","Except","Intersect","UnionBy","ExceptBy","IntersectBy",
        "SelectMany","Join","GroupJoin",
        "Count","LongCount","Any","All","Contains","Sum","Min","Max","MinBy","MaxBy","Average","Aggregate",
        "First","FirstOrDefault","Last","LastOrDefault","ElementAt","ElementAtOrDefault",
        "ToArray","ToList","ToHashSet","ToDictionary","ToLookup",
    };
    private static readonly CSharpParseOptions 解析設定=new CSharpParseOptions(
        LanguageVersion.Preview,
        DocumentationMode.Parse,
        SourceCodeKind.Regular)
        .WithFeatures([
            new("InterceptorsPreview","true"),
            new("InterceptorsNamespaces","Intercept"),
            new("InterceptorsPreviewNamespaces","Intercept"),
        ]);
    private readonly MetadataReferenceProvider 参照提供;

    public DynamicQueryCompiler(MetadataReferenceProvider? 参照提供=null) {
        this.参照提供=参照提供??new MetadataReferenceProvider();
    }

    public MetadataReferenceSnapshot MetadataReferences=>this.参照提供.Get();

    public DynamicCompilationResult Compile(DynamicCompilationRequest 要求,CancellationToken キャンセルToken=default) {
        ArgumentNullException.ThrowIfNull(要求);
        var 診断一覧=new List<DynamicCompilationDiagnostic>();
        var Trace一覧=new List<string>();
        var 展開結果=string.Empty;
        var SourceText時間=0L;
        var SyntaxTree時間=0L;
        var Compilation時間=0L;
        var Generator時間=0L;
        var Emit時間=0L;
        var Load時間=0L;
        var Bind時間=0L;
        DynamicAssemblyLoadContext? 読込コンテキスト=null;
        try {
            var 開始=Stopwatch.GetTimestamp();
            var Source=SourceText.From(要求.Source,Encoding.UTF8);
            SourceText時間=経過ナノ秒(開始);

            開始=Stopwatch.GetTimestamp();
            var 構文木=CSharpSyntaxTree.ParseText(Source,解析設定,要求.SourcePath,キャンセルToken);
            SyntaxTree時間=経過ナノ秒(開始);

            開始=Stopwatch.GetTimestamp();
            var コンパイル設定=new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel:OptimizationLevel.Release,
                    allowUnsafe:false,
                    nullableContextOptions:NullableContextOptions.Enable)
                .WithDeterministic(true)
                .WithConcurrentBuild(false);
            var Compilation=CSharpCompilation.Create(
                要求.AssemblyName,
                [構文木],
                this.参照提供.Get().References,
                コンパイル設定);
            Compilation時間=経過ナノ秒(開始);

            開始=Stopwatch.GetTimestamp();
            GeneratorDriver Driver=CSharpGeneratorDriver.Create(
                [new global::Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],
                parseOptions:解析設定,
                driverOptions:new GeneratorDriverOptions(
                    IncrementalGeneratorOutputKind.None,
                    trackIncrementalGeneratorSteps:false));
            Driver=Driver.RunGeneratorsAndUpdateCompilation(Compilation,out var 出力Compilation,out var Generator診断,キャンセルToken);
            Generator時間=経過ナノ秒(開始);
            診断一覧.AddRange(Generator診断.Select(診断=>変換("Generator",診断)));
            var Run結果=Driver.GetRunResult();
            foreach(var 結果 in Run結果.Results) {
                診断一覧.AddRange(結果.Diagnostics.Select(診断=>変換("Generator",診断)));
                foreach(var 生成Source in 結果.GeneratedSources) {
                    診断一覧.Add(new(
                        "Generator",
                        "Info",
                        "GeneratedSource",
                        生成Source.HintName,
                        string.Empty));
                    foreach(var 行 in 生成Source.SourceText.Lines.Select(行=>行.ToString())) {
                        const string Prefix="// FLinqOptimizationTrace:";
                        if(行.StartsWith(Prefix,StringComparison.Ordinal)) {
                            Trace一覧.Add(行[Prefix.Length..].Trim());
                        }
                    }
                }
            }
            if(Trace一覧.Count==0) {
                診断一覧.Add(new(
                    "OptimizationTrace",
                    "Error",
                    "OptimizationTraceMissing",
                    "RunFLinqのopt-in最適化traceが生成されませんでした。",
                    string.Empty));
            }else if(Trace一覧.Any(static Payload=>!固定点到達(Payload))) {
                診断一覧.Add(new(
                    "OptimizationTrace",
                    "Error",
                    "OptimizationTraceNonConverged",
                    "FLinq最適化が固定点へ到達しなかったためEmitを中止しました。",
                    string.Empty));
            }
            展開結果=string.Join(
                Environment.NewLine,
                Run結果.Results.SelectMany(結果=>結果.GeneratedSources).Select(Source=>Source.SourceText.ToString()));
            診断一覧.AddRange(呼出先を検証(Compilation,構文木,Run結果));
            // Validate は診断一覧を遅延で読むので、AddRange(自分自身を書き換える)の前に配列へ確定させる。
            診断一覧.AddRange(FLinqExpansionValidator.Validate(
                診断一覧,
                string.Concat(Run結果.Results.SelectMany(結果=>結果.GeneratedSources).Select(Source=>Source.SourceText.ToString()))).ToArray());
            if(診断一覧.Any(診断=>診断.Severity=="Error")) {
                return 失敗();
            }

            開始=Stopwatch.GetTimestamp();
            using var PE=new MemoryStream();
            using var PDB=new MemoryStream();
            var Emit結果=出力Compilation.Emit(
                PE,
                PDB,
                options:new EmitOptions(debugInformationFormat:DebugInformationFormat.PortablePdb),
                cancellationToken:キャンセルToken);
            Emit時間=経過ナノ秒(開始);
            診断一覧.AddRange(Emit結果.Diagnostics.Select(診断=>変換("Emit",診断)));
            if(!Emit結果.Success) {
                return 失敗();
            }

            開始=Stopwatch.GetTimestamp();
            PE.Position=0;
            PDB.Position=0;
            読込コンテキスト=new DynamicAssemblyLoadContext(要求.AssemblyName);
            var Assembly=読込コンテキスト.LoadFromStream(PE,PDB);
            Load時間=経過ナノ秒(開始);

            開始=Stopwatch.GetTimestamp();
            var 型=Assembly.GetType(要求.TypeName,throwOnError:true,ignoreCase:false)
                ??throw new TypeLoadException(要求.TypeName);
            var 関数一覧=new Dictionary<DynamicImplementationKind,Func<object?,object?,DynamicQueryValue>> {
                [DynamicImplementationKind.System]=Bind(型,"RunSystem"),
                [DynamicImplementationKind.FLinqUnoptimized]=Bind(型,"RunFLinqUnoptimized"),
                [DynamicImplementationKind.FLinq]=Bind(型,"RunFLinq"),
            };
            if(型.GetMethod("RunZLinq",BindingFlags.Public|BindingFlags.Static) is not null) {
                関数一覧[DynamicImplementationKind.ZLinq]=Bind(型,"RunZLinq");
            }
            Bind時間=経過ナノ秒(開始);
            var Loaded=new LoadedDynamicQuery(読込コンテキスト,Assembly,関数一覧);
            読込コンテキスト=null;
            return new(true,時間(),診断一覧,Trace一覧,Loaded,展開結果);
        } catch(Exception 例外) when(例外 is not OperationCanceledException) {
            診断一覧.Add(new("Compiler","Error",例外.GetType().Name,例外.Message,string.Empty));
            if(読込コンテキスト is not null) {
                _=DynamicAssemblyUnloader.Release(読込コンテキスト);
            }
            return 失敗();
        }

        DynamicCompilationResult 失敗() {
            if(読込コンテキスト is not null) {
                _=DynamicAssemblyUnloader.Release(読込コンテキスト);
                読込コンテキスト=null;
            }
            return new(false,時間(),診断一覧,Trace一覧,null,展開結果);
        }

        DynamicCompilationTimings 時間() {
            return new(SourceText時間,SyntaxTree時間,Compilation時間,Generator時間,Emit時間,Load時間,Bind時間);
        }
    }

    private static Func<object?,object?,DynamicQueryValue> Bind(Type 型,string メソッド名) {
        var メソッド=型.GetMethod(
            メソッド名,
            BindingFlags.Public|BindingFlags.Static,
            binder:null,
            types:[typeof(object),typeof(object)],
            modifiers:null)??throw new MissingMethodException(型.FullName,メソッド名);
        if(メソッド.ReturnType!=typeof(DynamicQueryValue)) {
            throw new InvalidProgramException($"{メソッド名} must return {typeof(DynamicQueryValue).FullName}.");
        }
        return メソッド.CreateDelegate<Func<object?,object?,DynamicQueryValue>>();
    }

    private static IEnumerable<DynamicCompilationDiagnostic> 呼出先を検証(
        CSharpCompilation Compilation,
        SyntaxTree 構文木,
        GeneratorDriverRunResult Run結果) {
        var モデル=Compilation.GetSemanticModel(構文木,ignoreAccessibility:false);
        var Root=構文木.GetRoot();
        var メソッド一覧=Root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .Where(static メソッド=>メソッド.Identifier.ValueText is
                "RunSystem" or "RunZLinq" or "RunFLinqUnoptimizedCore" or "RunFLinqCore")
            .ToArray();
        foreach(var メソッド in メソッド一覧) {
            var 呼出一覧=メソッド.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>();
            var Query呼出一覧=呼出一覧
                .Select(呼出=>(呼出,Symbol:モデル.GetSymbolInfo(呼出).Symbol as IMethodSymbol))
                .Where(項目=>項目.Symbol is not null&&QueryMethodNames.Contains(項目.Symbol.Name))
                .ToArray();
            if(メソッド.Identifier.ValueText=="RunZLinq") {
                if(!Query呼出一覧.Any(項目=>項目.Symbol!.ContainingAssembly.Name=="ZLinq")) {
                    yield return new("SymbolValidation","Error","InvocationTargetMismatch","RunZLinqにZLinq呼出先がありません。",位置(メソッド.GetLocation()));
                }
                foreach(var 不正 in Query呼出一覧.Where(項目=>項目.Symbol!.ContainingAssembly.Name!="ZLinq")) {
                    yield return new(
                        "SymbolValidation",
                        "Error",
                        "InvocationTargetMismatch",
                        $"RunZLinqの{不正.Symbol!.Name}が想定外の{不正.Symbol.ContainingType.ToDisplayString()}へbindしました。",
                        位置(不正.呼出.GetLocation()));
                }
            }else{
                if(!Query呼出一覧.Any(項目=>項目.Symbol!.ContainingType.ToDisplayString()=="System.Linq.Enumerable")) {
                    yield return new("SymbolValidation","Error","InvocationTargetMismatch",$"{メソッド.Identifier.ValueText}の入力呼出先がSystem.Linq.Enumerableではありません。",位置(メソッド.GetLocation()));
                }
                foreach(var 不正 in Query呼出一覧.Where(項目=>項目.Symbol!.ContainingType.ToDisplayString()!="System.Linq.Enumerable")) {
                    yield return new(
                        "SymbolValidation",
                        "Error",
                        "InvocationTargetMismatch",
                        $"{メソッド.Identifier.ValueText}の{不正.Symbol!.Name}が想定外の{不正.Symbol.ContainingType.ToDisplayString()}へbindしました。",
                        位置(不正.呼出.GetLocation()));
                }
            }
        }
        var 生成文字列=string.Concat(
            Run結果.Results.SelectMany(結果=>結果.GeneratedSources).Select(Source=>Source.SourceText.ToString()));
        foreach(var メソッド名 in new[]{"RunFLinqUnoptimizedCore","RunFLinqCore"}) {
            if(!生成文字列.Contains(メソッド名,StringComparison.Ordinal)) {
                yield return new("SymbolValidation","Error","FLinqInterceptorMissing",$"{メソッド名}に対応するinterceptorが生成されませんでした。",string.Empty);
            }
        }
    }

    private static bool 固定点到達(string Payload) {
        try {
            var JSON=Encoding.UTF8.GetString(Convert.FromBase64String(Payload));
            using var Document=JsonDocument.Parse(JSON);
            return Document.RootElement.TryGetProperty("Summary",out var Summary)
                &&Summary.TryGetProperty("ReachedFixedPoint",out var FixedPoint)
                &&FixedPoint.ValueKind==JsonValueKind.True;
        }catch(Exception 例外) when(例外 is FormatException or JsonException) {
            return false;
        }
    }

    private static DynamicCompilationDiagnostic 変換(string 段階,Diagnostic 診断) {
        return new(段階,診断.Severity.ToString(),診断.Id,診断.GetMessage(),位置(診断.Location));
    }

    private static string 位置(Location 位置) {
        if(!位置.IsInSource) {
            return string.Empty;
        }
        var Span=位置.GetLineSpan();
        return $"{Span.Path}({Span.StartLinePosition.Line+1},{Span.StartLinePosition.Character+1})";
    }

    private static long 経過ナノ秒(long 開始) {
        return (long)((Stopwatch.GetTimestamp()-開始)*(1_000_000_000d/Stopwatch.Frequency));
    }
}
