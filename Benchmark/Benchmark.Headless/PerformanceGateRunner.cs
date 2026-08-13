using System.Text.Json;
using UI;

namespace Benchmark.Headless;

internal static class PerformanceGateRunner{
    private static readonly JsonSerializerOptions JSONオプション=new(){
        PropertyNameCaseInsensitive=true,
    };

    public static async Task<int>実行Async(IReadOnlyList<string>引数一覧,CancellationToken キャンセルトークン){
#if DEBUG
        throw new InvalidOperationException("性能ゲートはReleaseビルドでのみ実行できます。");
#else
        var マニフェストパス=オプション値を取得(引数一覧,"--performance-gate")
            ??throw new ArgumentException("--performance-gate <manifest.json> を指定してください。");
        マニフェストパス=Path.GetFullPath(マニフェストパス);
        if(!File.Exists(マニフェストパス)){
            throw new FileNotFoundException("性能ゲートマニフェストがありません。",マニフェストパス);
        }
        var マニフェスト=JsonSerializer.Deserialize<ベンチマーク性能ゲートマニフェスト>(
            await File.ReadAllTextAsync(マニフェストパス,キャンセルトークン),JSONオプション)
            ??throw new InvalidDataException("性能ゲートマニフェストを読み込めませんでした。");
        var 対象一覧=ベンチマーク性能ゲートロジック.検証して解決(マニフェスト,ベンチマークカタログ.読み込む());
        var DBパス=Path.Combine(Directory.GetCurrentDirectory(),"Work","UI","Benchmark.db");
        PerformanceGateDatabase.比較元を検証(DBパス,マニフェスト);
        var 論理CPU=ベンチマーク性能ゲートCPU選択.実機から解決()
            ??throw new InvalidOperationException("アフィニティ対応Pコアの主論理スレッドを解決できませんでした。代替CPUでは実行しません。");
        var 退役実行ID=退役実行IDを読む(引数一覧,マニフェスト.比較元実行ID);
        if(引数一覧.Contains("--dry-run",StringComparer.OrdinalIgnoreCase)){
            var 退役表示=退役実行ID?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??"<none>";
            Console.WriteLine($"performance-gate targets={対象一覧.Count} cpu={論理CPU} N={マニフェスト.要素数} target-ms={マニフェスト.目標ミリ秒} iterations={マニフェスト.反復回数} retire={退役表示}");
            return 0;
        }

        var 実行基準ルート=Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),"artifacts","performance-gate",
            成果物ルート名を作成(DateTimeOffset.UtcNow,Guid.NewGuid())));
        Directory.CreateDirectory(実行基準ルート);
        var 全測定一覧=new List<ベンチマーク性能ゲート測定>(対象一覧.Count*マニフェスト.反復回数);
        IReadOnlyList<PerformanceGatePairResult>?最終結果一覧=null;
        string?最終成果物ルート=null;
        for(var 反復番号=1;反復番号<=マニフェスト.反復回数;反復番号++){
            var 反復ルート=Path.Combine(実行基準ルート,"attempt-"+反復番号.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
            try{
                Console.WriteLine($"performance-gate attempt={反復番号}/{マニフェスト.反復回数} cpu={論理CPU}");
                var 反復結果一覧=await 一回を測定Async(対象一覧,反復番号,論理CPU,マニフェスト,反復ルート,キャンセルトークン);
                全測定一覧.AddRange(反復結果一覧.Select(static 結果=>結果.測定));
                if(反復番号==マニフェスト.反復回数){
                    最終結果一覧=反復結果一覧;
                    最終成果物ルート=反復ルート;
                }
            }finally{
                if(反復番号<マニフェスト.反復回数){
                    一時成果物を削除(実行基準ルート,反復ルート);
                }
            }
        }

        var 判定=ベンチマーク性能ゲートロジック.判定(マニフェスト,全測定一覧);
        foreach(var 対象判定 in 判定.対象判定一覧){
            var 中央値表示=対象判定.比率中央値?.ToString("F6",global::System.Globalization.CultureInfo.InvariantCulture)??"-";
            var シェア表示=対象判定.最終シェア?.ToString("F6",global::System.Globalization.CultureInfo.InvariantCulture)??"-";
            Console.WriteLine(
                $"gate target={対象判定.対象ID} pass={対象判定.合格か} median-ratio={中央値表示} final-share={シェア表示}"
                +(対象判定.失敗理由一覧.Count==0 ? string.Empty : " failures="+string.Join(" | ",対象判定.失敗理由一覧)));
        }
        if(!判定.合格か){
            Console.Error.WriteLine("性能ゲートは不合格です。Benchmark.dbは変更していません。");
            return 1;
        }
        if(最終結果一覧 is null||最終成果物ルート is null){
            throw new InvalidOperationException("5回目の性能ゲート結果がありません。");
        }
        if(退役実行ID is { }実行ID){
            var 保存結果=PerformanceGateDatabaseBoundary.判定後更新を実行(
                判定,
                退役実行ID,
                ()=>PerformanceGateDatabase.合格結果を保存して比較元を退役(
                    DBパス,マニフェスト,最終結果一覧,論理CPU,最終成果物ルート,実行ID))!;
            Console.WriteLine($"saved-run={保存結果.新実行ID} retired-run={実行ID} backup={保存結果.バックアップパス}");
        }else{
            Console.WriteLine("性能ゲートは合格しました。--retire-run-on-pass がないためBenchmark.dbは変更していません。");
        }
        return 0;
#endif
    }

    internal static string 成果物ルート名を作成(DateTimeOffset 日時,Guid 一意ID)=>
        日時.ToString("yyyyMMdd-HHmmss-fff",global::System.Globalization.CultureInfo.InvariantCulture)
        +"-"+一意ID.ToString("N",global::System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<IReadOnlyList<PerformanceGatePairResult>>一回を測定Async(
        IReadOnlyList<ベンチマーク性能ゲート解決対象>対象一覧,
        int 反復番号,
        int 論理CPU,
        ベンチマーク性能ゲートマニフェスト マニフェスト,
        string 反復ルート,
        CancellationToken キャンセルトークン){
        var 結果一覧=new List<PerformanceGatePairResult>(対象一覧.Count);
        foreach(var タブ組 in 対象一覧.GroupBy(static 対象=>対象.タブ.ID,StringComparer.Ordinal)){
            var タブ対象一覧=タブ組.ToArray();
            var 受信一覧=new List<ベンチマーク結果項目>();
            var タブ定義=タブ対象一覧[0].タブ;
            var タブルート=Path.Combine(反復ルート,ベンチマークソースタブ一覧.パス部分に変換(タブ定義.タブ));
            var サービス=new ベンチマーク実行サービス(new ベンチマークワーカープロセスサービス());
            await サービス.実行Async(
                タブ対象一覧.SelectMany(static 対象=>new[]{対象.FLinq,対象.基準}).ToArray(),
                [論理CPU],
                ()=>[論理CPU],
                [マニフェスト.要素数],
                タブ定義.タブ,
                ベンチマーク実行プロファイル.正確,
                TimeSpan.FromMilliseconds(マニフェスト.目標ミリ秒),
                false,
                タブルート,
                null,
                受信一覧.Add,
                状態=>Console.WriteLine($"performance-gate attempt={反復番号} tab={タブ定義.ID} status={状態}"),
                null,
                キャンセルトークン);
            foreach(var 対象 in タブ対象一覧){
                var FLinq=最終結果を取得(受信一覧,対象.FLinq,対象.定義.ID,"FLinq");
                var 基準=最終結果を取得(受信一覧,対象.基準,対象.定義.ID,"基準");
                if(FLinq.割り当て論理CPU!=論理CPU||基準.割り当て論理CPU!=論理CPU){
                    throw new InvalidDataException("性能ゲート測定が指定Pコアで実行されませんでした。ID="+対象.定義.ID+" expected="+論理CPU);
                }
                if(!FLinq.アフィニティ適用成功か||!基準.アフィニティ適用成功か){
                    throw new InvalidDataException("アフィニティ適用成功を確認できません。ID="+対象.定義.ID);
                }
                var 測定=new ベンチマーク性能ゲート測定(
                    対象.定義.ID,反復番号,
                    FLinq.平均ナノ秒??double.NaN,FLinq.割り当てバイト数値??-1,FLinq.結果ダイジェスト,FLinq.割り当て論理CPU,FLinq.ループ回数,FLinq.アフィニティ適用成功か,
                    基準.平均ナノ秒??double.NaN,基準.割り当てバイト数値??-1,基準.結果ダイジェスト,基準.割り当て論理CPU,基準.ループ回数,基準.アフィニティ適用成功か);
                結果一覧.Add(new(対象,測定,FLinq,基準));
            }
        }
        if(結果一覧.Count!=対象一覧.Count){
            throw new InvalidDataException("性能ゲートの反復結果が11件揃いませんでした。件数="+結果一覧.Count);
        }
        return 結果一覧;
    }

    private static ベンチマーク結果項目 最終結果を取得(
        IReadOnlyList<ベンチマーク結果項目>受信一覧,
        ベンチマークケース ケース,
        string 対象ID,
        string 側名){
        var 候補一覧=受信一覧.Where(結果=>
            string.Equals(結果.プロジェクト,ケース.プロジェクト,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(結果.型完全名,ケース.型完全名,StringComparison.Ordinal)
            &&string.Equals(結果.メソッド,ケース.メソッド名,StringComparison.Ordinal)
            &&string.Equals(結果.引数キー,ケース.引数キー,StringComparison.Ordinal)).ToArray();
        var 結果=候補一覧.LastOrDefault(static 結果=>結果.平均ナノ秒 is not null&&結果.終了コード==0);
        if(結果 is null){
            throw new InvalidDataException("性能ゲート測定が完了しませんでした。ID="+対象ID+" 側="+側名+" 理由="+(候補一覧.LastOrDefault()?.失敗理由??"結果なし"));
        }
        return 結果;
    }

    private static long?退役実行IDを読む(IReadOnlyList<string>引数一覧,long 比較元実行ID){
        var 文字列=オプション値を取得(引数一覧,"--retire-run-on-pass");
        if(文字列 is null){
            return null;
        }
        if(!long.TryParse(文字列,global::System.Globalization.NumberStyles.None,global::System.Globalization.CultureInfo.InvariantCulture,out var 値)
            ||値!=比較元実行ID){
            throw new ArgumentException("--retire-run-on-pass はマニフェストの比較元実行IDと一致する必要があります。");
        }
        return 値;
    }

    private static string?オプション値を取得(IReadOnlyList<string>引数一覧,string オプション){
        for(var 位置=0;位置<引数一覧.Count;位置++){
            if(string.Equals(引数一覧[位置],オプション,StringComparison.OrdinalIgnoreCase)){
                if(位置+1>=引数一覧.Count||引数一覧[位置+1].StartsWith("--",StringComparison.Ordinal)){
                    return null;
                }
                return 引数一覧[位置+1];
            }
        }
        return null;
    }

    private static void 一時成果物を削除(string 実行基準ルート,string 一時ルート){
        var 基準=Path.GetFullPath(実行基準ルート).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var 対象=Path.GetFullPath(一時ルート);
        if(!対象.StartsWith(基準,StringComparison.OrdinalIgnoreCase)){
            throw new InvalidOperationException("性能ゲートの一時成果物が実行ルート外です。対象="+対象);
        }
        if(Directory.Exists(対象)){
            Directory.Delete(対象,true);
        }
    }
}

internal sealed record PerformanceGatePairResult(
    ベンチマーク性能ゲート解決対象 対象,
    ベンチマーク性能ゲート測定 測定,
    ベンチマーク結果項目 FLinq,
    ベンチマーク結果項目 基準);
