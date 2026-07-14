using System.Globalization;

namespace UI;

public sealed record ベンチマーク引数(string 名前,string 値);

public sealed class ベンチマークケース(
    string                  プロジェクト,
    string                  アセンブリ名,
    string                  型完全名,
    string                  メソッド名,
    string                  引数キー,
    IReadOnlyList<ベンチマーク引数> 引数一覧,
    string                  ベンチマークパス="") {
    public ベンチマークケース(
        string プロジェクト,
        string アセンブリ名,
        string 型完全名,
        string メソッド名,
        string ベンチマークパス="")
        :this(プロジェクト,アセンブリ名,型完全名,メソッド名,string.Empty,[],ベンチマークパス){
    }
    public string                  プロジェクト      {get;}     =プロジェクト;
    public string                  アセンブリ名      {get;}     =アセンブリ名;
    public string                  型完全名        {get;}     =型完全名;
    public string                  メソッド名       {get;}     =メソッド名;
    public string                  引数キー        {get;}     =引数キー;
    public IReadOnlyList<ベンチマーク引数> 引数一覧        {get;}     =引数一覧;
    public string                  ベンチマークパス    {get;}     =ベンチマークパス;
    public int                     バインド行インデックス {get;init;}=-1;
    public string                  表示名         =>$"{this.プロジェクト} / {this.メソッド名}";
    public int                     Count引数     =>ベンチマーク引数フォーマッター.整数を取得(this.引数一覧,"Count");
}

public enum ジョブ状態{
    保留,
    実行中,
    一時停止,
    完了,
    失敗,
}

internal static class ジョブ状態名{
    public const string 保留保存値="Pending";
    public const string 実行中保存値="Running";
    public const string 一時停止保存値="Paused";
    public const string 完了保存値="Completed";
    public const string 失敗保存値="Failed";

    public static string 保存値に変換(ジョブ状態 状態){
        return 状態 switch{
            ジョブ状態.保留=>保留保存値,
            ジョブ状態.実行中=>実行中保存値,
            ジョブ状態.一時停止=>一時停止保存値,
            ジョブ状態.完了=>完了保存値,
            ジョブ状態.失敗=>失敗保存値,
            _=>throw new ArgumentOutOfRangeException(nameof(状態),状態,null),
        };
    }

    public static bool 保存値と一致するか(string 保存値,ジョブ状態 状態)=>
        string.Equals(保存値,保存値に変換(状態),StringComparison.OrdinalIgnoreCase);
}

internal enum 比較側{
    左,
    右,
}

internal static class 比較側名{
    public static string 保存値に変換(比較側 側)=>側 switch{
        比較側.左=>"Left",
        比較側.右=>"Right",
        _=>throw new ArgumentOutOfRangeException(nameof(側),側,null),
    };

    public static bool 保存値と一致するか(string 保存値,比較側 側)=>
        string.Equals(保存値,保存値に変換(側),StringComparison.OrdinalIgnoreCase);
}

public enum ベンチマーク実行プロファイル{
    正確,
    中間,
    高速,
    テスト,
}

internal static class ベンチマーク実行プロファイル名{
    public static string フォルダー名に変換(ベンチマーク実行プロファイル プロファイル){
        return プロファイル switch{
            ベンチマーク実行プロファイル.正確=>"Accurate",
            ベンチマーク実行プロファイル.中間=>"Intermediate",
            ベンチマーク実行プロファイル.高速=>"Fast",
            ベンチマーク実行プロファイル.テスト=>"Test",
            _=>throw new ArgumentOutOfRangeException(nameof(プロファイル),プロファイル,null),
        };
    }

    public static string 割り当てバイト数取得元を取得(ベンチマーク実行プロファイル プロファイル){
        return プロファイル switch{
            ベンチマーク実行プロファイル.高速=>"GC.GetAllocatedBytesForCurrentThread",
            ベンチマーク実行プロファイル.テスト=>"Thread.Sleep テスト実行器",
            ベンチマーク実行プロファイル.正確 or ベンチマーク実行プロファイル.中間=>"BenchmarkDotNet MemoryDiagnoser",
            _=>"不明",
        };
    }
}

public sealed class ベンチマークジョブ{
    public string ジョブID{get;set;}=string.Empty;
    public string プロジェクト{get;set;}=string.Empty;
    public string アセンブリ名{get;set;}=string.Empty;
    public string 型完全名{get;set;}=string.Empty;
    public string メソッド名{get;set;}=string.Empty;
    public string 引数キー{get;set;}=string.Empty;
    public List<ベンチマーク引数>引数一覧{get;set;}=[];
    public ベンチマークソースタブ ソースタブ{get;set;}=ベンチマークソースタブ.System配列;
    public int 要素数{get;set;}
    public int ループ回数{get;set;}=ベンチマーク作業軸.既定ループ回数;
    public List<int>準備済み要素数一覧{get;set;}=[];
    public ジョブ状態 状態{get;set;}=ジョブ状態.保留;
    public int?割り当て論理CPU{get;set;}
    public string 平均{get;set;}=string.Empty;
    public string 誤差{get;set;}=string.Empty;
    public string 標準偏差{get;set;}=string.Empty;
    public double?平均ナノ秒{get;set;}
    public double?誤差ナノ秒{get;set;}
    public double?標準偏差ナノ秒{get;set;}
    public long?割り当てバイト数{get;set;}
    public int?乱数シード{get;set;}
    public string 結果ダイジェスト{get;set;}=string.Empty;
    public string 結果確認文字列{get;set;}=string.Empty;
    public List<ベンチマーク診断カウンター>診断カウンター一覧{get;set;}=[];
    public int?終了コード{get;set;}
    public string 成果物フォルダー{get;set;}=string.Empty;
    public string 失敗理由{get;set;}=string.Empty;
    public DateTimeOffset?開始日時{get;set;}
    public DateTimeOffset?終了日時{get;set;}
    // 実行内で一意な生成順インデックス。実行中は文字列キー照合の代わりにこの値でUI行を更新する。
    public int 実行項目インデックス{get;set;}=-1;
    public int バインド行インデックス{get;set;}=-1;

    public ベンチマークケース ベンチマークケースに変換()=>
        new(this.プロジェクト,this.アセンブリ名,this.型完全名,this.メソッド名,this.引数キー,this.引数一覧){
            バインド行インデックス=this.バインド行インデックス,
        };

    public 報告行 報告行に変換(){
        var 平均ナノ秒=ベンチマーク作業軸.操作単位に正規化(this.ソースタブ,this.ループ回数,this.平均ナノ秒);
        var 誤差ナノ秒=ベンチマーク作業軸.操作単位に正規化(this.ソースタブ,this.ループ回数,this.誤差ナノ秒);
        var 標準偏差ナノ秒=ベンチマーク作業軸.操作単位に正規化(this.ソースタブ,this.ループ回数,this.標準偏差ナノ秒);
        var 割り当てバイト数=ベンチマーク作業軸.操作単位に正規化(this.ソースタブ,this.ループ回数,this.割り当てバイト数);
        return new(
            this.プロジェクト,
            this.メソッド名,
            this.型完全名,
            this.メソッド名,
            ジョブ状態名.保存値に変換(this.状態),
            this.平均,
            this.誤差,
            this.標準偏差,
            割り当てバイト数?.ToString(CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            平均ナノ秒??0,
            誤差ナノ秒??0,
            標準偏差ナノ秒??0,
            割り当てバイト数??0,
            0,
            0,
            this.割り当て論理CPU?.ToString(CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            this.開始日時?.ToString("O",CultureInfo.InvariantCulture)??string.Empty,
            this.終了日時?.ToString("O",CultureInfo.InvariantCulture)??string.Empty,
            this.成果物フォルダー,
            this.終了コード,
            this.引数キー,
            this.引数一覧);
    }
}

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class 適応ベンチマーク状態{
    public string メソッド名{get;set;}=string.Empty;
    public bool 停止済みか{get;set;}
    public bool 完了済みか{get;set;}
    public int 次の要素数インデックス{get;set;}
    public int?停止要素数{get;set;}
    public string 理由{get;set;}=string.Empty;
    public string 発火プロジェクト{get;set;}=string.Empty;
    public double?発火平均ナノ秒{get;set;}
}

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class 進行状態{
    public int スキーマ版{get;set;}=1;
    public string 実行ID{get;set;}=string.Empty;
    public ベンチマーク実行プロファイル 実行プロファイル{get;set;}=ベンチマーク実行プロファイル.正確;
    public string 実行ルート{get;set;}=string.Empty;
    public int 実行番号{get;set;}
    public ベンチマークソースタブ ソースタブ{get;set;}=ベンチマークソースタブ.System配列;
    public List<int>要素数一覧{get;set;}=[];
    public double 適応しきい値ナノ秒{get;set;}
    public List<ベンチマークジョブ>ジョブ一覧{get;set;}=[];
    // 適応実行中に動的生成されるジョブへ一意な実行項目インデックスを割り当てるための単調増加カウンター。
    public int 次の実行項目インデックス{get;set;}
    public List<適応ベンチマーク状態>適応状態一覧{get;set;}=[];
    public 進行ベンチマーク選択状態 ベンチマーク選択{get;set;}=new();
    public DateTimeOffset 作成日時{get;set;}
    public DateTimeOffset 更新日時{get;set;}

    public bool 未完了ジョブがあるか=>
        this.ジョブ一覧.Any(static ジョブ=>ジョブ.状態 is ジョブ状態.保留 or ジョブ状態.実行中);
}

internal sealed record ベンチマーク実行要求(
    IReadOnlyList<ベンチマークケース>ベンチマーク一覧,
    IReadOnlyList<int>論理CPU一覧,
    IReadOnlyList<int>要素数一覧,
    TimeSpan 適応しきい値,
    TimeSpan ジョブタイムアウト,
    Func<IReadOnlyList<int>>?論理CPU取得器=null,
    bool 要求要素数から再開するか=false,
    ベンチマーク実行プロファイル 実行プロファイル=ベンチマーク実行プロファイル.正確,
    string?実行ルート上書き=null,
    ベンチマークソースタブ ソースタブ=ベンチマークソースタブ.System配列,
    Func<IReadOnlyList<ベンチマークケース>>?ベンチマーク取得器=null);

public sealed record ベンチマーク実行オプション(bool 無限テスト実行器を使うか=false);

public sealed record ベンチマーク診断カウンター(
    string カウンター名,
    double カウンター値);

public sealed record ベンチマークジョブ実行結果(
    string プロジェクト,
    string メソッド,
    string 型完全名,
    int 要素数,
    string 状態,
    string 平均,
    string 誤差,
    string 標準偏差,
    double?平均ナノ秒,
    double?誤差ナノ秒,
    double?標準偏差ナノ秒,
    long?割り当てバイト数,
    int 終了コード,
    string 成果物フォルダー,
    string ログ,
    int?乱数シード=null,
    string 結果ダイジェスト="",
    string 引数キー="",
    IReadOnlyList<ベンチマーク引数>?引数値一覧=null,
    int ループ回数=0,
    int 実行項目インデックス=-1,
    IReadOnlyList<ベンチマーク診断カウンター>?診断カウンター値一覧=null){
    public IReadOnlyList<ベンチマーク引数>引数一覧{get;}=引数値一覧??[];
    public IReadOnlyList<ベンチマーク診断カウンター>診断カウンター一覧{get;}=診断カウンター値一覧??[];
}

public interface Iベンチマークジョブ実行器{
    Task<ベンチマークジョブ実行結果>実行Async(
        ベンチマークジョブ ジョブ,
        string 実行ルート,
        int 論理CPU,
        TimeSpan タイムアウト,
        CancellationToken キャンセルトークン);
}

internal sealed class 進行ベンチマーク選択状態{
    public Dictionary<string,bool>行選択{get;set;}=[with(StringComparer.OrdinalIgnoreCase)];
}

public interface Iベンチマーク動的CPUセット実行器{
    Task CPUセットを適用Async(IReadOnlyList<int>論理CPU一覧);
}

internal sealed record ベンチマーク進行更新(進行状態 状態,ベンチマークジョブ ジョブ);

internal sealed record ベンチマークオーケストレーター結果(string 実行ルート,進行状態 状態);

internal static class ベンチマーク引数フォーマッター{
    public static string キーを作成(IReadOnlyList<ベンチマーク引数>引数一覧)=>
        string.Join(",",引数一覧.Select(static 引数=>引数.名前+"="+引数.値));

    public static string 値を取得(IReadOnlyList<ベンチマーク引数>引数一覧,string 名前)=>
        引数一覧.FirstOrDefault(引数=>string.Equals(引数.名前,名前,StringComparison.OrdinalIgnoreCase))?.値??string.Empty;

    public static int 整数を取得(IReadOnlyList<ベンチマーク引数>引数一覧,string 名前){
        if(int.TryParse(値を取得(引数一覧,名前),NumberStyles.Integer,CultureInfo.InvariantCulture,out var 値)){
            return 値;
        }
        return 0;
    }

    public static object?[]オブジェクト一覧に変換(IReadOnlyList<ベンチマーク引数>引数一覧)=>
        引数一覧.Select(static 引数=>値を解析(引数.値)).ToArray();

    private static object 値を解析(string 値){
        if(long.TryParse(値,NumberStyles.Integer,CultureInfo.InvariantCulture,out var 整数値)){
            return 整数値;
        }
        if(double.TryParse(値,NumberStyles.Float,CultureInfo.InvariantCulture,out var 浮動小数点値)){
            return 浮動小数点値;
        }
        return 値;
    }
}
