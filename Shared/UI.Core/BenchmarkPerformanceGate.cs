namespace UI;

public sealed record ベンチマーク性能ゲートマニフェスト(
    int 版,
    long 比較元実行ID,
    int 要素数,
    double 目標ミリ秒,
    int 反復回数,
    IReadOnlyList<ベンチマーク性能ゲート対象>対象一覧);

public sealed record ベンチマーク性能ゲート対象(
    string ID,
    string タブID,
    string 表示型,
    string メソッド名,
    string 引数キー,
    string 左型完全名,
    string 右型完全名,
    long Run249FLinq割当上限);

public sealed record ベンチマーク性能ゲート解決対象(
    ベンチマーク性能ゲート対象 定義,
    ベンチマークソースタブ定義 タブ,
    ベンチマークケース FLinq,
    ベンチマークケース 基準);

public sealed record ベンチマーク性能ゲート測定(
    string 対象ID,
    int 反復番号,
    double FLinqナノ秒,
    long FLinq割当バイト,
    string FLinq結果ダイジェスト,
    int? FLinq論理CPU,
    int FLinqループ回数,
    bool FLinqアフィニティ適用成功か,
    double 基準ナノ秒,
    long 基準割当バイト,
    string 基準結果ダイジェスト,
    int? 基準論理CPU,
    int 基準ループ回数,
    bool 基準アフィニティ適用成功か);

public sealed record ベンチマーク性能ゲート対象判定(
    string 対象ID,
    bool 合格か,
    double? 比率中央値,
    double? 最終シェア,
    IReadOnlyList<string>失敗理由一覧);

public sealed record ベンチマーク性能ゲート判定(
    bool 合格か,
    IReadOnlyList<ベンチマーク性能ゲート対象判定>対象判定一覧);

[global::Seamless.FLinq.LinqIntercept(false)]
public static class ベンチマーク性能ゲートロジック{
    public const int 現行版=1;
    public const int 必須対象数=11;
    public const long 必須比較元実行ID=249;
    public const int 必須要素数=4096;
    public const double 必須目標ミリ秒=1024d;
    public const int 必須反復回数=5;

    public static IReadOnlyList<ベンチマーク性能ゲート解決対象>検証して解決(
        ベンチマーク性能ゲートマニフェスト マニフェスト,
        IReadOnlyList<ベンチマークケース>カタログ){
        ArgumentNullException.ThrowIfNull(マニフェスト);
        ArgumentNullException.ThrowIfNull(カタログ);
        マニフェスト構造を検証(マニフェスト);

        var 結果一覧=new List<ベンチマーク性能ゲート解決対象>(必須対象数);
        foreach(var 対象 in マニフェスト.対象一覧){
            if(string.IsNullOrWhiteSpace(対象.ID)
                ||string.IsNullOrWhiteSpace(対象.タブID)
                ||string.IsNullOrWhiteSpace(対象.表示型)
                ||string.IsNullOrWhiteSpace(対象.メソッド名)
                ||string.IsNullOrWhiteSpace(対象.左型完全名)
                ||string.IsNullOrWhiteSpace(対象.右型完全名)){
                throw new InvalidDataException("性能ゲート対象の識別情報が空です。ID="+対象.ID);
            }
            if(対象.Run249FLinq割当上限<0){
                throw new InvalidDataException("Run 249のFLinq割当上限は0以上である必要があります。ID="+対象.ID);
            }
            var タブ=ベンチマークソースタブ一覧.IDから取得を試みる(対象.タブID)
                ??throw new InvalidDataException("不明な性能ゲートタブIDです。ID="+対象.ID+" タブ="+対象.タブID);
            var 左=厳密にケースを取得(カタログ,タブ.左プロジェクト,タブ.左アセンブリ名,対象.左型完全名,対象.メソッド名,対象.引数キー,対象.ID,"FLinq");
            var 右=厳密にケースを取得(カタログ,タブ.右プロジェクト,タブ.右アセンブリ名,対象.右型完全名,対象.メソッド名,対象.引数キー,対象.ID,"基準");
            if(!左.引数一覧.SequenceEqual(右.引数一覧)){
                throw new InvalidDataException("性能ゲートの左右で引数一覧が一致しません。ID="+対象.ID);
            }
            結果一覧.Add(new(対象,タブ,左,右));
        }
        var 解決ケースキー一覧=結果一覧.SelectMany(static 対象=>new[]{
            解決ケースキーを作成(対象.FLinq),
            解決ケースキーを作成(対象.基準),
        }).ToArray();
        var 重複解決ケース一覧=解決ケースキー一覧
            .GroupBy(static キー=>キー,StringComparer.Ordinal)
            .Where(static 組=>組.Count()!=1)
            .Select(static 組=>組.Key)
            .ToArray();
        if(解決ケースキー一覧.Length!=必須対象数*2||重複解決ケース一覧.Length>0){
            throw new InvalidDataException("性能ゲートで解決した左右22ケースが一意ではありません: "+string.Join(",",重複解決ケース一覧));
        }
        return 結果一覧;
    }

    public static ベンチマーク性能ゲート判定 判定(
        ベンチマーク性能ゲートマニフェスト マニフェスト,
        IReadOnlyList<ベンチマーク性能ゲート測定>測定一覧){
        ArgumentNullException.ThrowIfNull(マニフェスト);
        ArgumentNullException.ThrowIfNull(測定一覧);
        マニフェスト構造を検証(マニフェスト);
        var 対象判定一覧=new List<ベンチマーク性能ゲート対象判定>(マニフェスト.対象一覧.Count);
        foreach(var 対象 in マニフェスト.対象一覧){
            var 対象測定一覧=測定一覧
                .Where(測定=>string.Equals(測定.対象ID,対象.ID,StringComparison.Ordinal))
                .OrderBy(static 測定=>測定.反復番号)
                .ToArray();
            var 失敗理由一覧=new List<string>();
            var 期待反復番号一覧=global::System.Linq.Enumerable.Range(1,マニフェスト.反復回数).ToArray();
            if(対象測定一覧.Length!=マニフェスト.反復回数
                ||!対象測定一覧.Select(static 測定=>測定.反復番号).SequenceEqual(期待反復番号一覧)){
                失敗理由一覧.Add("反復測定が1から5まで重複なく揃っていません。");
            }

            var 有効測定一覧=対象測定一覧
                .Where(static 測定=>double.IsFinite(測定.FLinqナノ秒)
                    &&測定.FLinqナノ秒>0
                    &&double.IsFinite(測定.基準ナノ秒)
                    &&測定.基準ナノ秒>0)
                .ToArray();
            if(有効測定一覧.Length!=対象測定一覧.Length){
                失敗理由一覧.Add("有限かつ正の左右測定時間が必要です。");
            }

            double? 比率中央値=null;
            if(有効測定一覧.Length==マニフェスト.反復回数){
                var 比率一覧=有効測定一覧.Select(static 測定=>測定.FLinqナノ秒/測定.基準ナノ秒).Order().ToArray();
                比率中央値=比率一覧[比率一覧.Length/2];
                if(比率中央値>1d){
                    失敗理由一覧.Add("5回のFLinq/基準比率中央値が1.00を超えています。");
                }
            }

            double? 最終シェア=null;
            var 最終測定=対象測定一覧.FirstOrDefault(測定=>測定.反復番号==マニフェスト.反復回数);
            if(最終測定 is not null&&最終測定.FLinqナノ秒>0&&最終測定.基準ナノ秒>0){
                最終シェア=最終測定.FLinqナノ秒/(最終測定.FLinqナノ秒+最終測定.基準ナノ秒);
                if(最終シェア>0.5d){
                    失敗理由一覧.Add("5回目の左シェアが0.50を超えています。");
                }
            }else{
                失敗理由一覧.Add("5回目の測定がありません。");
            }

            if(対象測定一覧.Any(static 測定=>string.IsNullOrWhiteSpace(測定.FLinq結果ダイジェスト)
                ||!string.Equals(測定.FLinq結果ダイジェスト,測定.基準結果ダイジェスト,StringComparison.Ordinal))){
                失敗理由一覧.Add("左右の結果ダイジェストが一致しません。");
            }
            if(対象測定一覧.Any(static 測定=>測定.FLinq論理CPU is null||測定.FLinq論理CPU!=測定.基準論理CPU)){
                失敗理由一覧.Add("左右の論理CPUが一致しません。");
            }
            if(対象測定一覧.Any(static 測定=>!測定.FLinqアフィニティ適用成功か||!測定.基準アフィニティ適用成功か)){
                失敗理由一覧.Add("左右のアフィニティ適用成功を確認できません。");
            }
            if(対象測定一覧.Any(static 測定=>測定.FLinqループ回数<=0||測定.FLinqループ回数!=測定.基準ループ回数)){
                失敗理由一覧.Add("左右のLoopCountが一致しません。");
            }
            if(対象測定一覧.Any(測定=>測定.FLinq割当バイト<0
                ||測定.基準割当バイト<0
                ||測定.FLinq割当バイト>測定.基準割当バイト
                ||測定.FLinq割当バイト>対象.Run249FLinq割当上限)){
                失敗理由一覧.Add("FLinq割当量が基準またはRun 249上限を超えています。");
            }
            対象判定一覧.Add(new(対象.ID,失敗理由一覧.Count==0,比率中央値,最終シェア,失敗理由一覧));
        }

        var 不明対象一覧=測定一覧
            .Select(static 測定=>測定.対象ID)
            .Except(マニフェスト.対象一覧.Select(static 対象=>対象.ID),StringComparer.Ordinal)
            .ToArray();
        if(不明対象一覧.Length>0){
            対象判定一覧.Add(new("<unknown>",false,null,null,["マニフェストにない測定があります: "+string.Join(",",不明対象一覧)]));
        }
        return new(対象判定一覧.All(static 判定=>判定.合格か),対象判定一覧);
    }

    private static void マニフェスト構造を検証(ベンチマーク性能ゲートマニフェスト マニフェスト){
        if(マニフェスト.版!=現行版){
            throw new InvalidDataException("未対応の性能ゲートマニフェスト版です。版="+マニフェスト.版);
        }
        if(マニフェスト.比較元実行ID!=必須比較元実行ID){
            throw new InvalidDataException("性能ゲートの比較元実行IDは承認済みのRun 249に固定されています。");
        }
        if(マニフェスト.要素数!=必須要素数
            ||マニフェスト.目標ミリ秒!=必須目標ミリ秒
            ||マニフェスト.反復回数!=必須反復回数){
            throw new InvalidDataException(
                "性能ゲート条件はN=4096、目標1024ms、5回に固定されています。"
                +" 要素数="+マニフェスト.要素数
                +" 目標ミリ秒="+マニフェスト.目標ミリ秒
                +" 反復回数="+マニフェスト.反復回数);
        }
        if(マニフェスト.対象一覧 is null||マニフェスト.対象一覧.Count!=必須対象数){
            throw new InvalidDataException("性能ゲート対象は重複なく11件である必要があります。");
        }
        if(マニフェスト.対象一覧.Any(static 対象=>対象 is null
            ||string.IsNullOrWhiteSpace(対象.ID)
            ||string.IsNullOrWhiteSpace(対象.タブID)
            ||string.IsNullOrWhiteSpace(対象.表示型)
            ||string.IsNullOrWhiteSpace(対象.メソッド名)
            ||string.IsNullOrWhiteSpace(対象.左型完全名)
            ||string.IsNullOrWhiteSpace(対象.右型完全名)
            ||対象.Run249FLinq割当上限<0)){
            throw new InvalidDataException("性能ゲート対象の識別情報またはRun 249割当上限が不正です。");
        }
        var 重複ID一覧=マニフェスト.対象一覧
            .GroupBy(static 対象=>対象.ID,StringComparer.Ordinal)
            .Where(static 組=>組.Count()!=1)
            .Select(static 組=>組.Key)
            .ToArray();
        if(重複ID一覧.Length>0){
            throw new InvalidDataException("性能ゲート対象IDが重複しています: "+string.Join(",",重複ID一覧));
        }
        var 重複ペア一覧=マニフェスト.対象一覧
            .GroupBy(static 対象=>string.Join("|",対象.タブID,対象.左型完全名,対象.右型完全名,対象.メソッド名,対象.引数キー),StringComparer.Ordinal)
            .Where(static 組=>組.Count()!=1)
            .Select(static 組=>組.Key)
            .ToArray();
        if(重複ペア一覧.Length>0){
            throw new InvalidDataException("性能ゲート対象ペアが重複しています: "+string.Join(",",重複ペア一覧));
        }
    }

    private static string 解決ケースキーを作成(ベンチマークケース ケース)=>
        string.Join("|",ケース.プロジェクト,ケース.アセンブリ名,ケース.型完全名,ケース.メソッド名,ケース.引数キー);

    private static ベンチマークケース 厳密にケースを取得(
        IReadOnlyList<ベンチマークケース>カタログ,
        string プロジェクト,
        string アセンブリ名,
        string 型完全名,
        string メソッド名,
        string 引数キー,
        string 対象ID,
        string 側名){
        var 候補一覧=カタログ.Where(ケース=>
            string.Equals(ケース.プロジェクト,プロジェクト,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(ケース.アセンブリ名,アセンブリ名,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(ケース.型完全名,型完全名,StringComparison.Ordinal)
            &&string.Equals(ケース.メソッド名,メソッド名,StringComparison.Ordinal)
            &&string.Equals(ケース.引数キー,引数キー,StringComparison.Ordinal)).ToArray();
        if(候補一覧.Length!=1){
            throw new InvalidDataException("性能ゲートケースを一意に解決できません。ID="+対象ID+" 側="+側名+" 件数="+候補一覧.Length);
        }
        return 候補一覧[0];
    }
}

public static class ベンチマーク性能ゲートCPU選択{
    public static int?最初のPコア主論理CPUを解決(IReadOnlyList<CPUコア情報>CPUコア一覧){
        ArgumentNullException.ThrowIfNull(CPUコア一覧);
        return CPUコア一覧
            .Where(static CPU=>CPU.コア種別==CPUコア種別.高性能)
            .GroupBy(static CPU=>CPU.物理コアインデックス)
            .Select(static 組=>new{
                物理コアインデックス=組.Key,
                種別内コアインデックス=組.Min(static CPU=>CPU.種別内コアインデックス),
                主論理CPU=組.OrderBy(static CPU=>CPU.論理プロセッサインデックス).First(),
            })
            .Where(static 組=>組.主論理CPU.アフィニティ対応か)
            .OrderBy(static 組=>組.種別内コアインデックス)
            .ThenBy(static 組=>組.物理コアインデックス)
            .Select(static 組=>(int?)組.主論理CPU.論理プロセッサインデックス)
            .FirstOrDefault();
    }

    public static int?実機から解決()=>最初のPコア主論理CPUを解決(ベンチマークCPUトポロジープロバイダー.読み込む());
}
