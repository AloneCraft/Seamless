namespace UI;

public enum ベンチマークソースタブ{
    最適化書き換え,
    最適化パイプライン融合,
    最適化述語押し下げ,
    最適化射影融合,
    System配列,
    SystemIEnumerable,
    System範囲,
    Systemリスト,
    Seamless永続化,
    System永続化,
    SeamlessTsavorite,
    SystemTsavorite,
    スケーラブルセット,
    スケーラブル辞書,
    スケーラブル永続化リスト,
    スケーラブル永続化セット,
    スケーラブルTsavoriteセット,
    スケーラブル永続化辞書,
    スケーラブルTsavorite辞書,
}

public sealed record 比較ペア選択肢(string キー,string 表示名,string 左プロジェクト,string 右プロジェクト);

public sealed record ベンチマーク比較列定義(string キー,string プロジェクト,string 見出し,string アセンブリ名,string 型接頭辞="");

public sealed record ベンチマークソースタブ定義(
    ベンチマークソースタブ タブ,
    string ID,
    string 表示名,
    string 左プロジェクト,
    string 左見出し,
    string 左アセンブリ名,
    string 右プロジェクト,
    string 右見出し,
    string 右アセンブリ名,
    string 左型接頭辞="",
    string 右型接頭辞="",
    string 理想プロジェクト="",
    string 理想見出し="",
    string 理想アセンブリ名="",
    string 理想型接頭辞="",
    IReadOnlyList<比較ペア定義>?比較ペア定義一覧=null){
    // 理想列は DB の列を末尾に足すだけで移行できるよう、常に最後に置く。
    public IReadOnlyList<ベンチマーク比較列定義>列一覧{get;}=
        new ベンチマーク比較列定義[]{
            new("左",左プロジェクト,左見出し,左アセンブリ名,左型接頭辞),
            new("右",右プロジェクト,右見出し,右アセンブリ名,右型接頭辞),
            new("理想",理想プロジェクト,理想見出し,理想アセンブリ名,理想型接頭辞),
        }.Where(static 列=>!string.IsNullOrWhiteSpace(列.プロジェクト)).ToArray();
    public IReadOnlyList<string>プロジェクト順{get;}=
        new[]{左プロジェクト,右プロジェクト,理想プロジェクト}.Where(static プロジェクト=>!string.IsNullOrWhiteSpace(プロジェクト)).ToArray();

    public bool 理想ありか=>!string.IsNullOrWhiteSpace(this.理想プロジェクト);

    /// <summary>ループ回数を決める列。列の並びに依存させず、常に右(ZLinq/最適化無効)で較正する。</summary>
    public string 較正プロジェクト=>this.右プロジェクト;
    public 比較ペア選択肢 ペア=>new(
        this.ID,
        this.表示名,
        this.左プロジェクト,
        this.右プロジェクト);

    public IReadOnlyList<比較ペア定義>比較ペア一覧{get;}=比較ペア定義一覧??[
        比較ペアを作成("左-右",左見出し,右見出し,左プロジェクト,右プロジェクト,"左","右"),
    ];
    private static 比較ペア定義 比較ペアを作成(string キー,string 左見出し,string 右見出し,string 左プロジェクト,string 右プロジェクト,string 最適化列キー,string 基準列キー)=>
        new(キー,左見出し+" → "+右見出し,左見出し+" 対 "+右見出し,左プロジェクト,右プロジェクト,最適化列キー,基準列キー);

    public bool プロジェクトを含むか(string プロジェクト)=>
        this.プロジェクト順.Contains(プロジェクト,StringComparer.OrdinalIgnoreCase);

    public bool ケースを含むか(ベンチマークケース ベンチマーク){
        if(!this.プロジェクトを含むか(ベンチマーク.プロジェクト)){
            return false;
        }

        var アセンブリ名=this.アセンブリ名を取得(ベンチマーク.プロジェクト);
        if(string.IsNullOrWhiteSpace(アセンブリ名)||string.IsNullOrWhiteSpace(ベンチマーク.アセンブリ名)){
            return true;
        }

        if(!string.Equals(アセンブリ名,ベンチマーク.アセンブリ名,StringComparison.OrdinalIgnoreCase)){
            return false;
        }
        var 接頭辞=this.列一覧.FirstOrDefault(列=>string.Equals(ベンチマーク.プロジェクト,列.プロジェクト,StringComparison.OrdinalIgnoreCase))?.型接頭辞??string.Empty;
        return string.IsNullOrWhiteSpace(接頭辞)||ベンチマーク.型完全名.StartsWith(接頭辞,StringComparison.Ordinal);
    }

    public string アセンブリ名を取得(string プロジェクト){
        return this.列一覧.FirstOrDefault(列=>string.Equals(プロジェクト,列.プロジェクト,StringComparison.OrdinalIgnoreCase))?.アセンブリ名??string.Empty;
    }
}

public static class ベンチマークソースタブ一覧{
    public const string 最適化書き換えID="Optimization.Rewrite";
    public const string 最適化パイプライン融合ID="Optimization.PipelineFusion";
    public const string 最適化述語押し下げID="Optimization.PredicatePushdown";
    public const string 最適化射影融合ID="Optimization.ProjectionMerge";
    public const string System配列ID="System.Array";
    public const string SystemIEnumerableID="System.Collections.Generic.IEnumerable<>";
    public const string System範囲ID="System.Range";
    public const string SystemリストID="System.List";
    public const string Seamless永続化ID="Seamless.Persisted.Seamless";
    public const string System永続化ID="Seamless.Persisted.System";
    public const string SeamlessTsavoriteID="Seamless.Tsavorite.Seamless";
    public const string SystemTsavoriteID="Seamless.Tsavorite.System";
    public const string スケーラブル永続化セットID="Scalable.Persisted.Set";
    public const string スケーラブルTsavoriteセットID="Scalable.Tsavorite.Set";
    public const string スケーラブル永続化辞書ID="Scalable.Persisted.Dictionary";
    public const string スケーラブルTsavorite辞書ID="Scalable.Tsavorite.Dictionary";
    public const string スケーラブル永続化リストID="Scalable.Persisted.List";
    private const string 理想見出し="理想";
    private const string 最適化理想アセンブリ名="Benchmark.Optimization.Ideal";

    public static IReadOnlyList<ベンチマークソースタブ定義>スケーラブル全て{get;}=[
        new(
            ベンチマークソースタブ.スケーラブル永続化セット,
            スケーラブル永続化セットID,
            "Persisted.Set",
            "Scalable.Persisted.Set",
            "今回",
            "Benchmark.Scalable.Persisted.Collections",
            "Scalable.Persisted.Set.Previous",
            "前回",
            string.Empty),
        new(
            ベンチマークソースタブ.スケーラブルTsavoriteセット,
            スケーラブルTsavoriteセットID,
            "Tsavorite.Set",
            "Scalable.Tsavorite.Set",
            "今回",
            "Benchmark.Scalable.Tsavorite.Collections",
            "Scalable.Tsavorite.Set.Previous",
            "前回",
            string.Empty),
        new(
            ベンチマークソースタブ.スケーラブル永続化辞書,
            スケーラブル永続化辞書ID,
            "Persisted.Dictionary",
            "Scalable.Persisted.Dictionary",
            "今回",
            "Benchmark.Scalable.Persisted.Collections",
            "Scalable.Persisted.Dictionary.Previous",
            "前回",
            string.Empty),
        new(
            ベンチマークソースタブ.スケーラブルTsavorite辞書,
            スケーラブルTsavorite辞書ID,
            "Tsavorite.Dictionary",
            "Scalable.Tsavorite.Dictionary",
            "今回",
            "Benchmark.Scalable.Tsavorite.Collections",
            "Scalable.Tsavorite.Dictionary.Previous",
            "前回",
            string.Empty),
        new(
            ベンチマークソースタブ.スケーラブル永続化リスト,
            スケーラブル永続化リストID,
            "Persisted.List",
            "Scalable.Persisted.List",
            "今回",
            "Benchmark.Scalable.Persisted.Collections",
            "Scalable.Persisted.List.Previous",
            "前回",
            string.Empty),
    ];

    public static IReadOnlyList<ベンチマークソースタブ定義>標準全て{get;}=[
        new(ベンチマークソースタブ.最適化書き換え,最適化書き換えID,"書き換え","Optimization.Rewrite.All","全最適化","Benchmark.Optimization.All","Optimization.Rewrite.Disabled","書き換え無効","Benchmark.Optimization.NoRewrite","Benchmark.Optimization.Rewrite.","Benchmark.Optimization.Rewrite.","Optimization.Rewrite.Ideal",理想見出し,最適化理想アセンブリ名,"Benchmark.Optimization.Rewrite."),
        new(ベンチマークソースタブ.最適化パイプライン融合,最適化パイプライン融合ID,"パイプライン融合","Optimization.PipelineFusion.All","全最適化","Benchmark.Optimization.All","Optimization.PipelineFusion.Disabled","パイプライン融合無効","Benchmark.Optimization.NoPipelineFusion","Benchmark.Optimization.PipelineFusion.","Benchmark.Optimization.PipelineFusion.","Optimization.PipelineFusion.Ideal",理想見出し,最適化理想アセンブリ名,"Benchmark.Optimization.PipelineFusion."),
        new(ベンチマークソースタブ.最適化述語押し下げ,最適化述語押し下げID,"述語プッシュダウン","Optimization.PredicatePushdown.All","全最適化","Benchmark.Optimization.All","Optimization.PredicatePushdown.Disabled","述語プッシュダウン無効","Benchmark.Optimization.NoPredicatePushdown","Benchmark.Optimization.PredicatePushdown.","Benchmark.Optimization.PredicatePushdown.","Optimization.PredicatePushdown.Ideal",理想見出し,最適化理想アセンブリ名,"Benchmark.Optimization.PredicatePushdown."),
        new(ベンチマークソースタブ.最適化射影融合,最適化射影融合ID,"射影融合","Optimization.ProjectionMerge.All","全最適化","Benchmark.Optimization.All","Optimization.ProjectionMerge.Disabled","射影融合無効","Benchmark.Optimization.NoProjectionMerge","Benchmark.Optimization.ProjectionMerge.","Benchmark.Optimization.ProjectionMerge.","Optimization.ProjectionMerge.Ideal",理想見出し,最適化理想アセンブリ名,"Benchmark.Optimization.ProjectionMerge."),
        new(
            ベンチマークソースタブ.System配列,
            System配列ID,
            "Array",
            "System.Array.FLinq",
            "FLinq",
            "Benchmark.System.Linq.Enumerable.FLinq",
            "System.Array.ZLinq",
            "ZLinq",
            "Benchmark.System.Linq.Enumerable.ZLinq",
            理想プロジェクト:"System.Array.Ideal",
            理想見出し:理想見出し,
            理想アセンブリ名:"Benchmark.System.Linq.Enumerable.Ideal"),
        new(
            ベンチマークソースタブ.SystemIEnumerable,
            SystemIEnumerableID,
            "IEnumerable",
            "System.IEnumerable.FLinq",
            "FLinq",
            "Benchmark.System.Linq.Enumerable.IEnumerable.FLinq",
            "System.IEnumerable.ZLinq",
            "ZLinq",
            "Benchmark.System.Linq.Enumerable.IEnumerable.ZLinq",
            理想プロジェクト:"System.IEnumerable.Ideal",
            理想見出し:理想見出し,
            理想アセンブリ名:"Benchmark.System.Linq.Enumerable.IEnumerable.Ideal"),
        new(
            ベンチマークソースタブ.System範囲,
            System範囲ID,
            "Range",
            "System.Range.FLinq",
            "FLinq",
            "Benchmark.System.Linq.Enumerable.Range.FLinq",
            "System.Range.ZLinq",
            "ZLinq",
            "Benchmark.System.Linq.Enumerable.Range.ZLinq",
            理想プロジェクト:"System.Range.Ideal",
            理想見出し:理想見出し,
            理想アセンブリ名:"Benchmark.System.Linq.Enumerable.Range.Ideal"),
        new(
            ベンチマークソースタブ.Systemリスト,
            SystemリストID,
            "List",
            "System.List.FLinq",
            "FLinq",
            "Benchmark.System.Linq.Enumerable.List.FLinq",
            "System.List.ZLinq",
            "ZLinq",
            "Benchmark.System.Linq.Enumerable.List.ZLinq",
            理想プロジェクト:"System.List.Ideal",
            理想見出し:理想見出し,
            理想アセンブリ名:"Benchmark.System.Linq.Enumerable.List.Ideal"),
        new(
            ベンチマークソースタブ.Seamless永続化,
            Seamless永続化ID,
            "Persisted(Seamless)",
            "Seamless.Persisted.FLinq",
            "FLinq",
            "Benchmark.Persisted.Linq.Enumerable.FLinq",
            "Seamless.Persisted.ValueLinq",
            "Persisted(Seamless)",
            "Benchmark.Persisted.Linq.Enumerable.ValueLinq"),
        new(
            ベンチマークソースタブ.System永続化,
            System永続化ID,
            "Persisted(System)",
            "Seamless.Persisted.IEnumerable.FLinq",
            "FLinq",
            "Benchmark.Persisted.Linq.Enumerable.IEnumerable.FLinq",
            "Seamless.Persisted.Linq",
            "Persisted(System)",
            "Benchmark.Persisted.Linq.Enumerable.Linq"),
        new(
            ベンチマークソースタブ.SeamlessTsavorite,
            SeamlessTsavoriteID,
            "Tsavorite(Seamless)",
            "Seamless.Tsavorite.FLinq",
            "FLinq",
            "Benchmark.Tsavorite.Linq.Enumerable.FLinq",
            "Seamless.Tsavorite.ValueLinq",
            "Tsavorite(Seamless)",
            "Benchmark.Tsavorite.Linq.Enumerable.ValueLinq"),
        new(
            ベンチマークソースタブ.SystemTsavorite,
            SystemTsavoriteID,
            "Tsavorite(System)",
            "Seamless.Tsavorite.IEnumerable.FLinq",
            "FLinq",
            "Benchmark.Tsavorite.Linq.Enumerable.IEnumerable.FLinq",
            "Seamless.Tsavorite.Linq",
            "Tsavorite(System)",
            "Benchmark.Tsavorite.Linq.Enumerable.Linq"),
    ];

#if SCALABLE_BENCHMARK
    public static IReadOnlyList<ベンチマークソースタブ定義>全て=>スケーラブル全て;
#else
    public static IReadOnlyList<ベンチマークソースタブ定義>全て=>標準全て;
#endif

    public static ベンチマークソースタブ定義 既定=>全て[0];

    private static IEnumerable<ベンチマークソースタブ定義>全定義=>
        標準全て.Concat(スケーラブル全て);

    public static ベンチマークソースタブ定義 取得(ベンチマークソースタブ タブ)=>
        全定義.First(項目=>項目.タブ==タブ);

    public static ベンチマークソースタブ定義?IDから取得を試みる(string ID)=>
        全定義.FirstOrDefault(項目=>string.Equals(項目.ID,ID,StringComparison.OrdinalIgnoreCase));

    public static ベンチマークソースタブ定義?プロジェクトから取得を試みる(string プロジェクト){
        var 候補一覧=全定義
            .Where(項目=>項目.プロジェクトを含むか(プロジェクト))
            .Take(2)
            .ToArray();
        return 候補一覧.Length==1 ? 候補一覧[0] : null;
    }

    public static ベンチマークソースタブ定義?プロジェクトとアセンブリから取得を試みる(string プロジェクト,string アセンブリ名){
        var 候補一覧=全定義
            .Where(項目=>項目.プロジェクトを含むか(プロジェクト)
                &&string.Equals(項目.アセンブリ名を取得(プロジェクト),アセンブリ名,StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        return 候補一覧.Length==1 ? 候補一覧[0] : null;
    }

    public static ベンチマークソースタブ定義 プロジェクトとアセンブリから取得(string プロジェクト,string アセンブリ名){
        if(プロジェクトとアセンブリから取得を試みる(プロジェクト,アセンブリ名) is { } ソースタブ){
            return ソースタブ;
        }

        var 候補一覧=全定義
            .Where(項目=>項目.プロジェクトを含むか(プロジェクト))
            .Select(項目=>項目.ID+" ("+項目.アセンブリ名を取得(プロジェクト)+")")
            .ToArray();
        throw new InvalidDataException(
            "ベンチマークのソースタブを厳密に解決できません。プロジェクト="+プロジェクト
            +" アセンブリ="+(string.IsNullOrWhiteSpace(アセンブリ名) ? "<空>" : アセンブリ名)
            +" 候補="+(候補一覧.Length==0 ? "<なし>" : string.Join(", ",候補一覧)));
    }

    public static string パス部分に変換(ベンチマークソースタブ タブ){
        var ID=取得(タブ).ID;
        var 無効文字一覧=Path.GetInvalidFileNameChars();
        var ビルダー=new System.Text.StringBuilder(ID.Length);
        foreach(var 文字 in ID){
            if(無効文字一覧.Contains(文字)||文字 is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'){
                ビルダー.Append('_');
            }else{
                ビルダー.Append(文字);
            }
        }
        if(ビルダー.Length==0){
            return "_";
        }
        return ビルダー.ToString().TrimEnd(' ','.');
    }
}
