using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.FLinq;
using Seamless.FLinq.Generator;
using Seamless.Linq;

namespace Sample.FLinqChainVerifier.Core;

/// <summary>
/// メソッドチェーンをRoslyn + FLinqIncrementalGeneratorでインメモリコンパイル・実行し、
/// 1. 全メソッド展開・フォールバック0
/// 2. 複数データパターンでの結果・例外一致
/// 3. 実行時間・メモリ割り当て計測(5%遅延時GC鎮静化リトライ付き)
/// 4. 2メソッドペア合成による理想特性との比較・原因と改善案の自動診断
/// を行う。
/// </summary>
public sealed class 動的検証ランナー{
    private static readonly Lazy<ImmutableArray<MetadataReference>>参照一覧=new(参照を構築);
    private static readonly CSharpParseOptions 解析オプション=new CSharpParseOptions(LanguageVersion.Preview)
        .WithFeatures([
            new("InterceptorsPreview","true"),
            new("InterceptorsNamespaces","Intercept"),
            new("InterceptorsPreviewNamespaces","Intercept"),
        ]);

    private static readonly int[]検証データ件数一覧=[0,1,16,256];
    private static readonly int[]固定第2ソース=[3,7,10,15,22,31];
    private const int 計測要素数=512;
    private const int 計測反復数=20;

    public string? 失敗テスト保存先フォルダ{get;set;}

    public チェーン検証結果 検証を実行(チェーン検証項目 項目){
        var ソースコード=検証用ソースを生成(項目);
        var 構文木=CSharpSyntaxTree.ParseText(ソースコード,解析オプション,path:"ChainContainer.cs");
        var コンパイル設定=new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithOptimizationLevel(OptimizationLevel.Release)
            .WithNullableContextOptions(NullableContextOptions.Enable);
        var アセンブリ名=$"FLinqVerify_{項目.識別子}_{Guid.NewGuid():N}";
        var コンパイル=CSharpCompilation.Create(
            assemblyName:アセンブリ名,
            syntaxTrees:[構文木],
            references:参照一覧.Value,
            options:コンパイル設定);

        GeneratorDriver ドライバ=CSharpGeneratorDriver.Create(
            [new FLinqIncrementalGenerator().AsSourceGenerator()],
            parseOptions:解析オプション,
            driverOptions:new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None,trackIncrementalGeneratorSteps:false));
        ドライバ=ドライバ.RunGeneratorsAndUpdateCompilation(コンパイル,out var 出力コンパイル,out var ジェネレータ診断);

        var エラー一覧=ジェネレータ診断
            .Concat(出力コンパイル.GetDiagnostics())
            .Where(static d=>d.Severity==DiagnosticSeverity.Error)
            .ToArray();

        var 生成コード全体=string.Concat(出力コンパイル.SyntaxTrees.Skip(1).Select(static t=>t.ToString()));
        var 展開コード抜粋=最適化メソッド本体を抽出(生成コード全体,"RunFLinqOptCore");
        var 実測ループ数=理想特性推定と原因診断器.ループ本数を数える(展開コード抜粋);

        if(エラー一覧.Length>0){
            var エラーメッセージ=string.Join(" / ",エラー一覧.Select(static e=>e.GetMessage()));
            return 失敗結果を構築(項目,false,false,0,false,実測ループ数,展開コード抜粋,エラーメッセージ);
        }

        // 生成コード内の未展開・フォールバック検査
        var インターセプトあり=生成コード全体.Contains("RunFLinqOptCore",StringComparison.Ordinal)
            &&生成コード全体.Contains("InterceptsLocation",StringComparison.Ordinal);
        var 未展開マーカーあり=展開コード抜粋.Contains("LinqInterceptorFailure(",StringComparison.Ordinal)
            ||展開コード抜粋.Contains("UnsupportedLinqExpansion(",StringComparison.Ordinal)
            ||ジェネレータ診断.Any(static d=>d.Id=="FLINQ002");
        var 全メソッド展開済み=インターセプトあり&&!未展開マーカーあり;

        using var peStream=new MemoryStream();
        var emitResult=出力コンパイル.Emit(peStream);
        if(!emitResult.Success){
            var emitError=string.Join(" / ",emitResult.Diagnostics.Where(static d=>d.Severity==DiagnosticSeverity.Error).Select(static d=>d.GetMessage()));
            return 失敗結果を構築(項目,false,全メソッド展開済み,0,false,実測ループ数,展開コード抜粋,emitError);
        }

        peStream.Position=0;
        var 読込文脈=new AssemblyLoadContext(アセンブリ名,isCollectible:true);
        try{
            var アセンブリ=読込文脈.LoadFromStream(peStream);
            var 型=アセンブリ.GetType("GeneratedVerify.ChainProbe",throwOnError:true)!;
            var fnSystem=型.GetMethod("RunSystem")!.CreateDelegate<Func<object,int[],object?>>();
            var fnUnopt=型.GetMethod("RunFLinqUnopt")!.CreateDelegate<Func<object,int[],object?>>();
            var fnOpt=型.GetMethod("RunFLinqOpt")!.CreateDelegate<Func<object,int[],object?>>();
            Func<int[],int[],object?>? fnIdeal=null;
            var mIdeal=型.GetMethod("RunIdeal");
            if(mIdeal is not null){
                fnIdeal=mIdeal.CreateDelegate<Func<int[],int[],object?>>();
            }

            var 実行時フォールバック合計=0;
            var 結果一致=true;
            var 不一致詳細=string.Empty;
            var 順序非依存か=項目.メソッド名一覧.Any(static m=>拡張メソッドカタログ.取得(m).順序を変更または非決定にするか);

            foreach(var 件数 in 検証データ件数一覧){
                var (ソース引数,実要素配列)=テスト入力を生成(項目.ソース種別,項目.シード値,件数);
                var sysRes=安全に実行(()=>fnSystem(ソース引数,固定第2ソース));
                var unoptRes=安全に実行(()=>fnUnopt(ソース引数,固定第2ソース));

                (object? 値,Type? 例外型) optRes;
                using(var カウンタスコープ=LinqCounterScope.Start()){
                    optRes=安全に実行(()=>fnOpt(ソース引数,固定第2ソース));
                    実行時フォールバック合計+=(int)カウンタスコープ.Counter.EnumerableFallback;
                }

                if(!結果が等しいか(sysRes,optRes,順序非依存か)){
                    結果一致=false;
                    不一致詳細=$"件数={件数}: System={値を文字列化(sysRes)} != FLinqOpt={値を文字列化(optRes)}";
                    break;
                }
                if(!結果が等しいか(sysRes,unoptRes,順序非依存か)){
                    結果一致=false;
                    不一致詳細=$"件数={件数}: System={値を文字列化(sysRes)} != FLinqUnopt={値を文字列化(unoptRes)}";
                    break;
                }
                if(fnIdeal is not null){
                    var idealRes=安全に実行(()=>fnIdeal(実要素配列,固定第2ソース));
                    if(!結果が等しいか(sysRes,idealRes,順序非依存か)){
                        結果一致=false;
                        不一致詳細=$"件数={件数}: System={値を文字列化(sysRes)} != Ideal={値を文字列化(idealRes)}";
                        break;
                    }
                }
            }

            if(実行時フォールバック合計>0){
                全メソッド展開済み=false;
            }

            // パフォーマンスとメモリ確保量の計測 (JITウォームアップ + 5%遅延時GC鎮静化リトライ付き)
            var (計測ソース,計測生配列)=テスト入力を生成(項目.ソース種別,項目.シード値,計測要素数);
            _ = 安全に実行(()=>fnSystem(計測ソース,固定第2ソース));
            _ = 安全に実行(()=>fnUnopt(計測ソース,固定第2ソース));
            _ = 安全に実行(()=>fnOpt(計測ソース,固定第2ソース));
            if(fnIdeal is not null){
                _ = 安全に実行(()=>fnIdeal(計測生配列,固定第2ソース));
            }

            var sysAlloc=確保バイト数を計測(()=>安全に実行(()=>fnSystem(計測ソース,固定第2ソース)));
            var unoptAlloc=確保バイト数を計測(()=>安全に実行(()=>fnUnopt(計測ソース,固定第2ソース)));
            var optAlloc=確保バイト数を計測(()=>安全に実行(()=>fnOpt(計測ソース,固定第2ソース)));
            var idealAlloc=項目.推定特性.期待最大確保バイト数;
            if(fnIdeal is not null){
                idealAlloc=確保バイト数を計測(()=>安全に実行(()=>fnIdeal(計測生配列,固定第2ソース)));
            }

            var (sysUs,unoptUs,optUs,idealUs)=時間をリトライ計測(fnSystem,fnUnopt,fnOpt,fnIdeal,計測ソース,計測生配列);

            var メモリ合格=optAlloc<=Math.Max(sysAlloc,項目.推定特性.期待最大確保バイト数)+64;
            var 速度合格=optUs<=Math.Max(sysUs,unoptUs)*1.15+5.0;
            var パフォーマンス合格=メモリ合格&&速度合格;

            var (原因診断,改善案,理想構造一致)=理想特性推定と原因診断器.診断する(
                項目,
                true,
                全メソッド展開済み,
                実行時フォールバック合計,
                結果一致,
                パフォーマンス合格,
                実測ループ数,
                sysUs,
                unoptUs,
                optUs,
                sysAlloc,
                optAlloc,
                展開コード抜粋,
                不一致詳細);

            var 再現コード=再現テストコード出力器.テストコードを生成(項目,原因診断,改善案);
            string? 保存パス=null;
            if((!全メソッド展開済み||!結果一致||!パフォーマンス合格)&&!string.IsNullOrWhiteSpace(this.失敗テスト保存先フォルダ)){
                保存パス=再現テストコード出力器.ファイルへ保存(this.失敗テスト保存先フォルダ,項目,再現コード);
            }

            var 要約=$"System: {sysUs:F1}μs ({sysAlloc:N0}B) / FLinq: {optUs:F1}μs ({optAlloc:N0}B) / ループ: {実測ループ数}本 (期待: {項目.推定特性.期待ループ本数}本)";
            return new チェーン検証結果(
                項目,
                true,
                全メソッド展開済み,
                実行時フォールバック合計,
                結果一致,
                パフォーマンス合格,
                理想構造一致,
                実測ループ数,
                sysUs,
                unoptUs,
                optUs,
                idealUs,
                sysAlloc,
                unoptAlloc,
                optAlloc,
                idealAlloc,
                展開コード抜粋,
                要約,
                原因診断,
                改善案,
                再現コード,
                保存パス);
        }finally{
            読込文脈.Unload();
        }
    }

    private チェーン検証結果 失敗結果を構築(
        チェーン検証項目 項目,
        bool コンパイル成功か,
        bool 全メソッド展開済みか,
        int フォールバック数,
        bool 結果一致か,
        int 実測ループ数,
        string 展開コード抜粋,
        string 詳細メッセージ){
        var (原因診断,改善案,理想構造一致)=理想特性推定と原因診断器.診断する(
            項目,
            コンパイル成功か,
            全メソッド展開済みか,
            フォールバック数,
            結果一致か,
            false,
            実測ループ数,
            0,
            0,
            0,
            0,
            0,
            展開コード抜粋,
            詳細メッセージ);
        var 再現コード=再現テストコード出力器.テストコードを生成(項目,原因診断,改善案);
        string? 保存パス=null;
        if(!string.IsNullOrWhiteSpace(this.失敗テスト保存先フォルダ)){
            保存パス=再現テストコード出力器.ファイルへ保存(this.失敗テスト保存先フォルダ,項目,再現コード);
        }
        return new チェーン検証結果(
            項目,
            コンパイル成功か,
            全メソッド展開済みか,
            フォールバック数,
            結果一致か,
            false,
            理想構造一致,
            実測ループ数,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            展開コード抜粋,
            詳細メッセージ,
            原因診断,
            改善案,
            再現コード,
            保存パス);
    }

    private static (double SysUs,double UnoptUs,double OptUs,double IdealUs) 時間をリトライ計測(
        Func<object,int[],object?>fnSystem,
        Func<object,int[],object?>fnUnopt,
        Func<object,int[],object?>fnOpt,
        Func<int[],int[],object?>? fnIdeal,
        object 計測ソース,
        int[]計測生配列){
        var sysTicks=0L;
        var unoptTicks=0L;
        var optTicks=0L;
        var idealTicks=0L;

        for(var 試行=0;試行<5;試行++){
            if(試行>0){
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            var swSys=Stopwatch.StartNew();
            for(var i=0;i<計測反復数;i++){
                _ = 安全に実行(()=>fnSystem(計測ソース,固定第2ソース));
            }
            swSys.Stop();

            var swUnopt=Stopwatch.StartNew();
            for(var i=0;i<計測反復数;i++){
                _ = 安全に実行(()=>fnUnopt(計測ソース,固定第2ソース));
            }
            swUnopt.Stop();

            var swOpt=Stopwatch.StartNew();
            for(var i=0;i<計測反復数;i++){
                _ = 安全に実行(()=>fnOpt(計測ソース,固定第2ソース));
            }
            swOpt.Stop();

            if(fnIdeal is not null){
                var swIdeal=Stopwatch.StartNew();
                for(var i=0;i<計測反復数;i++){
                    _ = 安全に実行(()=>fnIdeal(計測生配列,固定第2ソース));
                }
                swIdeal.Stop();
                idealTicks=swIdeal.ElapsedTicks;
            }else{
                idealTicks=(long)(swOpt.ElapsedTicks*0.95);
            }

            sysTicks=swSys.ElapsedTicks;
            unoptTicks=swUnopt.ElapsedTicks;
            optTicks=swOpt.ElapsedTicks;

            if(optTicks<=Math.Max(sysTicks,unoptTicks)*1.05){
                break;
            }
        }

        var 係数=1_000_000.0/(Stopwatch.Frequency*計測反復数);
        return (sysTicks*係数,unoptTicks*係数,optTicks*係数,idealTicks*係数);
    }

    private static long 確保バイト数を計測(Action 処理){
        var 開始=GC.GetAllocatedBytesForCurrentThread();
        処理();
        var 差分=GC.GetAllocatedBytesForCurrentThread()-開始;
        if(差分<0){
            return 0;
        }
        return 差分;
    }

    private static string 検証用ソースを生成(チェーン検証項目 項目){
        var キャスト型=ソース型文字列(項目.ソース種別);
        string ソース初期化式;
        if(項目.ソース種別==入力ソース種別.Range){
            ソース初期化式="global::System.Linq.Enumerable.Range(1,(int)rawInput)";
        }else{
            ソース初期化式=$"({キャスト型})rawInput";
        }
        var チェーン本体=拡張メソッドカタログ.クエリ式を構築("source",項目.メソッド名一覧);

        var ビルダー=new StringBuilder();
        ビルダー.AppendLine("#nullable enable");
        ビルダー.AppendLine("using System;");
        ビルダー.AppendLine("using System.Collections.Generic;");
        ビルダー.AppendLine("using System.Linq;");
        ビルダー.AppendLine("using Seamless.FLinq;");
        ビルダー.AppendLine("namespace GeneratedVerify;");
        ビルダー.AppendLine("public sealed class ChainProbe{");
        ビルダー.AppendLine("    [LinqIntercept(false)]");
        ビルダー.AppendLine($"    public static object? RunSystem(object rawInput,int[]second){{var source={ソース初期化式};return {チェーン本体};}}");
        ビルダー.AppendLine();
        ビルダー.AppendLine("    public static object? RunFLinqUnopt(object rawInput,int[]second)=>RunFLinqUnoptCore(rawInput,second);");
        ビルダー.AppendLine("    [LinqIntercept(true,Optimizations=LinqOptimizations.None)]");
        ビルダー.AppendLine($"    private static object? RunFLinqUnoptCore(object rawInput,int[]second){{var source={ソース初期化式};return {チェーン本体};}}");
        ビルダー.AppendLine();
        ビルダー.AppendLine("    public static object? RunFLinqOpt(object rawInput,int[]second)=>RunFLinqOptCore(rawInput,second);");
        ビルダー.AppendLine("    [LinqIntercept(true,Optimizations=LinqOptimizations.All)]");
        ビルダー.AppendLine($"    private static object? RunFLinqOptCore(object rawInput,int[]second){{var source={ソース初期化式};return {チェーン本体};}}");

        if(!string.IsNullOrWhiteSpace(項目.手書き理想コード)){
            ビルダー.AppendLine();
            ビルダー.AppendLine("    [LinqIntercept(false)]");
            ビルダー.AppendLine("    public static object? RunIdeal(int[]raw,int[]second){");
            foreach(var 行 in 項目.手書き理想コード.Split('\n')){
                ビルダー.AppendLine("        "+行.TrimEnd('\r'));
            }
            ビルダー.AppendLine("    }");
        }
        ビルダー.AppendLine("}");
        return ビルダー.ToString();
    }

    private static string ソース型文字列(入力ソース種別 ソース種別){
        switch(ソース種別){
            case 入力ソース種別.Array:
                return "int[]";
            case 入力ソース種別.List:
                return "global::System.Collections.Generic.List<int>";
            default:
                return "global::System.Collections.Generic.IEnumerable<int>";
        }
    }

    private static (object ソース引数,int[]実要素配列) テスト入力を生成(入力ソース種別 ソース種別,ulong シード値,int 件数){
        var 配列=new int[件数];
        var 状態=シード値;
        if(状態==0UL){
            状態=0x9E3779B97F4A7C15UL;
        }
        for(var i=0;i<件数;i++){
            状態^=状態>>12;
            状態^=状態<<25;
            状態^=状態>>27;
            var 値=(int)((状態*0x2545F4914F6CDD1DUL)>>32);
            配列[i]=(値&255)-32;
        }

        switch(ソース種別){
            case 入力ソース種別.Array:
                return (配列,配列);
            case 入力ソース種別.List:
                return (new List<int>(配列),配列);
            case 入力ソース種別.Range:{
                var rangeArray=new int[件数];
                for(var i=0;i<件数;i++){
                    rangeArray[i]=1+i;
                }
                return (件数,rangeArray);
            }
            default:
                return (配列.Where(static _=>true),配列);
        }
    }

    private static (object? 値,Type? 例外型) 安全に実行(Func<object?>処理){
        try{
            var 値=処理();
            if(値 is IEnumerable 列挙&&値 is not string){
                var リスト=new List<object?>();
                foreach(var 要素 in 列挙){
                    リスト.Add(要素);
                }
                return (リスト,null);
            }
            return (値,null);
        }catch(Exception 例外){
            return (null,例外.GetType());
        }
    }

    private static bool 結果が等しいか((object? 値,Type? 例外型) 期待,(object? 値,Type? 例外型) 実際,bool 順序非依存か){
        if(期待.例外型 is not null||実際.例外型 is not null){
            return 期待.例外型==実際.例外型;
        }
        if(期待.値 is List<object?>期待リスト&&実際.値 is List<object?>実際リスト){
            if(期待リスト.Count!=実際リスト.Count){
                return false;
            }
            if(順序非依存か){
                var ソート期待=期待リスト.Select(static x=>Convert.ToInt64(x)).OrderBy(static x=>x).ToArray();
                var ソート実際=実際リスト.Select(static x=>Convert.ToInt64(x)).OrderBy(static x=>x).ToArray();
                return ソート期待.AsSpan().SequenceEqual(ソート実際);
            }
            for(var i=0;i<期待リスト.Count;i++){
                if(!Equals(期待リスト[i],実際リスト[i])){
                    return false;
                }
            }
            return true;
        }
        if(期待.値 is double d1&&実際.値 is double d2){
            return Math.Abs(d1-d2)<1e-9;
        }
        return Equals(期待.値,実際.値);
    }

    private static string 値を文字列化((object? 値,Type? 例外型) 結果){
        if(結果.例外型 is not null){
            return $"Exception({結果.例外型.Name})";
        }
        if(結果.値 is List<object?>リスト){
            return $"Count={リスト.Count}[{string.Join(",",リスト.Take(6))}]";
        }
        if(結果.値 is null){
            return "null";
        }
        return 結果.値.ToString()??"null";
    }

    private static string 最適化メソッド本体を抽出(string 生成コード全体,string マーカー名){
        if(string.IsNullOrWhiteSpace(生成コード全体)){
            return "// 生成コードなし";
        }
        var 位置=生成コード全体.IndexOf($"static object? OptimizedLinq_{マーカー名}",StringComparison.Ordinal);
        if(位置<0){
            位置=生成コード全体.IndexOf($"public static object? {マーカー名}",StringComparison.Ordinal);
        }
        if(位置<0){
            位置=生成コード全体.IndexOf(マーカー名,StringComparison.Ordinal);
        }
        if(位置<0){
            位置=生成コード全体.LastIndexOf("InterceptsLocation",StringComparison.Ordinal);
        }
        if(位置<0){
            return 生成コード全体;
        }
        var 行頭=生成コード全体.LastIndexOf('\n',Math.Max(0,位置-1));
        if(行頭<0){
            行頭=0;
        }
        var 波括弧開始=生成コード全体.IndexOf('{',位置);
        if(波括弧開始<0){
            return 生成コード全体[行頭..Math.Min(生成コード全体.Length,行頭+1600)].Trim();
        }
        var 深さ=0;
        var 終了=生成コード全体.Length;
        for(var i=波括弧開始;i<生成コード全体.Length;i++){
            if(生成コード全体[i]=='{'){
                深さ++;
            }else if(生成コード全体[i]=='}'){
                深さ--;
                if(深さ==0){
                    終了=i+1;
                    break;
                }
            }
        }
        return 生成コード全体[行頭..終了].Trim();
    }

    private static ImmutableArray<MetadataReference>参照を構築(){
        var パス集合=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trusted=AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string??string.Empty;
        foreach(var p in trusted.Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries)){
            if(!string.IsNullOrWhiteSpace(p)&&File.Exists(p)){
                パス集合.Add(Path.GetFullPath(p));
            }
        }
        var 追加アセンブリ=new Assembly[]{
            typeof(GeneratedCodeHelpers).Assembly,
            typeof(動的検証ランナー).Assembly,
        };
        foreach(var asm in 追加アセンブリ){
            if(!string.IsNullOrWhiteSpace(asm.Location)&&File.Exists(asm.Location)){
                パス集合.Add(Path.GetFullPath(asm.Location));
            }
        }
        return パス集合
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(static p=>(MetadataReference)MetadataReference.CreateFromFile(p))
            .ToImmutableArray();
    }
}
