using System.Globalization;

namespace UI;

public sealed record ベンチマーク保存結果(
    string プロジェクト,
    string メソッド,
    string 型完全名,
    int 要素数,
    int ループ回数,
    string 状態,
    string 平均,
    double?平均ナノ秒,
    long?割り当てバイト数,
    DateTimeOffset 測定日時,
    DateTimeOffset?元データ更新日時,
    string 元データパス){
    public string 引数キー{get;init;}=string.Empty;
    public IReadOnlyList<ベンチマーク引数>引数一覧{get;init;}=[];
    public string ソースタブID{get;init;}=string.Empty;
    public double?合計ナノ秒{get;init;}
    public bool 実行内測定か{get;init;}
    /// <summary>計測を実行した論理CPU。DBから復元し、表の論理コア列表示に使う。</summary>
    public int?割り当て論理CPU{get;init;}

    public プロジェクト測定値 プロジェクト測定値に変換(){
        if(ジョブ状態名.保存値と一致するか(this.状態,ジョブ状態.完了)&&this.平均ナノ秒 is not null){
            return プロジェクト測定値.測定から作成(this.平均ナノ秒,this.割り当てバイト数);
        }
        return プロジェクト測定値.欠測;
    }
}

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ベンチマーク結果項目{
    public ベンチマーク結果項目(ベンチマークジョブ ジョブ){
        var 平均ナノ秒=ベンチマーク作業軸.操作単位に正規化(ジョブ.ソースタブ,ジョブ.ループ回数,ジョブ.平均ナノ秒);
        var 割り当てバイト数=ベンチマーク作業軸.操作単位に正規化(ジョブ.ソースタブ,ジョブ.ループ回数,ジョブ.割り当てバイト数);
        this.プロジェクト=ジョブ.プロジェクト;
        this.メソッド=ジョブ.メソッド名;
        this.型完全名=ジョブ.型完全名;
        this.ソースタブ=ジョブ.ソースタブ;
        this.要素数=ジョブ.要素数;
        this.ループ回数=ジョブ.ループ回数;
        this.名前=ジョブ.プロジェクト+" / "+ジョブ.メソッド名+" / "+this.軸数.ToString(CultureInfo.InvariantCulture);
        this.引数キー=ジョブ.引数キー;
        this.引数一覧=ジョブ.引数一覧.ToArray();
        this.状態=ジョブ状態名.保存値に変換(ジョブ.状態);
        if(平均ナノ秒 is { } 平均値){
            this.平均=ベンチマークフォーマッター.ナノ秒を整形(平均値);
        }else if(ジョブ.状態==ジョブ状態.失敗&&string.IsNullOrWhiteSpace(ジョブ.平均)){
            this.平均="失敗";
        }else{
            this.平均=ジョブ.平均;
        }
        if(割り当てバイト数 is { } 割り当て値){
            this.割り当てバイト数文字列=ベンチマークフォーマッター.バイト数を整形(割り当て値);
        }else{
            this.割り当てバイト数文字列=string.Empty;
        }
        this.割り当てバイト数値=割り当てバイト数;
        this.平均ナノ秒=平均ナノ秒;
        this.合計ナノ秒=ジョブ.平均ナノ秒;
        this.誤差ナノ秒=ベンチマーク作業軸.操作単位に正規化(ジョブ.ソースタブ,ジョブ.ループ回数,ジョブ.誤差ナノ秒);
        this.標準偏差ナノ秒=ベンチマーク作業軸.操作単位に正規化(ジョブ.ソースタブ,ジョブ.ループ回数,ジョブ.標準偏差ナノ秒);
        this.乱数シード=ジョブ.乱数シード;
        this.結果ダイジェスト=ジョブ.結果ダイジェスト;
        this.結果確認文字列=ジョブ.結果確認文字列;
        this.診断カウンター一覧=ジョブ.診断カウンター一覧.ToArray();
        this.成果物フォルダー=ジョブ.成果物フォルダー;
        this.終了コード=ジョブ.終了コード;
        this.割り当て論理CPU=ジョブ.割り当て論理CPU;
        this.アフィニティ適用成功か=ジョブ.アフィニティ適用成功か;
        this.開始日時=ジョブ.開始日時;
        this.失敗理由=ジョブ.失敗理由;
        this.実行項目インデックス=ジョブ.実行項目インデックス;
        this.バインド行インデックス=ジョブ.バインド行インデックス;
    }

    public ベンチマーク結果項目(
        string プロジェクト,
        string メソッド,
        プロジェクト測定値 測定値,
        int 要素数=0,
        string 型完全名="",
        string 引数キー="",
        IReadOnlyList<ベンチマーク引数>?引数一覧=null,
        ベンチマークソースタブ?ソースタブ=null){
        this.プロジェクト=プロジェクト;
        this.メソッド=メソッド;
        this.型完全名=型完全名;
        this.ソースタブ=ソースタブ??ソースタブを解決(プロジェクト);
        this.要素数=ベンチマーク作業軸.要素数を解決(this.ソースタブ,要素数);
        this.ループ回数=ベンチマーク作業軸.ループ回数を解決(this.ソースタブ,要素数);
        this.引数キー=引数キー;
        this.引数一覧=引数一覧??[];
        this.名前=プロジェクト+" / "+メソッド;
        if(測定値.測定値ありか){
            this.状態=ジョブ状態名.保存値に変換(ジョブ状態.完了);
        }else{
            this.状態="欠測";
        }
        this.平均=時間文字列を作成(測定値);
        this.割り当てバイト数文字列=割り当て文字列を作成(測定値);
        this.割り当てバイト数値=測定値.割り当てバイト数;
        this.平均ナノ秒=測定値.時間ナノ秒;
        this.合計ナノ秒=合計ナノ秒を解決(this.ソースタブ,this.ループ回数,this.平均ナノ秒);
        this.誤差ナノ秒=null;
        this.標準偏差ナノ秒=null;
        this.乱数シード=null;
        this.結果ダイジェスト=string.Empty;
        this.結果確認文字列=string.Empty;
        this.診断カウンター一覧=[];
        this.成果物フォルダー=string.Empty;
        if(測定値.測定値ありか){
            this.終了コード=0;
        }
        this.割り当て論理CPU=null;
        this.開始日時=null;
        this.失敗理由=string.Empty;
    }

    public ベンチマーク結果項目(ベンチマーク保存結果 結果)
        :this(結果.プロジェクト,結果.メソッド,結果.型完全名,結果.プロジェクト測定値に変換(),結果.要素数,結果.ループ回数,結果.状態,結果.測定日時,結果.引数キー,結果.引数一覧,結果.ソースタブID){
        this.割り当て論理CPU=結果.割り当て論理CPU;
        this.合計ナノ秒=結果.合計ナノ秒;
        this.実行内測定か=結果.実行内測定か;
    }

    private ベンチマーク結果項目(
        string プロジェクト,
        string メソッド,
        string 型完全名,
        プロジェクト測定値 測定値,
        int 要素数,
        int ループ回数,
        string 状態,
        DateTimeOffset 測定日時,
        string 引数キー="",
        IReadOnlyList<ベンチマーク引数>?引数一覧=null,
        string ソースタブID=""){
        this.プロジェクト=プロジェクト;
        this.メソッド=メソッド;
        this.型完全名=型完全名;
        this.ソースタブ=ソースタブを解決(プロジェクト,型完全名:型完全名,ソースタブID:ソースタブID);
        this.要素数=要素数;
        if(ループ回数>0){
            this.ループ回数=ループ回数;
        }else{
            this.ループ回数=ベンチマーク作業軸.既定ループ回数;
        }
        this.引数キー=引数キー;
        this.引数一覧=引数一覧??[];
        this.名前=プロジェクト+" / "+メソッド;
        this.状態=状態;
        this.平均=時間文字列を作成(測定値);
        this.割り当てバイト数文字列=割り当て文字列を作成(測定値);
        this.割り当てバイト数値=測定値.割り当てバイト数;
        this.平均ナノ秒=測定値.時間ナノ秒;
        this.合計ナノ秒=合計ナノ秒を解決(this.ソースタブ,this.ループ回数,this.平均ナノ秒);
        this.誤差ナノ秒=null;
        this.標準偏差ナノ秒=null;
        this.乱数シード=null;
        this.結果ダイジェスト=string.Empty;
        this.結果確認文字列=string.Empty;
        this.診断カウンター一覧=[];
        this.成果物フォルダー=string.Empty;
        if(測定値.測定値ありか){
            this.終了コード=0;
        }
        this.割り当て論理CPU=null;
        this.開始日時=測定日時;
        this.失敗理由=string.Empty;
    }

    private ベンチマーク結果項目(ベンチマークケース ベンチマーク,Exception 例外){
        this.プロジェクト=ベンチマーク.プロジェクト;
        this.メソッド=ベンチマーク.メソッド名;
        this.型完全名=ベンチマーク.型完全名;
        this.ソースタブ=ソースタブを解決(ベンチマーク.プロジェクト,ベンチマーク.アセンブリ名,ベンチマーク.型完全名);
        this.要素数=0;
        this.ループ回数=ベンチマーク作業軸.既定ループ回数;
        this.引数キー=ベンチマーク.引数キー;
        this.引数一覧=ベンチマーク.引数一覧;
        this.名前=ベンチマーク.プロジェクト+" / "+ベンチマーク.メソッド名;
        this.状態=ジョブ状態名.保存値に変換(ジョブ状態.失敗);
        this.平均=例外.GetType().Name;
        this.割り当てバイト数文字列=string.Empty;
        this.割り当てバイト数値=null;
        this.平均ナノ秒=null;
        this.合計ナノ秒=null;
        this.誤差ナノ秒=null;
        this.標準偏差ナノ秒=null;
        this.乱数シード=null;
        this.結果ダイジェスト=string.Empty;
        this.結果確認文字列=string.Empty;
        this.診断カウンター一覧=[];
        this.成果物フォルダー=string.Empty;
        this.終了コード=1;
        this.割り当て論理CPU=null;
        this.開始日時=null;
        this.失敗理由=例外.ToString();
    }

    public string プロジェクト{get;}
    public string メソッド{get;}
    public string 型完全名{get;}
    public ベンチマークソースタブ ソースタブ{get;}
    public int 要素数{get;}
    public int ループ回数{get;}
    public int 軸数=>ベンチマーク作業軸.軸数を取得(this);
    public string 引数キー{get;}
    public IReadOnlyList<ベンチマーク引数>引数一覧{get;}
    public string 名前{get;}
    public string 状態{get;}
    public string 平均{get;}
    public string 割り当てバイト数文字列{get;}
    public long?割り当てバイト数値{get;}
    public double?平均ナノ秒{get;}
    public double?合計ナノ秒{get;private set;}
    public double?誤差ナノ秒{get;}
    public double?標準偏差ナノ秒{get;}
    public int?乱数シード{get;}
    public string 結果ダイジェスト{get;}
    public string 結果確認文字列{get;set;}
    public IReadOnlyList<ベンチマーク診断カウンター>診断カウンター一覧{get;}
    public string 結果シード文字列=>this.乱数シード?.ToString(CultureInfo.InvariantCulture)??string.Empty;
    public string 成果物フォルダー{get;}
    public int?終了コード{get;}
    public int?割り当て論理CPU{get;}
    public bool アフィニティ適用成功か{get;}
    public DateTimeOffset?開始日時{get;}
    public string 失敗理由{get;}
    public bool 実行内測定か{get;set;}
    // 実行内で一意な生成順インデックス。既定値(-1)は対応する実行中ジョブがない履歴・比較表示由来などを示す。
    public int 実行項目インデックス{get;}=-1;
    public int バインド行インデックス{get;}=-1;

    public static ベンチマーク結果項目 失敗を作成(ベンチマークケース ベンチマーク,Exception 例外)=>new(ベンチマーク,例外);

    private static string 時間文字列を作成(プロジェクト測定値 測定値){
        if(測定値.測定値ありか&&測定値.時間ナノ秒 is { } 時間){
            return ベンチマークフォーマッター.ナノ秒を整形(時間);
        }
        return string.Empty;
    }

    private static string 割り当て文字列を作成(プロジェクト測定値 測定値){
        if(測定値.測定値ありか&&測定値.割り当てバイト数 is { } 割り当て){
            return ベンチマークフォーマッター.バイト数を整形(割り当て);
        }
        return string.Empty;
    }

    private static double?合計ナノ秒を解決(ベンチマークソースタブ ソースタブ,int ループ回数,double?平均ナノ秒){
        if(平均ナノ秒 is { } 値&&ベンチマーク作業軸.ループ回数を使うか(ソースタブ)&&ループ回数>1){
            return 値*ループ回数;
        }
        return 平均ナノ秒;
    }

    private static ベンチマークソースタブ ソースタブを解決(string プロジェクト,string アセンブリ名="",string 型完全名="",string ソースタブID=""){
        if(!string.IsNullOrWhiteSpace(ソースタブID)){
            var ソースタブ=ベンチマークソースタブ一覧.IDから取得を試みる(ソースタブID)
                ??throw new InvalidDataException("不明なベンチマークソースタブIDです。ID="+ソースタブID);
            return ソースタブを検証(ソースタブ.タブ,プロジェクト,アセンブリ名,型完全名);
        }

        if(!string.IsNullOrWhiteSpace(アセンブリ名)){
            var ソースタブ=ベンチマークソースタブ一覧.プロジェクトとアセンブリから取得(プロジェクト,アセンブリ名);
            return ソースタブを検証(ソースタブ.タブ,プロジェクト,アセンブリ名,型完全名);
        }

        return ベンチマークソースタブ一覧.プロジェクトから取得を試みる(プロジェクト)?.タブ
            ??throw new InvalidDataException("アセンブリなしではベンチマークのソースタブを一意に解決できません。プロジェクト="+プロジェクト);
    }

    private static ベンチマークソースタブ ソースタブを検証(
        ベンチマークソースタブ ソースタブ,
        string プロジェクト,
        string アセンブリ名,
        string 型完全名){
        var 定義=ベンチマークソースタブ一覧.取得(ソースタブ);
        var 解決アセンブリ名=string.IsNullOrWhiteSpace(アセンブリ名)
            ? 定義.アセンブリ名を取得(プロジェクト)
            : アセンブリ名;
        var ベンチマーク=new ベンチマークケース(プロジェクト,解決アセンブリ名,型完全名,string.Empty);
        if(!定義.ケースを含むか(ベンチマーク)){
            throw new InvalidDataException(
                "ベンチマークケースが指定されたソースタブに属していません。ソースタブ="+定義.ID
                +" プロジェクト="+プロジェクト
                +" アセンブリ="+(string.IsNullOrWhiteSpace(アセンブリ名) ? "<空>" : アセンブリ名)
                +" 型="+(string.IsNullOrWhiteSpace(型完全名) ? "<空>" : 型完全名));
        }
        return 定義.タブ;
    }
}
