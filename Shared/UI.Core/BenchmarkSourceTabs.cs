namespace UI;

public enum ベンチマークソースタブ{
    最適化書き換え,
    最適化パイプライン融合,
    最適化述語押し下げ,
    System配列,
    SystemIEnumerable,
    System範囲,
    Systemリスト,
    Seamless永続化,
    SeamlessTsavorite,
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
    string 中央プロジェクト="",
    string 中央見出し="",
    string 中央アセンブリ名="",
    string 中央型接頭辞="",
    string 追加プロジェクト="",
    string 追加見出し="",
    string 追加アセンブリ名="",
    string 追加型接頭辞="",
    IReadOnlyList<比較ペア定義>?比較ペア定義一覧=null){
    public IReadOnlyList<ベンチマーク比較列定義>列一覧{get;}=
        new ベンチマーク比較列定義[]{
            new("左",左プロジェクト,左見出し,左アセンブリ名,左型接頭辞),
            new("中央",中央プロジェクト,中央見出し,中央アセンブリ名,中央型接頭辞),
            new("追加",追加プロジェクト,追加見出し,追加アセンブリ名,追加型接頭辞),
            new("右",右プロジェクト,右見出し,右アセンブリ名,右型接頭辞),
        }.Where(static 列=>!string.IsNullOrWhiteSpace(列.プロジェクト)).ToArray();
    public IReadOnlyList<string>プロジェクト順{get;}=
        new[]{左プロジェクト,中央プロジェクト,追加プロジェクト,右プロジェクト}.Where(static プロジェクト=>!string.IsNullOrWhiteSpace(プロジェクト)).ToArray();
    public 比較ペア選択肢 ペア=>new(
        this.ID,
        this.表示名,
        this.左プロジェクト,
        this.右プロジェクト);

    public IReadOnlyList<比較ペア定義>比較ペア一覧{get;}=比較ペア定義一覧??(
        string.IsNullOrWhiteSpace(中央プロジェクト)
            ? [比較ペアを作成("左-右",左見出し,右見出し,左プロジェクト,右プロジェクト,"左","右")]
            : [
                比較ペアを作成("左-右",左見出し,右見出し,左プロジェクト,右プロジェクト,"左","右"),
                比較ペアを作成("中央-右",中央見出し,右見出し,中央プロジェクト,右プロジェクト,"中央","右"),
            ]);

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
    public const string System配列ID="System.Array";
    public const string SystemIEnumerableID="System.Collections.Generic.IEnumerable<>";
    public const string System範囲ID="System.Range";
    public const string SystemリストID="System.List";
    public const string Seamless永続化ID="Seamless.Persisted";
    public const string SeamlessTsavoriteID="Seamless.Tsavorite";
    public const string スケーラブル永続化セットID="Scalable.Persisted.Set";
    public const string スケーラブルTsavoriteセットID="Scalable.Tsavorite.Set";
    public const string スケーラブル永続化辞書ID="Scalable.Persisted.Dictionary";
    public const string スケーラブルTsavorite辞書ID="Scalable.Tsavorite.Dictionary";
    public const string スケーラブル永続化リストID="Scalable.Persisted.List";

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
        new(ベンチマークソースタブ.最適化書き換え,最適化書き換えID,"書き換え","Optimization.Rewrite.All","全最適化","Benchmark.Optimization.All","Optimization.Rewrite.Disabled","書き換え無効","Benchmark.Optimization.NoRewrite","Benchmark.Optimization.Rewrite.","Benchmark.Optimization.Rewrite."),
        new(ベンチマークソースタブ.最適化パイプライン融合,最適化パイプライン融合ID,"パイプライン融合","Optimization.PipelineFusion.All","全最適化","Benchmark.Optimization.All","Optimization.PipelineFusion.Disabled","パイプライン融合無効","Benchmark.Optimization.NoPipelineFusion","Benchmark.Optimization.PipelineFusion.","Benchmark.Optimization.PipelineFusion."),
        new(ベンチマークソースタブ.最適化述語押し下げ,最適化述語押し下げID,"述語プッシュダウン","Optimization.PredicatePushdown.All","全最適化","Benchmark.Optimization.All","Optimization.PredicatePushdown.Disabled","述語プッシュダウン無効","Benchmark.Optimization.NoPredicatePushdown","Benchmark.Optimization.PredicatePushdown.","Benchmark.Optimization.PredicatePushdown."),
        new(
            ベンチマークソースタブ.System配列,
            System配列ID,
            "Array",
            "System.Array.FLinq",
            "FLinq",
            "Benchmark.System.Linq.Enumerable.FLinq",
            "System.Array.Linq",
            "System.Linq",
            "Benchmark.System.Linq.Enumerable.Linq",
            中央プロジェクト:"System.Array.ZLinq",
            中央見出し:"ZLinq",
            中央アセンブリ名:"Benchmark.System.Linq.Enumerable.ZLinq"),
        new(
            ベンチマークソースタブ.SystemIEnumerable,
            SystemIEnumerableID,
            "IEnumerable",
            "System.IEnumerable.FLinq",
            "FLinq",
            "Benchmark.System.Linq.Enumerable.IEnumerable.FLinq",
            "System.IEnumerable.Linq",
            "System.Linq",
            "Benchmark.System.Linq.Enumerable.IEnumerable.Linq",
            中央プロジェクト:"System.IEnumerable.ZLinq",
            中央見出し:"ZLinq",
            中央アセンブリ名:"Benchmark.System.Linq.Enumerable.IEnumerable.ZLinq"),
        new(
            ベンチマークソースタブ.System範囲,
            System範囲ID,
            "Range",
            "System.Range.FLinq",
            "FLinq",
            "Benchmark.System.Linq.Enumerable.Range.FLinq",
            "System.Range.Linq",
            "System.Linq",
            "Benchmark.System.Linq.Enumerable.Range.Linq",
            中央プロジェクト:"System.Range.ZLinq",
            中央見出し:"ZLinq",
            中央アセンブリ名:"Benchmark.System.Linq.Enumerable.Range.ZLinq"),
        new(
            ベンチマークソースタブ.Systemリスト,
            SystemリストID,
            "List",
            "System.List.FLinq",
            "FLinq",
            "Benchmark.System.Linq.Enumerable.List.FLinq",
            "System.List.Linq",
            "System.Linq",
            "Benchmark.System.Linq.Enumerable.List.Linq",
            中央プロジェクト:"System.List.ZLinq",
            中央見出し:"ZLinq",
            中央アセンブリ名:"Benchmark.System.Linq.Enumerable.List.ZLinq"),
        new(
            ベンチマークソースタブ.Seamless永続化,
            Seamless永続化ID,
            "Persisted",
            "Seamless.Persisted.FLinq",
            "FLinq(Persisted)",
            "Benchmark.Persisted.Linq.Enumerable.FLinq",
            "Seamless.Persisted.Linq",
            "System.Linq",
            "Benchmark.Persisted.Linq.Enumerable.Linq",
            中央プロジェクト:"Seamless.Persisted.ValueLinq",
            中央見出し:"ValueEnumerable",
            中央アセンブリ名:"Benchmark.Persisted.Linq.Enumerable.ValueLinq",
            追加プロジェクト:"Seamless.Persisted.IEnumerable.FLinq",
            追加見出し:"FLinq(IEnumerable)",
            追加アセンブリ名:"Benchmark.Persisted.Linq.Enumerable.IEnumerable.FLinq",
             比較ペア定義一覧:[                  new("Native-ValueEnumerable","FLinq(Persisted) → ValueEnumerable","FLinq(Persisted) 対 ValueEnumerable","Seamless.Persisted.FLinq","Seamless.Persisted.ValueLinq","左","中央"),                  new("IEnumerable-SystemLinq","FLinq(IEnumerable) → System.Linq","FLinq(IEnumerable) 対 System.Linq","Seamless.Persisted.IEnumerable.FLinq","Seamless.Persisted.Linq","追加","右"),              ]),
        new(
            ベンチマークソースタブ.SeamlessTsavorite,
            SeamlessTsavoriteID,
            "Tsavorite",
            "Seamless.Tsavorite.FLinq",
            "FLinq(Tsavorite)",
            "Benchmark.Tsavorite.Linq.Enumerable.FLinq",
            "Seamless.Tsavorite.Linq",
            "System.Linq",
            "Benchmark.Tsavorite.Linq.Enumerable.Linq",
            中央プロジェクト:"Seamless.Tsavorite.ValueLinq",
            中央見出し:"ValueEnumerable",
            中央アセンブリ名:"Benchmark.Tsavorite.Linq.Enumerable.ValueLinq",
            追加プロジェクト:"Seamless.Tsavorite.IEnumerable.FLinq",
            追加見出し:"FLinq(IEnumerable)",
            追加アセンブリ名:"Benchmark.Tsavorite.Linq.Enumerable.IEnumerable.FLinq",
             比較ペア定義一覧:[                  new("Native-ValueEnumerable","FLinq(Tsavorite) → ValueEnumerable","FLinq(Tsavorite) 対 ValueEnumerable","Seamless.Tsavorite.FLinq","Seamless.Tsavorite.ValueLinq","左","中央"),                  new("IEnumerable-SystemLinq","FLinq(IEnumerable) → System.Linq","FLinq(IEnumerable) 対 System.Linq","Seamless.Tsavorite.IEnumerable.FLinq","Seamless.Tsavorite.Linq","追加","右"),              ]),
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
