using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;

namespace UI;

public enum ベンチマーク下段タブ {
    CPU,
    ログ,
    速度改善プロンプト,
    メモリ改善プロンプト,
}

public enum ベンチマーク指標種別 {
    時間,
    割り当て,
}

public enum ベンチマークグリッドソート列 {
    ベンチマーク型,
    ベンチマークメソッド,
    ベンチマーク名,
    論理CPU,
    ループ回数,
    引数Count,
    引数インデックス,
    引数ページ,
    引数セグメント,
    引数メモリ,
    引数割合,
    引数初期値,
    引数データ,
    左日時,
    左時間,
    左割り当て,
    時間シェア,
    メモリシェア,
    右日時,
    右時間,
    右割り当て,
}

public sealed class ベンチマークソート規則(ベンチマークグリッドソート列 列,bool 降順か値=false):ObservableObject {
    private ベンチマークグリッドソート列 列値  =列;
    private bool           降順か値=降順か値;
    public ベンチマークグリッドソート列 列 {
        get => this.列値;
        set {
            if(this.SetProperty(ref this.列値,value)) {
                this.OnPropertyChanged(nameof(this.列文字列));
            }
        }
    }

    public bool 降順か {
        get => this.降順か値;
        set {
            if(this.SetProperty(ref this.降順か値,value)) {
                this.OnPropertyChanged(nameof(this.方向文字列));
            }
        }
    }

    public string 列文字列 => 列文字列を取得(this.列);

    public string 方向文字列 => this.降順か ? "降順" : "昇順";

    public static string 列文字列を取得(ベンチマークグリッドソート列 列) =>
        列 switch {
            ベンチマークグリッドソート列.ベンチマーク型 => "型",
            ベンチマークグリッドソート列.ベンチマークメソッド => "ベンチマーク",
            ベンチマークグリッドソート列.ベンチマーク名 => "ベンチマーク",
            ベンチマークグリッドソート列.論理CPU => "CPU",
            ベンチマークグリッドソート列.ループ回数 => "ループ回数",
            ベンチマークグリッドソート列.引数Count => "Count",
            ベンチマークグリッドソート列.引数インデックス => "Index",
            ベンチマークグリッドソート列.引数ページ => "Page",
            ベンチマークグリッドソート列.引数セグメント => "Segment",
            ベンチマークグリッドソート列.引数メモリ => "Memory",
            ベンチマークグリッドソート列.引数割合 => "Fraction",
            ベンチマークグリッドソート列.引数初期値 => "Initial",
            ベンチマークグリッドソート列.引数データ => "Data",
            ベンチマークグリッドソート列.左日時 => "左 日時",
            ベンチマークグリッドソート列.左時間 => "左 時間",
            ベンチマークグリッドソート列.左割り当て => "左 メモリ",
            ベンチマークグリッドソート列.時間シェア => "時間シェア",
            ベンチマークグリッドソート列.メモリシェア => "メモリシェア",
            ベンチマークグリッドソート列.右日時 => "右 日時",
            ベンチマークグリッドソート列.右時間 => "右 時間",
            ベンチマークグリッドソート列.右割り当て => "右 メモリ",
            _ => 列.ToString(),
        };
}

public sealed record ベンチマークソート列選択肢(ベンチマークグリッドソート列 列,string 表示名);

/// <概要>選択/非選択タブヘッダーの配色を一元管理する。2つのタブストリップ(ソース/下段)で共有する。</概要>
public static class ベンチマークタブ見出しスタイル {
    public static FontAttributes フォント属性を取得(bool 選択中か値) => 選択中か値 ? FontAttributes.Bold : FontAttributes.None;
    public static Color 文字色を取得(bool 選択中か値) => 選択中か値 ? Color.FromArgb("#0F172A") : Color.FromArgb("#475569");
    public static Color 背景色を取得(bool 選択中か値) => 選択中か値 ? Colors.White : Color.FromArgb("#F1F5F9");
    public static Brush 枠線を取得(bool 選択中か値) => new SolidColorBrush(選択中か値 ? Color.FromArgb("#0F766E") : Color.FromArgb("#CBD5E1"));
}

/// <概要>タブヘッダーの選択状態を持つ共通基底。ソースタブ選択肢 と タブ見出し選択肢 が実装する。</概要>
public abstract class タブ見出し選択肢基底:ObservableObject {
    private bool 選択中か値;

    public bool 選択中か {
        get => this.選択中か値;
        private set {
            if(this.SetProperty(ref this.選択中か値,value)) {
                this.OnPropertyChanged(nameof(this.見出しフォント属性));
                this.OnPropertyChanged(nameof(this.見出し文字色));
                this.OnPropertyChanged(nameof(this.見出し背景色));
                this.OnPropertyChanged(nameof(this.見出し枠線));
            }
        }
    }

    public abstract string 見出し文字列 { get; }
    public FontAttributes 見出しフォント属性 => ベンチマークタブ見出しスタイル.フォント属性を取得(this.選択中か);
    public Color 見出し文字色 => ベンチマークタブ見出しスタイル.文字色を取得(this.選択中か);
    public Color 見出し背景色 => ベンチマークタブ見出しスタイル.背景色を取得(this.選択中か);
    public Brush 見出し枠線 => ベンチマークタブ見出しスタイル.枠線を取得(this.選択中か);

    public void 選択状態を設定(bool 値) {
        this.選択中か=値;
    }
}

/// <概要>進捗リング等の付帯情報を持たない、静的タイトルのタブヘッダー(下段タブ用)。</概要>
public sealed class タブ見出し選択肢(string 見出し文字列値):タブ見出し選択肢基底 {
    public override string 見出し文字列 { get; }=見出し文字列値;
}

public sealed class ソースタブ選択肢(ベンチマークソースタブ定義 定義):タブ見出し選択肢基底 {
    private static readonly Color         実行中インジケーター色= Color.FromArgb("#00FF00");
    private static readonly Color         完了進行外枠色    = Color.FromArgb("#22C55E");
    private static readonly Color         既定進行外枠色    = Color.FromArgb("#64748B");
    private                 ベンチマークタブ進行    進行値;
    private                 bool          実行中か値;
    private                 bool          最終完了か値;
    private                 bool          完了進行を最終完了として扱うか値;
    private                 string        ベンチマークシェア概要短文値 = ベンチマーク改善プロンプトビルダー.概要を作成([]).短縮表示文字列;
    public                  ベンチマークソースタブ定義 定義  { get; }=定義;
    public                  ベンチマークソースタブ   タブ  => this.定義.タブ;
    public                  string        ID  => this.定義.ID;
    public                  string        表示名 => this.定義.表示名;
    public ベンチマークタブ進行 進行 {
        get => this.進行値;
        private set {
            if(this.SetProperty(ref this.進行値,value)) {
                this.OnPropertyChanged(nameof(this.見出し文字列));
                this.OnPropertyChanged(nameof(this.進行ありか));
                this.OnPropertyChanged(nameof(this.完了か));
                this.OnPropertyChanged(nameof(this.完了アイコン表示中か));
                this.OnPropertyChanged(nameof(this.進行リング表示中か));
                this.OnPropertyChanged(nameof(this.実行スピナー表示中か));
                this.OnPropertyChanged(nameof(this.完了アイコンソース));
                this.OnPropertyChanged(nameof(this.進行割合));
                this.OnPropertyChanged(nameof(this.進行文字列));
                this.OnPropertyChanged(nameof(this.進行概要文字列));
                this.OnPropertyChanged(nameof(this.進行ツールチップ文字列));
                this.OnPropertyChanged(nameof(this.進行自動化状態));
                this.OnPropertyChanged(nameof(this.進行外枠色));
            }
        }
    }
    public override string 見出し文字列 => this.表示名;
    public bool 進行ありか => this.進行.合計数>0;
    public bool 完了か => this.進行.合計数>0&&this.進行.完了数>=this.進行.合計数;
    public bool 最終完了か {
        get => this.最終完了か値;
        private set {
            if(this.SetProperty(ref this.最終完了か値,value)) {
                this.OnPropertyChanged(nameof(this.完了アイコン表示中か));
                this.OnPropertyChanged(nameof(this.進行リング表示中か));
                this.OnPropertyChanged(nameof(this.実行スピナー表示中か));
                this.OnPropertyChanged(nameof(this.完了アイコンソース));
                this.OnPropertyChanged(nameof(this.進行ツールチップ文字列));
            }
        }
    }
    public bool 完了進行を最終完了として扱うか {
        get => this.完了進行を最終完了として扱うか値;
        private set {
            if(this.SetProperty(ref this.完了進行を最終完了として扱うか値,value)) {
                this.OnPropertyChanged(nameof(this.完了アイコン表示中か));
                this.OnPropertyChanged(nameof(this.進行リング表示中か));
                this.OnPropertyChanged(nameof(this.実行スピナー表示中か));
                this.OnPropertyChanged(nameof(this.完了アイコンソース));
                this.OnPropertyChanged(nameof(this.進行ツールチップ文字列));
            }
        }
    }
    public bool 完了アイコン表示中か => this.完了か||this.最終完了か;
    public bool 進行リング表示中か => this.進行ありか&&!this.完了アイコン表示中か;
    public bool 実行スピナー表示中か => this.進行ありか&&this.実行中か&&!this.完了アイコン表示中か;
    public string 完了アイコンソース => ベンチマーク状態画像名.完了;
    public double 進行割合 => this.進行.合計数<=0 ? 0 : this.進行.割合;
    public string 進行文字列 => this.進行.合計数<=0 ? "0/0" : this.進行.割合文字列;
    public string ベンチマークシェア概要短文 {
        get => this.ベンチマークシェア概要短文値;
        private set {
            if(this.SetProperty(ref this.ベンチマークシェア概要短文値,value)) {
                this.OnPropertyChanged(nameof(this.進行概要文字列));
                this.OnPropertyChanged(nameof(this.進行ツールチップ文字列));
            }
        }
    }
    public string 進行概要文字列 => this.進行接頭辞を作成()+","+this.ベンチマークシェア概要短文;
    public string 進行ツールチップ文字列 => this.進行概要文字列;
    public string 進行自動化状態 => this.進行ありか
        ? this.進行接頭辞を作成()
        : "未選択";
    public Color 進行外枠色 => this.実行中か
        ? 実行中インジケーター色
        : this.完了か ? 完了進行外枠色 : 既定進行外枠色;
    public bool 実行中か {
        get => this.実行中か値;
        private set {
            if(this.SetProperty(ref this.実行中か値,value)) {
                this.OnPropertyChanged(nameof(this.進行自動化状態));
                this.OnPropertyChanged(nameof(this.進行外枠色));
                this.OnPropertyChanged(nameof(this.進行概要文字列));
                this.OnPropertyChanged(nameof(this.進行ツールチップ文字列));
                this.OnPropertyChanged(nameof(this.完了アイコン表示中か));
                this.OnPropertyChanged(nameof(this.進行リング表示中か));
                this.OnPropertyChanged(nameof(this.実行スピナー表示中か));
                this.OnPropertyChanged(nameof(this.完了アイコンソース));
            }
        }
    }

    public void ベンチマークシェア概要短文を設定(string 値) {
        this.ベンチマークシェア概要短文=値;
    }

    private string 進行接頭辞を作成() => this.実行中か
        ? "実行中 "+this.進行文字列
        : this.完了アイコン表示中か ? "完了 "+this.進行文字列 : "待機 "+this.進行文字列;

    public void 進行を設定(ベンチマークタブ進行 値) {
        this.進行=値;
        if(this.完了か) {
            this.実行中か=false;
            return;
        }

        this.完了進行の最終完了扱いを設定(false);
        this.最終完了状態を設定(false);
    }

    public void 最終完了状態を設定(bool 値) {
        this.最終完了か=値;
        if(値) {
            this.実行中か=false;
        }
    }

    public void 完了進行の最終完了扱いを設定(bool 値) {
        this.完了進行を最終完了として扱うか=値;
    }

    public void 実行中状態を設定(bool 値) {
        if(値&&(this.完了か||this.最終完了か)) {
            return;
        }

        this.実行中か=値;
    }
}

public sealed class 監視可能範囲コレクション<T項目>:ObservableCollection<T項目> {
    public void 範囲を置換(IEnumerable<T項目> 項目一覧) {
        this.CheckReentrancy();
        this.Items.Clear();
        foreach(var 項目 in 項目一覧) {
            this.Items.Add(項目);
        }
        this.OnPropertyChanged(new PropertyChangedEventArgs(nameof(this.Count)));
        this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

public sealed class ベンチマークソースタブ表示状態(ベンチマークソースタブ定義 定義):ObservableObject {
    private  string                          ベンチマークシェア概要文値= string.Empty;
    private  bool?                           ベンチマーク選択状態値;
    public   ベンチマークソースタブ定義                   定義              { get; }     =定義;
    public   監視可能範囲コレクション<ベンチマーク表行>          行一覧             { get; }     = [];
    public   IReadOnlyList<ベンチマーク比較行>        比較行一覧           { get; set; }= [];

    public bool? ベンチマーク選択状態 {
        get => this.ベンチマーク選択状態値;
        set => this.SetProperty(ref this.ベンチマーク選択状態値,value);
    }

    public string ベンチマークシェア概要文 {
        get => this.ベンチマークシェア概要文値;
        set => this.SetProperty(ref this.ベンチマークシェア概要文値,value);
    }

}

public sealed record 実行プロファイル選択肢(string 表示名,ベンチマーク実行プロファイル プロファイル);

public sealed record 結果要素数選択肢(int 値,string 表示名);

public sealed record 閾値選択肢(string 表示名,int ミリ秒);

public sealed record ベンチマーク自動スクロール要求(ベンチマークソースタブ ソースタブ,string メソッド名,string 行キー,int 連番);

public sealed class ベンチマーク改善候補行(ベンチマーク改善候補 候補,bool 選択中か値):ObservableObject {
    private bool               選択中か値=選択中か値;
    public event EventHandler? 選択変更時;

    internal ベンチマーク改善候補 候補 { get; }=候補;
    public   string     キー => this.候補.キー;

    public bool 選択中か {
        get => this.選択中か値;
        set {
            if(this.SetProperty(ref this.選択中か値,value)) {
                this.選択変更時?.Invoke(this,EventArgs.Empty);
            }
        }
    }
}

public readonly record struct ベンチマークソート値(string? 文字列,double? 数値,bool 欠測か) {
    public static ベンチマークソート値 文字列から作成(string 文字列) => new(文字列,null,string.IsNullOrWhiteSpace(文字列));

    public static ベンチマークソート値 指標から作成(プロジェクト測定値 測定値値,ベンチマーク指標種別 種別) {
        if(!測定値値.測定値ありか) {
            return 欠測;
        }

        return 種別==ベンチマーク指標種別.時間
            ? 数値から作成(測定値値.時間ナノ秒)
            : 数値から作成(測定値値.割り当てバイト数);
    }

    public static ベンチマークソート値 シェアから作成(シェア値 シェア) =>
        数値から作成(シェア.左シェア);

    public static ベンチマークソート値 日時から作成(DateTimeOffset? 値) =>
        数値から作成(値?.ToUnixTimeMilliseconds());

    public static ベンチマークソート値 数値から作成(double? 値) {
        return 値 is { } 数値&&double.IsFinite(数値)
            ? new(null,数値,false)
            : 欠測;
    }

    public static readonly ベンチマークソート値 欠測 = new(null,null,true);

    public static int 比較(ベンチマークソート値 左,ベンチマークソート値 右,bool 降順か値) {
        if(左.欠測か&&右.欠測か) {
            return 0;
        }

        if(左.欠測か) {
            return 1;
        }

        if(右.欠測か) {
            return -1;
        }

        var 結果 = 左.数値 is { } 左数値&&右.数値 is { } 右数値
            ? 左数値.CompareTo(右数値)
            : string.Compare(左.文字列,右.文字列,StringComparison.OrdinalIgnoreCase);
        return 降順か値 ? -結果 : 結果;
    }
}

public sealed class ベンチマークプロジェクトセル:ObservableObject {
    private static readonly Color 既定実行文字色 = Color.FromArgb("#334155");
    private static readonly Color 既定指標文字色 = Color.FromArgb("#1E293B");
    private static readonly Color 失敗実行文字色値 = Color.FromArgb("#DC2626");
    private const string メタデータ欠測文字列 = "CPU/日時不明";

    private readonly ベンチマーク表行 親行;
    private bool UI選択中か値;
    private bool 失敗か値;
    private bool 測定なし完了か値;
    private string 状態画像ソース値;
    private string 状態文字列値;
    private string 要素数文字列値 = string.Empty;
    private string 実行文字列値 = string.Empty;
    private string 失敗実行文字列値 = string.Empty;
    private string 失敗理由文字列値 = string.Empty;
    private string 実行ツールチップ文字列値 = string.Empty;
    private string 結果確認文字列値 = string.Empty;
    private string 結果シード文字列値 = string.Empty;
    private string 論理プロセッサ文字列値 = string.Empty;
    private string コア種別ラベル値 = string.Empty;
    private string コア種別画像ソース値 = string.Empty;
    private string 時間文字列値 = string.Empty;
    private string 割り当て文字列値 = string.Empty;
    private Color 実行文字色値 = 既定実行文字色;
    private Color 指標文字色値 = 既定指標文字色;
    private FontAttributes 実行フォント属性値;
    private FontAttributes 指標フォント属性値;
    private bool 実行中か値;
    private bool CPU表示ありか値;
    private ベンチマークプロジェクト実行状態 実行時状態値;
    private DateTimeOffset? 最終測定日時値;
    private int? 最終成功要素数値;
    private int? 最終ループ回数値;
    private double? 最終合計ナノ秒値;
    private int? 最終失敗要素数値;
    private DateTimeOffset? 最終失敗日時値;
    private ベンチマーク測定強調状態 測定強調状態値;
    private プロジェクト測定値 測定値値 = プロジェクト測定値.欠測;
    private Color バッジ背景色値 = Color.FromArgb("#E2E8F0");
    private Color バッジ文字色値 = Color.FromArgb("#334155");
    private bool 行最終完了か値;
    private bool 今回実行待機中か値;

    public ベンチマークプロジェクトセル(ベンチマーク表行 親行,string プロジェクト,bool ケースありか) {
        this.親行=親行;
        this.プロジェクト=プロジェクト;
        this.ケースありか=ケースありか;
        this.状態画像ソース値=ケースありか ? ベンチマーク状態画像名.待機中 : string.Empty;
        this.状態文字列値=ケースありか ? "待機" : "対象なし";
        this.実行文字列値=ケースありか ? string.Empty : "対象なし";
    }

    public event EventHandler? UI選択変更時;

    public string プロジェクト { get; }
    public bool ケースありか { get; }
    public プロジェクト測定値 測定値 => this.測定値値;
    public DateTimeOffset? 最終測定日時 => this.最終測定日時値;
    public DateTimeOffset? 最新表示日時 {
        get {
            if(this.最終測定日時値 is null) {
                return this.最終失敗日時値;
            }
            if(this.最終失敗日時値 is null||this.最終測定日時値>=this.最終失敗日時値) {
                return this.最終測定日時値;
            }
            return this.最終失敗日時値;
        }
    }
    public int? 最終成功要素数 => this.最終成功要素数値;
    public int? 最終ループ回数 => this.最終ループ回数値;
    public double? 最終合計ナノ秒 => this.最終合計ナノ秒値;
    public string 時間ツールチップ文字列 => 時間ツールチップを作成(this.時間文字列,this.最終合計ナノ秒値,this.最終ループ回数値);
    public int? 最終失敗要素数 => this.最終失敗要素数値;
    public DateTimeOffset? 最終失敗日時 => this.最終失敗日時値;
    public ベンチマーク測定強調状態 測定強調状態 => this.測定強調状態値;

    public bool 実行予定か => this.親行.選択中か&&this.ケースありか;

    public void 行選択変更を反映() {
        this.状態表示を更新();
    }

    public bool UI選択中か {
        get => this.UI選択中か値;
        set {
            if(this.SetProperty(ref this.UI選択中か値,this.ケースありか&&value)) {
                this.UI選択変更時?.Invoke(this,EventArgs.Empty);
            }
        }
    }

    public string 状態画像ソース {
        get => this.状態画像ソース値;
        private set => this.SetProperty(ref this.状態画像ソース値,value);
    }

    public bool 状態アイコン表示中か => !string.IsNullOrWhiteSpace(this.状態画像ソース)&&!this.実行中表示か;

    public string 状態文字列 {
        get => this.状態文字列値;
        private set => this.SetProperty(ref this.状態文字列値,value);
    }

    public string 要素数文字列 {
        get => this.要素数文字列値;
        private set => this.SetProperty(ref this.要素数文字列値,value);
    }

    public string 実行文字列 {
        get => this.実行文字列値;
        private set => this.SetProperty(ref this.実行文字列値,value);
    }

    public string 失敗実行文字列 {
        get => this.失敗実行文字列値;
        private set => this.SetProperty(ref this.失敗実行文字列値,value);
    }

    public string 実行ツールチップ文字列 {
        get => this.実行ツールチップ文字列値;
        private set => this.SetProperty(ref this.実行ツールチップ文字列値,value);
    }

    public string 結果確認文字列 {
        get => this.結果確認文字列値;
        private set => this.SetProperty(ref this.結果確認文字列値,value);
    }

    public string 結果シード文字列 {
        get => this.結果シード文字列値;
        private set => this.SetProperty(ref this.結果シード文字列値,value);
    }

    public string 論理プロセッサ文字列 {
        get => this.論理プロセッサ文字列値;
        private set => this.SetProperty(ref this.論理プロセッサ文字列値,value);
    }

    public string コア種別ラベル {
        get => this.コア種別ラベル値;
        private set => this.SetProperty(ref this.コア種別ラベル値,value);
    }

    public string コア種別画像ソース {
        get => this.コア種別画像ソース値;
        private set => this.SetProperty(ref this.コア種別画像ソース値,value);
    }

    public string 時間文字列 {
        get => this.時間文字列値;
        private set => this.SetProperty(ref this.時間文字列値,value);
    }

    public string 割り当て文字列 {
        get => this.割り当て文字列値;
        private set => this.SetProperty(ref this.割り当て文字列値,value);
    }

    public Color 実行文字色 {
        get => this.実行文字色値;
        private set => this.SetProperty(ref this.実行文字色値,value);
    }

    public Color 失敗実行文字色 => 失敗実行文字色値;

    public Color 指標文字色 {
        get => this.指標文字色値;
        private set => this.SetProperty(ref this.指標文字色値,value);
    }

    public FontAttributes 実行フォント属性 {
        get => this.実行フォント属性値;
        private set => this.SetProperty(ref this.実行フォント属性値,value);
    }

    public FontAttributes 指標フォント属性 {
        get => this.指標フォント属性値;
        private set => this.SetProperty(ref this.指標フォント属性値,value);
    }

    public bool 実行中か {
        get => this.実行中か値;
        private set {
            if(this.SetProperty(ref this.実行中か値,value)) {
                this.状態表示を更新();
            }
        }
    }

    public bool CPU表示ありか {
        get => this.CPU表示ありか値;
        private set => this.SetProperty(ref this.CPU表示ありか値,value);
    }

    public Color バッジ背景色 {
        get => this.バッジ背景色値;
        private set => this.SetProperty(ref this.バッジ背景色値,value);
    }

    public Color バッジ文字色 {
        get => this.バッジ文字色値;
        private set => this.SetProperty(ref this.バッジ文字色値,value);
    }

    public ベンチマークプロジェクト実行状態 実行時状態 {
        get => this.実行時状態値;
        private set {
            if(this.SetProperty(ref this.実行時状態値,value)) {
                this.OnPropertyChanged(nameof(this.待機表示か));
                this.OnPropertyChanged(nameof(this.実行中表示か));
                this.OnPropertyChanged(nameof(this.状態アイコン表示中か));
            }
        }
    }

    public bool 待機表示か => ベンチマークプロジェクト実行状態ロジック.待機中か(this.実行時状態,this.測定値.測定値ありか);

    public bool 実行中表示か => ベンチマークプロジェクト実行状態ロジック.実行中か(this.実行時状態);

    public bool 行最終完了か => this.行最終完了か値;

    public void 行最終完了状態を設定(bool 値) {
        if(this.SetProperty(ref this.行最終完了か値,値,nameof(this.行最終完了か))) {
            this.状態表示を更新();
        }
    }

    public void 今回実行待機を設定(bool 値) {
        if(this.今回実行待機中か値!=値) {
            this.今回実行待機中か値=値;
            this.状態表示を更新();
        }
    }

    public void 測定値を適用(プロジェクト測定値 値,bool 維持表示を保持するか = false) {
        // 維持表示を保持するか=true のときは、完了済みセルを欠測/待機に戻さない。
        // 途中で比較行が欠けても、最後に見えていた確定値を画面上に残す。
        if(!値.測定値ありか&&維持表示を保持するか&&(this.測定値値.測定値ありか||this.測定なし完了か値||this.実行中か)) {
            this.失敗か値=false;
            if(this.実行中か) {
                this.実行時状態=ベンチマークプロジェクト実行状態.実行中;
            } else {
                this.測定なし完了か値=!this.測定値値.測定値ありか;
                this.実行時状態=ベンチマークプロジェクト実行状態.完了;
            }
            this.OnPropertyChanged(nameof(this.待機表示か));
            this.状態表示を更新();
            return;
        }

        if(!値.測定値ありか) {
            this.測定値を設定(プロジェクト測定値.欠測);
            this.失敗か値=false;
            this.測定なし完了か値=false;
            this.時間文字列=string.Empty;
            this.割り当て文字列=string.Empty;
            this.要素数文字列=string.Empty;
            this.実行文字列=string.Empty;
            this.失敗実行文字列=string.Empty;
            this.失敗理由文字列値=string.Empty;
            this.実行ツールチップ文字列=string.Empty;
            this.結果確認文字列=string.Empty;
            this.結果シード文字列=string.Empty;
            this.CPU表示をクリア();
            this.測定強調を適用(ベンチマーク測定強調状態.なし);
            this.OnPropertyChanged(nameof(this.待機表示か));
            this.状態表示を更新();
            return;
        }

        this.測定値を設定(値);
        this.測定なし完了か値=false;
        this.時間文字列=時間を整形(値);
        this.割り当て文字列=割り当てを整形(値);
        this.OnPropertyChanged(nameof(this.待機表示か));
        if(値.測定値ありか) {
            this.失敗か値=false;
        }
        this.状態表示を更新();
    }

    public void 保存結果を適用(ベンチマーク保存結果 結果) {
        var 測定日時 = 結果.元データ更新日時??結果.測定日時;
        this.最終測定日時値=測定日時;
        var 結果測定値 = 結果.プロジェクト測定値に変換();
        if(結果測定値.測定値ありか) {
            this.成功表示を適用(結果測定値,結果.要素数,測定日時,ベンチマーク測定強調状態.なし);
            this.失敗表示をクリア();
        } else {
            this.測定値を適用(結果測定値);
            if(string.Equals(結果.状態,"Failed",StringComparison.OrdinalIgnoreCase)) {
                this.失敗表示を適用(結果.要素数,測定日時,string.Empty);
            } else if(string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase)) {
                this.測定なし完了か値=true;
                this.要素数文字列=要素数を整形(結果.要素数);
                this.実行文字列=日時を整形(測定日時);
                this.実行ツールチップ文字列を更新();
            }
        }
        this.実行中か=false;
        this.失敗か値=string.Equals(結果.状態,"Failed",StringComparison.OrdinalIgnoreCase);
        this.結果確認文字列=string.Empty;
        this.結果シード文字列=string.Empty;
        this.実行時状態=ベンチマークプロジェクト実行状態.待機中;
        this.CPU表示をクリア();
        this.測定強調を適用(ベンチマーク測定強調状態.なし);
        this.状態表示を更新();
    }

    public void 実行結果を適用(ベンチマーク結果項目 結果,CPU選択項目? CPU,DateTimeOffset 完了日時,bool 完了を強調するか = true) {
        // 今回実行の結果(実行中/終端いずれも)が届いた時点で、実行待ちの時計表示は終わり。
        this.今回実行待機中か値=false;
        var 完了か = string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase);
        var 失敗か値 = string.Equals(結果.状態,"Failed",StringComparison.OrdinalIgnoreCase);
        var 実行中か値 = ジョブ状態名.保存値と一致するか(結果.状態,ジョブ状態.実行中);
        if(実行中か値) {
            // Running は確定値の更新ではなく、実行中の状態表示だけを切り替える。
            this.実行中か=true;
            this.失敗か値=false;
            this.測定なし完了か値=false;
            this.失敗実行文字列=string.Empty;
            this.失敗理由文字列値=string.Empty;
            if(結果.要素数>0) {
                this.要素数文字列=要素数を整形(結果.要素数);
            }
            this.実行文字列=日時を整形(ベンチマーク実行日時表示ロジック.実行中日時を解決(結果.開始日時,完了日時));
            this.実行ツールチップ文字列を更新();
            this.CPU表示を適用(CPU);
            this.実行時状態=ベンチマークプロジェクト実行状態.実行中;
            this.状態表示を更新();
            return;
        }

        this.結果確認文字列=結果.結果確認文字列;
        this.結果シード文字列=結果.結果シード文字列;

        if(結果.要素数<=0) {
            // 要素数=0 の コールバック は比較表の確定データとして扱わない。
            this.実行中か=false;
            this.CPU表示を適用(CPU,欠測時に維持するか: true);
            this.実行時状態=this.測定値.測定値ありか||this.測定なし完了か値
                ? ベンチマークプロジェクト実行状態.完了
                : ベンチマークプロジェクト実行状態.待機中;
            this.状態表示を更新();
            return;
        }

        if(完了か&&結果.平均ナノ秒 is not null) {
            // Completed かつ測定値ありだけが、表の確定値を更新する。
            this.失敗か値=false;
            this.測定なし完了か値=false;
            this.最終測定日時値=完了日時;
            this.成功表示を適用(
                プロジェクト測定値.測定から作成(結果.平均ナノ秒,結果.割り当てバイト数値),
                結果.要素数,
                完了日時,
                完了を強調するか ? ベンチマーク測定強調状態.新規 : ベンチマーク測定強調状態.なし);
            this.最終ループ回数値=結果.ループ回数;
            this.最終合計ナノ秒値=結果.合計ナノ秒;
            this.OnPropertyChanged(nameof(this.最終ループ回数));
            this.OnPropertyChanged(nameof(this.最終合計ナノ秒));
            this.OnPropertyChanged(nameof(this.時間ツールチップ文字列));
            this.失敗表示をクリア();
        } else if(完了か) {
            // Completed でも測定値がない場合は、待機に戻さず「測定値なし」として終端表示する。
            this.失敗か値=false;
            this.測定なし完了か値=true;
            this.最終測定日時値=完了日時;
            this.要素数文字列=要素数を整形(結果.要素数);
            this.実行文字列=日時を整形(完了日時);
            this.実行ツールチップ文字列を更新();
            this.失敗表示をクリア();
        } else if(失敗か値) {
            this.失敗か値=true;
            this.測定なし完了か値=false;
            this.最終測定日時値=完了日時;
            this.失敗表示を適用(結果.要素数,完了日時,結果.失敗理由);
        }
        this.実行中か=false;
        this.CPU表示を適用(CPU,欠測時に維持するか: true);
        this.実行時状態=完了か
            ? ベンチマークプロジェクト実行状態.完了
            : ベンチマークプロジェクト実行状態.待機中;
        this.状態表示を更新();
    }

    // 保存Runロードでは、DBの1行を画面グリッド1行としてそのまま復元する。
    // CPU/日時/要素数も現在のCPUリストやカタログに再照合せず、保存値を表示値として反映する。
    public void スナップショット結果を適用(ベンチマーク結果項目 結果,int? CPU番号,DateTimeOffset? 測定日時) {
        this.スナップショット結果を適用(結果,null,CPU番号,測定日時);
    }

    public void スナップショット結果を適用(ベンチマーク結果項目 結果,CPU選択項目? CPU,int? CPU番号,DateTimeOffset? 測定日時) {
        var 完了か = string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase);
        var 失敗か値 = string.Equals(結果.状態,"Failed",StringComparison.OrdinalIgnoreCase);
        this.結果確認文字列=結果.結果確認文字列;
        this.結果シード文字列=結果.結果シード文字列;
        this.実行中か=false;
        this.実行時状態=完了か
            ? ベンチマークプロジェクト実行状態.完了
            : ベンチマークプロジェクト実行状態.待機中;
        if(完了か&&結果.平均ナノ秒 is not null) {
            this.測定値を設定(プロジェクト測定値.測定から作成(結果.平均ナノ秒,結果.割り当てバイト数値));
            this.失敗か値=false;
            this.測定なし完了か値=false;
            this.最終成功要素数値=結果.要素数>0 ? 結果.要素数 : null;
            this.最終ループ回数値=結果.ループ回数;
            this.最終合計ナノ秒値=結果.合計ナノ秒;
            this.OnPropertyChanged(nameof(this.最終ループ回数));
            this.OnPropertyChanged(nameof(this.最終合計ナノ秒));
            this.最終測定日時値=測定日時;
            this.時間文字列=時間を整形(this.測定値値);
            this.割り当て文字列=割り当てを整形(this.測定値値);
            this.要素数文字列=結果.要素数>0 ? 要素数を整形(結果.要素数) : string.Empty;
            this.実行文字列=測定日時 is { } 完了日時 ? 日時を整形(完了日時) : string.Empty;
            this.失敗表示をクリア();
            this.OnPropertyChanged(nameof(this.時間ツールチップ文字列));
        } else if(完了か) {
            this.測定値を設定(プロジェクト測定値.欠測);
            this.失敗か値=false;
            this.測定なし完了か値=true;
            this.最終成功要素数値=結果.要素数>0 ? 結果.要素数 : null;
            this.最終測定日時値=測定日時;
            this.時間文字列=string.Empty;
            this.割り当て文字列=string.Empty;
            this.要素数文字列=結果.要素数>0 ? 要素数を整形(結果.要素数) : string.Empty;
            this.実行文字列=測定日時 is { } 完了日時 ? 日時を整形(完了日時) : string.Empty;
            this.失敗表示をクリア();
        } else if(失敗か値) {
            this.測定値を設定(プロジェクト測定値.欠測);
            this.失敗か値=true;
            this.測定なし完了か値=false;
            this.最終失敗要素数値=結果.要素数>0 ? 結果.要素数 : null;
            this.最終失敗日時値=測定日時;
            this.最終測定日時値=測定日時;
            this.時間文字列=string.Empty;
            this.割り当て文字列=string.Empty;
            this.要素数文字列=結果.要素数>0 ? 要素数を整形(結果.要素数) : string.Empty;
            this.失敗実行文字列=測定日時 is { } 失敗日時 ? "  "+日時を整形(失敗日時) : string.Empty;
            this.失敗理由文字列値=ベンチマーク失敗表示ロジック.ツールチップ理由を作成(結果.失敗理由);
            this.実行文字列=string.Empty;
        } else {
            this.測定値を適用(プロジェクト測定値.欠測);
        }

        if(CPU is not null) {
            this.CPU表示を適用(CPU);
        } else {
            this.CPU番号表示を適用(CPU番号);
        }
        this.測定強調を適用(ベンチマーク測定強調状態.なし);
        this.実行ツールチップ文字列を更新();
        this.状態表示を更新();
    }

    public void 結果確認を適用(string 確認文字列,string シード文字列) {
        this.結果確認文字列=確認文字列;
        this.結果シード文字列=シード文字列;
    }

    public void スキップを適用(int 停止要素数,プロジェクト測定値 停止時測定値) {
        this.スキップを適用(停止要素数,停止時測定値,null,null,false);
    }

    public void スキップを適用(int 停止要素数,プロジェクト測定値 停止時測定値,CPU選択項目? CPU,DateTimeOffset? 測定日時,bool メタデータ欠測か) {
        // adaptive で止まった行は、低い N の completed 測定値値 をそのまま表示する。
        this.測定値を設定(停止時測定値);
        this.測定なし完了か値=false;
        this.時間文字列=時間を整形(停止時測定値);
        this.割り当て文字列=割り当てを整形(停止時測定値);
        this.実行中か=false;
        this.失敗か値=false;
        this.最終成功要素数値=停止要素数;
        this.最終測定日時値=測定日時;
        this.要素数文字列=要素数を整形(停止要素数);
        // 日時と論理コアは独立に判定する: 片方のメタデータが欠けていても、判明している方は表示し続ける。
        this.実行文字列=測定日時 is null ? メタデータ欠測文字列 : 日時を整形(測定日時.Value);
        this.失敗実行文字列=string.Empty;
        this.CPU表示を適用(CPU,欠測時に維持するか: true);
        this.実行ツールチップ文字列を更新();
        this.測定強調を適用(ベンチマーク測定強調状態.なし);
        this.実行時状態=ベンチマークプロジェクト実行状態.完了;
        this.OnPropertyChanged(nameof(this.待機表示か));
        this.OnPropertyChanged(nameof(this.最終成功要素数));
        this.状態表示を更新();
    }

    public void 実行時状態をクリア() {
        this.実行中か=false;
        this.今回実行待機中か値=false;
        this.実行時状態=this.測定値.測定値ありか||this.測定なし完了か値
            ? ベンチマークプロジェクト実行状態.完了
            : ベンチマークプロジェクト実行状態.待機中;
        this.状態表示を更新();
    }

    public void 実行要素の完了強調を維持(int 実行要素数) {
        this.測定強調を適用(ベンチマーク測定強調ロジック.実行要素数へ進める(
            this.測定強調状態,
            this.最終成功要素数,
            実行要素数));
    }

    public void 実行強調をクリア() {
            this.測定強調を適用(ベンチマーク測定強調ロジック.実行後にクリア());
    }

    public void 測定値をクリア() {
        this.測定値を設定(プロジェクト測定値.欠測);
        this.失敗か値=false;
        this.測定なし完了か値=false;
        this.最終成功要素数値=null;
        this.最終ループ回数値=null;
        this.最終合計ナノ秒値=null;
        this.OnPropertyChanged(nameof(this.最終ループ回数));
        this.OnPropertyChanged(nameof(this.最終合計ナノ秒));
        this.OnPropertyChanged(nameof(this.時間ツールチップ文字列));
        this.最終測定日時値=null;
        this.最終失敗要素数値=null;
        this.最終失敗日時値=null;
        this.失敗理由文字列値=string.Empty;
        this.時間文字列=string.Empty;
        this.割り当て文字列=string.Empty;
        this.要素数文字列=string.Empty;
        this.実行時状態をクリア();
        if(this.ケースありか) {
            this.状態画像ソース=ベンチマーク状態画像名.待機中;
            this.状態文字列="待機";
            this.実行文字列=string.Empty;
            this.失敗実行文字列=string.Empty;
            this.失敗理由文字列値=string.Empty;
            this.実行ツールチップ文字列=string.Empty;
            this.結果確認文字列=string.Empty;
            this.結果シード文字列=string.Empty;
            this.CPU表示をクリア();
            this.測定強調を適用(ベンチマーク測定強調状態.なし);
            this.状態表示を更新();
        }
    }

    private void 成功表示を適用(
        プロジェクト測定値 値,
        int 要素数,
        DateTimeOffset 測定日時,
        ベンチマーク測定強調状態 強調状態) {
        this.測定値を設定(値);
        this.失敗か値=false;
        this.測定なし完了か値=false;
        this.最終成功要素数値=要素数;
        this.最終測定日時値=測定日時;
        this.時間文字列=時間を整形(値);
        this.割り当て文字列=割り当てを整形(値);
        this.要素数文字列=要素数を整形(要素数);
        this.実行文字列=日時を整形(測定日時);
        this.実行ツールチップ文字列を更新();
        this.測定強調を適用(強調状態);
        this.OnPropertyChanged(nameof(this.待機表示か));
        this.OnPropertyChanged(nameof(this.最終成功要素数));
    }

    private void 測定値を設定(プロジェクト測定値 値) {
        this.測定値値=値;
        this.OnPropertyChanged(nameof(this.測定値));
    }

    private void 失敗表示を適用(int 要素数,DateTimeOffset 失敗日時,string 失敗理由) {
        this.失敗か値=true;
        this.測定なし完了か値=false;
        this.最終失敗要素数値=要素数;
        this.最終失敗日時値=失敗日時;
        this.失敗理由文字列値=ベンチマーク失敗表示ロジック.ツールチップ理由を作成(失敗理由);
        this.要素数文字列=要素数を整形(要素数);
        this.失敗実行文字列="  "+日時を整形(失敗日時);
        this.実行ツールチップ文字列を更新();
    }

    private void 失敗表示をクリア() {
        this.最終失敗要素数値=null;
        this.最終失敗日時値=null;
        this.失敗理由文字列値=string.Empty;
        this.失敗実行文字列=string.Empty;
        this.実行ツールチップ文字列を更新();
    }

    private void 実行ツールチップ文字列を更新() {
        var 失敗文字列 = this.最終失敗要素数値 is { } 失敗要素数&&this.最終失敗日時値 is { } 失敗日時
            ? "失敗 "+要素数を整形(失敗要素数)+" "+日時を整形(失敗日時)
            : string.Empty;
        this.実行ツールチップ文字列=string.Join(
            Environment.NewLine,
            new[] { 要素ツールチップを整形(this.要素数文字列,this.実行文字列),失敗文字列,this.失敗理由文字列値 }.Where(static 文字列 => !string.IsNullOrWhiteSpace(文字列)));
    }

    private void 測定強調を適用(ベンチマーク測定強調状態 値) {
        if(this.SetProperty(ref this.測定強調状態値,値,nameof(this.測定強調状態))) {
            this.OnPropertyChanged(nameof(this.測定強調状態));
        }

        this.実行文字色=既定実行文字色;
        this.指標文字色=既定指標文字色;
        this.実行フォント属性=FontAttributes.None;
        this.指標フォント属性=FontAttributes.None;
    }

    private void 状態表示を更新() {
        var 種別 = ベンチマーク状態画像ロジック.プロジェクト画像種別を取得(new ベンチマーク状態画像入力(
            this.ケースありか,
            this.実行中か,
            this.失敗か値,
            this.測定値.測定値ありか,
            this.測定なし完了か値,
            this.実行予定か,
            this.行最終完了か,
            this.今回実行待機中か値));
        this.状態画像ソース=ベンチマーク状態画像名.種別から取得(種別);
        if(!this.ケースありか) {
            this.状態文字列="対象なし";
        } else if(this.実行中か) {
            this.状態文字列="実行中";
        } else if(this.失敗か値) {
            this.状態文字列="失敗";
        } else if(this.測定値.測定値ありか) {
            this.状態文字列="完了";
        } else if(this.測定なし完了か値) {
            this.状態文字列="測定値なし";
        } else if(this.実行予定か) {
            this.状態文字列="待機";
        } else {
            this.状態文字列="予定なし";
        }

        this.OnPropertyChanged(nameof(this.状態アイコン表示中か));
    }

    private static string 要素数を整形(int 要素数) =>
        要素数.ToString(CultureInfo.InvariantCulture);

    private static string 日時を整形(DateTimeOffset 日時) =>
        ベンチマーク日時表示.ローカル日時を整形(日時);

    private static string 要素ツールチップを整形(string 要素数文字列値,string 日時文字列) =>
        string.IsNullOrWhiteSpace(要素数文字列値)
            ? 日時文字列
            : string.IsNullOrWhiteSpace(日時文字列)
                ? 要素数文字列値
                : 要素数文字列値+" "+日時文字列;

    private static string 時間を整形(プロジェクト測定値 測定値値) {
        return 測定値値.測定値ありか&&測定値値.時間ナノ秒 is { } 値
            ? ベンチマークフォーマッター.ナノ秒を整形(値)
            : string.Empty;
    }

    private static string 時間ツールチップを作成(string 一回あたり文字列,double? 合計ナノ秒,int? ループ回数) {
        if(string.IsNullOrWhiteSpace(一回あたり文字列)) {
            return string.Empty;
        }

        var 値一覧 = new List<string> { "1回あたり: "+一回あたり文字列 };
        if(合計ナノ秒 is { } 合計) {
            値一覧.Add("最終バッチ合計: "+ベンチマークフォーマッター.ナノ秒を整形(合計));
        }
        if(ループ回数 is { } ループ&&ループ>0) {
            値一覧.Add("ループ回数: "+ループ.ToString(CultureInfo.InvariantCulture));
        }
        return string.Join(Environment.NewLine,値一覧);
    }

    private void CPU表示を適用(CPU選択項目? CPU,bool 欠測時に維持するか = false) {
        if(CPU is null) {
            // 終端状態の適用で CPU を解決できなかった場合は、実行中に表示していた論理コアを保持する
            // (実装未完了などで測定値が無くても、実行したコアは変わらないため表示し続ける)。
            if(!欠測時に維持するか) {
                this.CPU表示をクリア();
            }
            return;
        }

        this.CPU表示ありか=true;
        this.論理プロセッサ文字列=CPU.論理プロセッサ文字列;
        this.コア種別ラベル=CPU.コア種別ラベル;
        this.コア種別画像ソース=CPU.コア種別画像ソース;
        this.バッジ背景色=CPU.バッジ背景色;
        this.バッジ文字色=CPU.バッジ文字色;
    }

    private void CPU番号表示を適用(int? CPU番号) {
        if(CPU番号 is not { } 値) {
            this.CPU表示をクリア();
            return;
        }

        this.CPU表示ありか=true;
        this.論理プロセッサ文字列=値.ToString(CultureInfo.InvariantCulture);
        this.コア種別ラベル=string.Empty;
        this.コア種別画像ソース=string.Empty;
        this.バッジ背景色=Color.FromArgb("#E2E8F0");
        this.バッジ文字色=Color.FromArgb("#334155");
    }

    private void CPU表示をクリア() {
        this.CPU表示ありか=false;
        this.論理プロセッサ文字列=string.Empty;
        this.コア種別ラベル=string.Empty;
        this.コア種別画像ソース=string.Empty;
        this.バッジ背景色=Color.FromArgb("#E2E8F0");
        this.バッジ文字色=Color.FromArgb("#334155");
    }

    private static string 割り当てを整形(プロジェクト測定値 測定値値) {
        return 測定値値.測定値ありか&&測定値値.割り当てバイト数 is { } 値
            ? ベンチマークフォーマッター.バイト数を整形(値)
            : string.Empty;
    }
}

public sealed class ベンチマーク列選択グループ(string id,string 見出し文字列値,IReadOnlyList<string> 列キー一覧):ObservableObject {
    private string 見出し文字列値=見出し文字列値;
    public  string ID { get; }=id;
    public string 見出し文字列 {
        get => this.見出し文字列値;
        private set => this.SetProperty(ref this.見出し文字列値,value);
    }
    public IReadOnlyList<string> 列キー一覧 { get; }=列キー一覧;
    public void 見出し文字列を設定(string 値) {
        this.見出し文字列=値;
    }
}

public sealed class ベンチマーク表行:ObservableObject {
    private static readonly Color 既定指標文字色 = Color.FromArgb("#1E293B");
    private static readonly Color 高性能指標文字色 = Color.FromArgb("#2563EB");
    private static readonly Color 低性能指標文字色 = Color.FromArgb("#DC2626");
    private bool 選択中か値;
    private bool UI選択中か値;
    private bool? 選択チェック状態値;
    private string 時間シェア文字列 = string.Empty;
    private string メモリシェア文字列 = string.Empty;
    private string 停止要素数文字列値 = string.Empty;
    private Color 停止要素数文字色値 = 既定停止要素数文字色;
    private FontAttributes 停止要素数フォント属性値;
    private string 左時間文字列値 = string.Empty;
    private string 左割り当て文字列値 = string.Empty;
    private string 右時間文字列値 = string.Empty;
    private string 右割り当て文字列値 = string.Empty;
    private string ベンチマーク名値 = string.Empty;
    private string ベンチマーク型文字列値 = string.Empty;
    private string ベンチマークメソッド文字列値 = string.Empty;
    private readonly string スナップショットベンチマーク型文字列;
    private bool CPU表示ありか値;
    private string 論理プロセッサ文字列値 = string.Empty;
    private string コア種別ラベル値 = string.Empty;
    private string コア種別画像ソース値 = string.Empty;
    private Color バッジ背景色値 = Color.FromArgb("#E2E8F0");
    private Color バッジ文字色値 = Color.FromArgb("#334155");
    private double? 論理CPUソート値値;
    private bool 最終完了か値;
    private static readonly Color 既定停止要素数文字色 = Color.FromArgb("#334155");

    public ベンチマーク表行(
        string 行キー,
        string メソッド名,
        string ベンチマーク名値,
        ベンチマークソースタブ定義 ソースタブ,
        IReadOnlyDictionary<string,ベンチマークケース> プロジェクト別ケース)
        : this(行キー,メソッド名,ベンチマーク名値,ソースタブ,プロジェクト別ケース,string.Empty) {
    }

    public ベンチマーク表行(
        string 行キー,
        string メソッド名,
        string ベンチマーク名値,
        ベンチマークソースタブ定義 ソースタブ,
        IReadOnlyDictionary<string,ベンチマークケース> プロジェクト別ケース,
        string スナップショットベンチマーク型文字列) {
        this.行キー=行キー;
        this.メソッド名=メソッド名;
        this.ベンチマーク名値=ベンチマーク名値;
        // 保存Runの「型」は完全型名ではなく、グリッドの型列に出す表示文字列。
        // ベンチマーク型文字列を作成("int") のような再フォーマットを通すと空になるため、明示値を保持する。
        this.スナップショットベンチマーク型文字列=スナップショットベンチマーク型文字列;
        this.ソースタブ=ソースタブ;
        this.プロジェクト別ケース=プロジェクト別ケース;
        this.ベンチマーク表示部分を更新();
        // セルの初期状態表示（待機/対象なし）が親行の選択を参照するため、セル生成前に行選択を確定する。
        this.実行対象ありか=ソースタブ.プロジェクト順.Any(プロジェクト別ケース.ContainsKey);
        this.選択中か値=this.実行対象ありか;
        this.選択チェック状態値=this.選択中か値;
        this.左セル=this.セルを作成(ソースタブ.左プロジェクト);
        this.右セル=this.セルを作成(ソースタブ.右プロジェクト);
        foreach(var セル in this.プロジェクトセル一覧) {
            セル.UI選択変更時+=this.セルUI選択変更時;
            セル.PropertyChanged+=this.セルプロパティ変更時;
        }
        this.UI選択状態を更新();
    }

    public string 行キー { get; }
    public string メソッド名 { get; }
    public string ベンチマーク名 {
        get => this.ベンチマーク名値;
        private set {
            if(this.SetProperty(ref this.ベンチマーク名値,value)) {
                this.ベンチマーク表示部分を更新();
            }
        }
    }
    public string ベンチマーク型文字列 {
        get => this.ベンチマーク型文字列値;
        private set => this.SetProperty(ref this.ベンチマーク型文字列値,value);
    }
    public string ベンチマークメソッド文字列 {
        get => this.ベンチマークメソッド文字列値;
        private set => this.SetProperty(ref this.ベンチマークメソッド文字列値,value);
    }

    private static string 時間ツールチップを作成(string 一回あたり文字列,double? 合計ナノ秒,int? ループ回数) {
        if(string.IsNullOrWhiteSpace(一回あたり文字列)) {
            return string.Empty;
        }

        var 値一覧 = new List<string> { "1回あたり: "+一回あたり文字列 };
        if(合計ナノ秒 is { } 合計) {
            値一覧.Add("最終バッチ合計: "+ベンチマークフォーマッター.ナノ秒を整形(合計));
        }
        if(ループ回数 is { } ループ&&ループ>0) {
            値一覧.Add("ループ回数: "+ループ.ToString(CultureInfo.InvariantCulture));
        }
        return string.Join(Environment.NewLine,値一覧);
    }
    public TextDecorations ベンチマーク識別文字装飾 =>
        this.プロジェクトセル一覧.Any(static セル => セル.ケースありか)
            ? TextDecorations.None
            : TextDecorations.Strikethrough;
    public bool CPU表示ありか {
        get => this.CPU表示ありか値;
        private set => this.SetProperty(ref this.CPU表示ありか値,value);
    }
    public string 論理プロセッサ文字列 {
        get => this.論理プロセッサ文字列値;
        private set => this.SetProperty(ref this.論理プロセッサ文字列値,value);
    }
    public string コア種別ラベル {
        get => this.コア種別ラベル値;
        private set => this.SetProperty(ref this.コア種別ラベル値,value);
    }
    public string コア種別画像ソース {
        get => this.コア種別画像ソース値;
        private set => this.SetProperty(ref this.コア種別画像ソース値,value);
    }
    public Color バッジ背景色 {
        get => this.バッジ背景色値;
        private set => this.SetProperty(ref this.バッジ背景色値,value);
    }
    public Color バッジ文字色 {
        get => this.バッジ文字色値;
        private set => this.SetProperty(ref this.バッジ文字色値,value);
    }
    public string CPUツールチップ文字列 => this.CPU表示ありか
        ? (string.IsNullOrWhiteSpace(this.コア種別ラベル) ? "CPU" : this.コア種別ラベル)+" / CPU "+this.論理プロセッサ文字列
        : string.Empty;
    public int? 行ループ回数 =>
        this.右セル.最終ループ回数??this.左セル.最終ループ回数;
    public string ループ回数文字列 =>
        this.行ループ回数 is { } 値 ? 値.ToString(CultureInfo.InvariantCulture) : string.Empty;
    public string ループ回数ツールチップ文字列 =>
        string.IsNullOrWhiteSpace(this.ループ回数文字列) ? string.Empty : "最終ループ回数: "+this.ループ回数文字列;
    public double? 論理CPUソート値 {
        get => this.論理CPUソート値値;
        private set => this.SetProperty(ref this.論理CPUソート値値,value);
    }
    public ベンチマークソースタブ定義 ソースタブ { get; }
    public IReadOnlyDictionary<string,ベンチマークケース> プロジェクト別ケース { get; }
    public ベンチマークプロジェクトセル 左セル { get; }
    public ベンチマークプロジェクトセル 右セル { get; }
    internal ベンチマークグリッドスナップショット項目? 読込済みスナップショット { get; set; }
    internal int グリッド表示インデックス { get; set; } = -1;
    public string Count引数 => this.引数値を取得(スケーラブルベンチマーク引数列.Count);
    public string Index引数 => this.引数値を取得(スケーラブルベンチマーク引数列.Index);
    public string Page引数 => this.引数値を取得(スケーラブルベンチマーク引数列.Page);
    public string Segment引数 => this.引数値を取得(スケーラブルベンチマーク引数列.Segment);
    public string Memory引数 => this.引数値を取得(スケーラブルベンチマーク引数列.Memory);
    public string 割合引数 => this.引数値を取得(スケーラブルベンチマーク引数列.Fraction);
    public string Initial引数 => this.引数値を取得(スケーラブルベンチマーク引数列.Initial);
    public string Data引数 => this.引数値を取得(スケーラブルベンチマーク引数列.Data);

    public bool 実行対象ありか { get; }

    public bool 選択中か {
        get => this.選択中か値;
        set {
            this.SetProperty(ref this.選択中か値,value&&this.実行対象ありか);
            this.選択チェック状態=this.選択中か値;
            foreach(var セル in this.プロジェクトセル一覧) {
                セル.行選択変更を反映();
            }
        }
    }

    public bool UI選択中か {
        get => this.UI選択中か値;
        private set => this.SetProperty(ref this.UI選択中か値,value);
    }

    public bool 左UI選択中か => this.左セル.UI選択中か;
    public bool 右UI選択中か => this.右セル.UI選択中か;
    public bool 左部分UI選択中か => this.左セル.UI選択中か&&!this.UI選択中か;
    public bool 右部分UI選択中か => this.右セル.UI選択中か&&!this.UI選択中か;

    public bool? 選択チェック状態 {
        get => this.選択チェック状態値;
        private set => this.SetProperty(ref this.選択チェック状態値,value);
    }

    public bool 最終完了か {
        get => this.最終完了か値;
        private set {
            if(this.SetProperty(ref this.最終完了か値,value)) {
                foreach(var セル in this.プロジェクトセル一覧) {
                    セル.行最終完了状態を設定(value);
                }
            }
        }
    }

    public string 時間シェア表示 {
        get => this.時間シェア文字列;
        private set => this.SetProperty(ref this.時間シェア文字列,value);
    }

    public string メモリシェア表示 {
        get => this.メモリシェア文字列;
        private set => this.SetProperty(ref this.メモリシェア文字列,value);
    }

    public ベンチマークシェアゲージ状態 時間シェアゲージ =>
        ベンチマークシェアゲージ状態.シェアから作成(this.表示時間シェアを取得());

    public ベンチマークシェアゲージ状態 メモリシェアゲージ =>
        ベンチマークシェアゲージ状態.シェアから作成(this.表示メモリシェアを取得());

    public string 停止要素数文字列 => string.IsNullOrWhiteSpace(this.停止要素数文字列値)
        ? this.要素数文字列
        : this.停止要素数文字列値;

    private string 要素数文字列 {
        get {
            var 左 = this.左セル.要素数文字列;
            var 右 = this.右セル.要素数文字列;
            if(string.Equals(左,右,StringComparison.Ordinal)) {
                return 左;
            }

            if(string.IsNullOrWhiteSpace(左)) {
                return 右;
            }

            if(string.IsNullOrWhiteSpace(右)) {
                return 左;
            }

            return 左+"/"+右;
        }
    }

    public Color 停止要素数文字色 {
        get => this.停止要素数文字色値;
        private set => this.SetProperty(ref this.停止要素数文字色値,value);
    }

    public FontAttributes 停止要素数フォント属性 {
        get => this.停止要素数フォント属性値;
        private set => this.SetProperty(ref this.停止要素数フォント属性値,value);
    }

    public string 左時間文字列 {
        get => this.左時間文字列値;
        private set => this.SetProperty(ref this.左時間文字列値,value);
    }

    public Color 左時間文字色 => 比較指標文字色を取得(this.左セル.測定値.時間ナノ秒,this.右セル.測定値.時間ナノ秒);
    public string 左割り当て文字列 {
        get => this.左割り当て文字列値;
        private set => this.SetProperty(ref this.左割り当て文字列値,value);
    }
    public Color 左メモリ文字色 => 比較指標文字色を取得(this.左セル.測定値.割り当てバイト数,this.右セル.測定値.割り当てバイト数);
    public string 右時間文字列 {
        get => this.右時間文字列値;
        private set => this.SetProperty(ref this.右時間文字列値,value);
    }
    public Color 右時間文字色 => 比較指標文字色を取得(this.右セル.測定値.時間ナノ秒,this.左セル.測定値.時間ナノ秒);
    public string 右割り当て文字列 {
        get => this.右割り当て文字列値;
        private set => this.SetProperty(ref this.右割り当て文字列値,value);
    }
    public Color 右メモリ文字色 => 比較指標文字色を取得(this.右セル.測定値.割り当てバイト数,this.左セル.測定値.割り当てバイト数);

    public IReadOnlyList<ベンチマークケース> 実行ケースを取得() {
        // 保存Runスナップショットの型列は表示値であり、worker実行用の完全型名ではない。
        // 現行カタログへ一意接続できなかったセルは 型完全名 を空にして保持し、計測キューには入れない。
        if(!this.選択中か) {
            return [];
        }

        return this.プロジェクトセル一覧
            .Where(static セル => セル.ケースありか)
            .Select(セル => this.プロジェクト別ケース[セル.プロジェクト])
            .Where(static ベンチマーク => !string.IsNullOrWhiteSpace(ベンチマーク.型完全名))
            .ToArray();
    }

    public IEnumerable<ベンチマークプロジェクトセル> プロジェクトセル一覧 {
        get {
            yield return this.左セル;
            yield return this.右セル;
        }
    }

    public ベンチマークプロジェクトセル? プロジェクトセルを取得(string プロジェクト) =>
        this.プロジェクトセル一覧.FirstOrDefault(セル => string.Equals(セル.プロジェクト,プロジェクト,StringComparison.OrdinalIgnoreCase));

    public void UI選択状態を更新() {
        this.UI選択中か=ベンチマーク行UI選択ロジック.行全体を選択中か(
            this.プロジェクトセル一覧.Select(static セル => new ベンチマーク行UI選択セル(セル.ケースありか,セル.UI選択中か)));
        this.OnPropertyChanged(nameof(this.左UI選択中か));
        this.OnPropertyChanged(nameof(this.右UI選択中か));
        this.OnPropertyChanged(nameof(this.左部分UI選択中か));
        this.OnPropertyChanged(nameof(this.右部分UI選択中か));
    }

    public void 最終完了状態を設定(bool 最終完了か) {
        this.最終完了か=最終完了か;
    }

    private void セルUI選択変更時(object? 送信元,EventArgs 引数) {
        this.UI選択状態を更新();
    }

    private void セルプロパティ変更時(object? 送信元,PropertyChangedEventArgs 引数) {
        if(引数.PropertyName==nameof(ベンチマークプロジェクトセル.要素数文字列)) {
            this.OnPropertyChanged(nameof(this.停止要素数文字列));
        }
        if(引数.PropertyName==nameof(ベンチマークプロジェクトセル.最終ループ回数)) {
            this.OnPropertyChanged(nameof(this.ループ回数文字列));
            this.OnPropertyChanged(nameof(this.ループ回数ツールチップ文字列));
        }
    }

    public void 保存結果を適用(ベンチマーク保存結果 結果) {
        if(this.プロジェクトセルを取得(結果.プロジェクト) is { } セル) {
            セル.保存結果を適用(結果);
            this.プロジェクト指標文字列を設定(結果.プロジェクト,時間を整形(セル.測定値),割り当てを整形(セル.測定値));
            this.表示シェアを更新();
        }
    }

    public void 実行結果を適用(ベンチマーク結果項目 結果,CPU選択項目? CPU,DateTimeOffset 完了日時,bool 完了を強調するか = true) {
        this.CPU表示を適用(CPU,欠測時に維持するか: true);
        if(this.プロジェクトセルを取得(結果.プロジェクト) is { } セル) {
            セル.実行結果を適用(結果,CPU,完了日時,完了を強調するか);
            this.プロジェクト指標文字列を設定(結果.プロジェクト,時間を整形(セル.測定値),割り当てを整形(セル.測定値));
            this.表示シェアを更新();
        }
    }

    public void スナップショット結果を適用(ベンチマーク結果項目 結果,int? CPU番号,DateTimeOffset? 測定日時) {
        this.スナップショット結果を適用(結果,null,CPU番号,測定日時);
    }

    public void スナップショット結果を適用(ベンチマーク結果項目 結果,CPU選択項目? CPU,int? CPU番号,DateTimeOffset? 測定日時) {
        if(CPU is not null) {
            this.CPU表示を適用(CPU,欠測時に維持するか: true);
        } else {
            this.CPU番号表示を適用(CPU番号);
        }
        if(this.プロジェクトセルを取得(結果.プロジェクト) is { } セル) {
            セル.スナップショット結果を適用(結果,CPU,CPU番号,測定日時);
            this.プロジェクト指標文字列を設定(結果.プロジェクト,時間を整形(セル.測定値),割り当てを整形(セル.測定値));
            this.表示シェアを更新();
        }
    }

    public void スナップショット結果を適用(string プロジェクト,ベンチマーク結果項目 結果,int? CPU番号,DateTimeOffset? 測定日時) {
        this.スナップショット結果を適用(プロジェクト,結果,null,CPU番号,測定日時);
    }

    public void スナップショット結果を適用(string プロジェクト,ベンチマーク結果項目 結果,CPU選択項目? CPU,int? CPU番号,DateTimeOffset? 測定日時) {
        if(CPU is not null) {
            this.CPU表示を適用(CPU,欠測時に維持するか: true);
        } else {
            this.CPU番号表示を適用(CPU番号);
        }
        if(this.プロジェクトセルを取得(プロジェクト) is { } セル) {
            セル.スナップショット結果を適用(結果,CPU,CPU番号,測定日時);
            this.プロジェクト指標文字列を設定(プロジェクト,時間を整形(セル.測定値),割り当てを整形(セル.測定値));
            this.表示シェアを更新();
        }
    }

    public void 結果確認を適用(string プロジェクト,string 確認文字列,string シード文字列) {
        this.プロジェクトセルを取得(プロジェクト)?.結果確認を適用(確認文字列,シード文字列);
    }

    public void スキップを適用(string プロジェクト,int 停止要素数,プロジェクト測定値 停止時測定値) {
        this.スキップを適用(プロジェクト,停止要素数,停止時測定値,null,null,false);
    }

    public void スキップを適用(string プロジェクト,int 停止要素数,プロジェクト測定値 停止時測定値,CPU選択項目? CPU,DateTimeOffset? 測定日時,bool メタデータ欠測か) {
        this.CPU表示を適用(CPU,欠測時に維持するか: true);
        if(this.プロジェクトセルを取得(プロジェクト) is { } セル) {
            セル.スキップを適用(停止要素数,停止時測定値,CPU,測定日時,メタデータ欠測か);
        }

        this.プロジェクト指標文字列を設定(プロジェクト,時間を整形(停止時測定値),割り当てを整形(停止時測定値));
        this.表示シェアを更新();
    }

    public void ペア停止を適用(string 左プロジェクト,string 右プロジェクト,int 停止要素数,bool 強調するか) {
        var 文字列 = 停止要素数.ToString(CultureInfo.InvariantCulture);
        if(隣接ペアか(左プロジェクト,右プロジェクト,this.ソースタブ.プロジェクト順)) {
            this.停止要素数文字列値=文字列;
            this.停止要素数スタイルを適用(強調するか);
            this.OnPropertyChanged(nameof(this.停止要素数文字列));
            this.OnPropertyChanged(nameof(this.停止要素数文字色));
            this.OnPropertyChanged(nameof(this.停止要素数フォント属性));
        }
    }

    public void 実行時状態をクリア() {
        foreach(var セル in this.プロジェクトセル一覧) {
            セル.実行時状態をクリア();
        }
    }

    public void 今回実行待機を設定(bool 値) {
        foreach(var セル in this.プロジェクトセル一覧) {
            if(セル.ケースありか) {
                セル.今回実行待機を設定(値);
            }
        }
    }

    public void 実行要素の完了強調を維持(int 実行要素数) {
        foreach(var セル in this.プロジェクトセル一覧) {
            セル.実行要素の完了強調を維持(実行要素数);
        }
    }

    public void 実行強調をクリア() {
        foreach(var セル in this.プロジェクトセル一覧) {
            セル.実行強調をクリア();
        }
    }

    public void 結果表示をクリア() {
        this.比較をクリア(false);
        this.最終完了状態を設定(false);
        this.CPU表示をクリア();
        // 行オブジェクトが「新規」で再利用された場合に、DBロード時の古いスナップショット参照が残らないようにする。
        this.読込済みスナップショット=null;
    }

    public void 未選択結果表示をクリア() {
        if(!this.選択中か) {
            this.結果表示をクリア();
        }
        foreach(var セル in this.プロジェクトセル一覧) {
            if(!this.選択中か||!セル.ケースありか) {
                セル.測定値をクリア();
            }
        }
        this.左時間文字列=時間を整形(this.左セル.測定値);
        this.左割り当て文字列=割り当てを整形(this.左セル.測定値);
        this.右時間文字列=時間を整形(this.右セル.測定値);
        this.右割り当て文字列=割り当てを整形(this.右セル.測定値);
        this.表示シェアを更新();
    }

    public void 比較を適用(ベンチマーク比較行? 行,bool 維持表示を保持するか = false) {
        if(行 is null) {
            this.比較をクリア(維持表示を保持するか);
            return;
        }

        var 値 = 行.Value;
        // 比較行の値があるときだけ比較ラベルを更新し、セルの保持値は別に残す。
        this.停止要素数文字列をクリア();
        var 左 = 値.プロジェクトを取得(this.ソースタブ.左プロジェクト);
        var 右 = 値.プロジェクトを取得(this.ソースタブ.右プロジェクト);
        this.左セル.測定値を適用(左,維持表示を保持するか);
        this.右セル.測定値を適用(右,維持表示を保持するか);
        this.左時間文字列=時間を整形(this.左セル.測定値);
        this.左割り当て文字列=割り当てを整形(this.左セル.測定値);
        this.右時間文字列=時間を整形(this.右セル.測定値);
        this.右割り当て文字列=割り当てを整形(this.右セル.測定値);
        this.表示シェアを更新();
    }

    public ベンチマークソート値 ソート値を取得(ベンチマークグリッドソート列 列) {
        return 列 switch {
            ベンチマークグリッドソート列.ベンチマーク型 => ベンチマークソート値.文字列から作成(this.ベンチマーク型文字列),
            ベンチマークグリッドソート列.ベンチマークメソッド => ベンチマークソート値.文字列から作成(this.ベンチマークメソッド文字列),
            ベンチマークグリッドソート列.ベンチマーク名 => ベンチマークソート値.文字列から作成(this.ベンチマーク名),
            ベンチマークグリッドソート列.論理CPU => ベンチマークソート値.数値から作成(this.論理CPUソート値),
            ベンチマークグリッドソート列.ループ回数 => ベンチマークソート値.数値から作成(this.行ループ回数),
            ベンチマークグリッドソート列.引数Count => ベンチマークソート値.数値から作成(引数数値を解析(this.Count引数)),
            ベンチマークグリッドソート列.引数インデックス => ベンチマークソート値.数値から作成(引数数値を解析(this.Index引数)),
            ベンチマークグリッドソート列.引数ページ => ベンチマークソート値.数値から作成(引数数値を解析(this.Page引数)),
            ベンチマークグリッドソート列.引数セグメント => ベンチマークソート値.数値から作成(引数数値を解析(this.Segment引数)),
            ベンチマークグリッドソート列.引数メモリ => ベンチマークソート値.数値から作成(引数数値を解析(this.Memory引数)),
            ベンチマークグリッドソート列.引数割合 => ベンチマークソート値.数値から作成(引数数値を解析(this.割合引数)),
            ベンチマークグリッドソート列.引数初期値 => ベンチマークソート値.数値から作成(引数数値を解析(this.Initial引数)),
            ベンチマークグリッドソート列.引数データ => ベンチマークソート値.数値から作成(引数数値を解析(this.Data引数)),
            ベンチマークグリッドソート列.左日時 => ベンチマークソート値.日時から作成(this.左セル.最新表示日時),
            ベンチマークグリッドソート列.左時間 => ベンチマークソート値.指標から作成(this.左セル.測定値,ベンチマーク指標種別.時間),
            ベンチマークグリッドソート列.左割り当て => ベンチマークソート値.指標から作成(this.左セル.測定値,ベンチマーク指標種別.割り当て),
            ベンチマークグリッドソート列.時間シェア => ベンチマークソート値.シェアから作成(this.表示時間シェアを取得()),
            ベンチマークグリッドソート列.メモリシェア => ベンチマークソート値.シェアから作成(this.表示メモリシェアを取得()),
            ベンチマークグリッドソート列.右日時 => ベンチマークソート値.日時から作成(this.右セル.最新表示日時),
            ベンチマークグリッドソート列.右時間 => ベンチマークソート値.指標から作成(this.右セル.測定値,ベンチマーク指標種別.時間),
            ベンチマークグリッドソート列.右割り当て => ベンチマークソート値.指標から作成(this.右セル.測定値,ベンチマーク指標種別.割り当て),
            _ => ベンチマークソート値.欠測,
        };
    }

    private void 比較をクリア(bool 維持表示を保持するか) {
        this.時間シェア表示=string.Empty;
        this.メモリシェア表示=string.Empty;
        this.停止要素数文字列値=string.Empty;
        if(維持表示を保持するか) {
            // 実行中/完了後は、比較行が消えてもセルの確定値を維持する。
            this.左時間文字列=時間を整形(this.左セル.測定値);
            this.左割り当て文字列=割り当てを整形(this.左セル.測定値);
            this.右時間文字列=時間を整形(this.右セル.測定値);
            this.右割り当て文字列=割り当てを整形(this.右セル.測定値);
        } else {
            this.左時間文字列=string.Empty;
            this.左割り当て文字列=string.Empty;
            this.右時間文字列=string.Empty;
            this.右割り当て文字列=string.Empty;
        }
        this.OnPropertyChanged(nameof(this.停止要素数文字列));
        foreach(var セル in this.プロジェクトセル一覧) {
            if(!維持表示を保持するか) {
                セル.測定値をクリア();
            }
        }
        // セルの測定値を確定させた後に呼ぶことで、左右のシェア表示・ゲージも
        // (欠測クリア時は空に、維持時は現在値に)最新の状態へ揃える。
        this.表示シェアを更新();
    }

    private void 表示シェアを更新() {
        var 時間シェア=this.表示時間シェアを取得();
        var メモリシェア=this.表示メモリシェアを取得();
        this.時間シェア表示=時間シェア.表示文字列;
        this.メモリシェア表示=メモリシェア.表示文字列;
        this.左時間文字列=時間を整形(this.左セル.測定値);
        this.右時間文字列=時間を整形(this.右セル.測定値);
        this.左割り当て文字列=割り当てを整形(this.左セル.測定値);
        this.右割り当て文字列=割り当てを整形(this.右セル.測定値);
        this.比較指標文字色を更新();
        this.OnPropertyChanged(nameof(this.時間シェアゲージ));
        this.OnPropertyChanged(nameof(this.メモリシェアゲージ));
    }

    private シェア値 表示時間シェアを取得() => this.表示時間シェアを取得("左");

    private シェア値 表示時間シェアを取得(string 最適化列キー) {
        var ペア=this.ソースタブ.比較ペア一覧.FirstOrDefault(候補=>string.Equals(候補.最適化列キー,最適化列キー,StringComparison.Ordinal));
        if(string.IsNullOrWhiteSpace(ペア.左プロジェクト)){
            return シェア値.欠測;
        }
        return ベンチマーク比較フォーマッター.時間シェアを計算(
            this.プロジェクトセルを取得(ペア.左プロジェクト)?.測定値??プロジェクト測定値.欠測,
            this.プロジェクトセルを取得(ペア.右プロジェクト)?.測定値??プロジェクト測定値.欠測);
    }

    private シェア値 表示メモリシェアを取得() => this.表示メモリシェアを取得("左");

    private シェア値 表示メモリシェアを取得(string 最適化列キー) {
        var ペア=this.ソースタブ.比較ペア一覧.FirstOrDefault(候補=>string.Equals(候補.最適化列キー,最適化列キー,StringComparison.Ordinal));
        if(string.IsNullOrWhiteSpace(ペア.左プロジェクト)){
            return シェア値.欠測;
        }
        return ベンチマーク比較フォーマッター.メモリシェアを計算(
            this.プロジェクトセルを取得(ペア.左プロジェクト)?.測定値??プロジェクト測定値.欠測,
            this.プロジェクトセルを取得(ペア.右プロジェクト)?.測定値??プロジェクト測定値.欠測);
    }

    private void プロジェクト指標文字列を設定(string プロジェクト,string 時間文字列値,string 割り当て文字列値) {
        if(string.Equals(プロジェクト,this.ソースタブ.左プロジェクト,StringComparison.OrdinalIgnoreCase)) {
            this.左時間文字列=時間文字列値;
            this.左割り当て文字列=割り当て文字列値;
        } else if(string.Equals(プロジェクト,this.ソースタブ.右プロジェクト,StringComparison.OrdinalIgnoreCase)) {
            this.右時間文字列=時間文字列値;
            this.右割り当て文字列=割り当て文字列値;
        }
    }

    private void 比較指標文字色を更新() {
        this.OnPropertyChanged(nameof(this.左時間文字色));
        this.OnPropertyChanged(nameof(this.左メモリ文字色));
        this.OnPropertyChanged(nameof(this.右時間文字色));
        this.OnPropertyChanged(nameof(this.右メモリ文字色));
    }    private static Color 比較指標文字色を取得<T>(T? 対象,T? 相手) where T:struct,IComparable<T> {
        if(対象 is not { } 対象値||相手 is not { } 相手値){
            return 既定指標文字色;
        }
        if(対象値.CompareTo(相手値)==0){
            return 既定指標文字色;
        }
        return 対象値.CompareTo(相手値)<0 ? 高性能指標文字色 : 低性能指標文字色;
    }
    private void 停止要素数文字列をクリア() {
        this.停止要素数文字列値=string.Empty;
        this.停止要素数スタイルを適用(false);
        this.OnPropertyChanged(nameof(this.停止要素数文字列));
    }

    private void CPU表示を適用(CPU選択項目? CPU,bool 欠測時に維持するか = false) {
        if(CPU is null) {
            if(!欠測時に維持するか) {
                this.CPU表示をクリア();
            }
            return;
        }

        this.CPU表示ありか=true;
        this.論理プロセッサ文字列=CPU.論理プロセッサ文字列;
        this.コア種別ラベル=CPU.コア種別ラベル;
        this.コア種別画像ソース=CPU.コア種別画像ソース;
        this.バッジ背景色=CPU.バッジ背景色;
        this.バッジ文字色=CPU.バッジ文字色;
        this.論理CPUソート値=CPU.論理プロセッサインデックス;
        this.OnPropertyChanged(nameof(this.CPUツールチップ文字列));
    }

    private void CPU番号表示を適用(int? CPU番号) {
        if(CPU番号 is not { } 値) {
            this.CPU表示をクリア();
            return;
        }

        this.CPU表示ありか=true;
        this.論理プロセッサ文字列=値.ToString(CultureInfo.InvariantCulture);
        this.コア種別ラベル=string.Empty;
        this.コア種別画像ソース=string.Empty;
        this.バッジ背景色=Color.FromArgb("#E2E8F0");
        this.バッジ文字色=Color.FromArgb("#334155");
        this.論理CPUソート値=値;
        this.OnPropertyChanged(nameof(this.CPUツールチップ文字列));
    }

    private void CPU表示をクリア() {
        this.CPU表示ありか=false;
        this.論理プロセッサ文字列=string.Empty;
        this.コア種別ラベル=string.Empty;
        this.コア種別画像ソース=string.Empty;
        this.バッジ背景色=Color.FromArgb("#E2E8F0");
        this.バッジ文字色=Color.FromArgb("#334155");
        this.論理CPUソート値=null;
        this.OnPropertyChanged(nameof(this.CPUツールチップ文字列));
    }

    private void 停止要素数スタイルを適用(bool 強調するか) {
        this.停止要素数文字色=既定停止要素数文字色;
        this.停止要素数フォント属性=FontAttributes.None;
    }

    private static bool 隣接ペアか(string 左プロジェクト,string 右プロジェクト,IReadOnlyList<string>プロジェクト順) {
        for(var インデックス = 0;インデックス<プロジェクト順.Count-1;インデックス++) {
            if(string.Equals(プロジェクト順[インデックス],左プロジェクト,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(プロジェクト順[インデックス+1],右プロジェクト,StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        return false;
    }

    private ベンチマークプロジェクトセル セルを作成(string プロジェクト) =>
        new(this,プロジェクト,this.プロジェクト別ケース.ContainsKey(プロジェクト));

    public void ベンチマーク名を更新(int 要素数) {
        var ベンチマークパス = this.表示用ベンチマークパスを取得();
        if(!string.IsNullOrWhiteSpace(ベンチマークパス)) {
            this.ベンチマーク名=ベンチマークパス+"."+this.メソッド名;
            return;
        }
        var 型完全名 = this.先頭型完全名を取得();
        this.ベンチマーク名=スケーラブルベンチマーク名フォーマッター.ベンチマーク名を作成(型完全名,this.メソッド名);
    }

    // 保存済み実行のケースはベンチマークパス(カタログ由来)を持たないため、型列と同じ保存済み表示文字列で補う。
    private string 表示用ベンチマークパスを取得() {
        var ベンチマークパス = this.先頭ベンチマークパスを取得();
        if(!string.IsNullOrWhiteSpace(ベンチマークパス)) {
            return ベンチマークパス;
        }

        if(スケーラブルベンチマーク名フォーマッター.スケーラブルソースタブか(this.ソースタブ.タブ)) {
            return string.Empty;
        }

        return this.スナップショットベンチマーク型文字列;
    }

    private void ベンチマーク表示部分を更新() {
        var 型完全名 = this.先頭型完全名を取得();
        var 型文字列 = スケーラブルベンチマーク名フォーマッター.ベンチマーク型表示を作成(型完全名);
        if(!string.IsNullOrWhiteSpace(this.スナップショットベンチマーク型文字列)) {
            型文字列=this.スナップショットベンチマーク型文字列;
        }
        if(string.IsNullOrWhiteSpace(型文字列)) {
            型文字列=型文字列を抽出(this.ベンチマーク名値);
        }
        this.ベンチマーク型文字列=型文字列;
        var ベンチマークパス = this.表示用ベンチマークパスを取得();
        if(string.IsNullOrWhiteSpace(ベンチマークパス)) {
            this.ベンチマークメソッド文字列=this.メソッド名;
        }else{
            this.ベンチマークメソッド文字列=ベンチマークパス+"."+this.メソッド名;
        }
    }

    private string 先頭ベンチマークパスを取得() =>
        this.プロジェクト別ケース.Values
            .Select(static ベンチマーク => ベンチマーク.ベンチマークパス)
            .FirstOrDefault(static パス => !string.IsNullOrWhiteSpace(パス))??string.Empty;

    // ライブカタログに紐づく行(計測直後・新規グリッド)でのみ非空になる完全なベンチマークパス
    // (例: "Tsavorite<ClassInt32>")。保存時にこの値を型名として永続化することで、
    // ロード後の表示もライブ計測時と同じ形式にできる(保存済みDBには一切紐づかない)。
    internal string 表示用ベンチマークパス => this.先頭ベンチマークパスを取得();

    private string 先頭型完全名を取得() {
        foreach(var プロジェクト in this.ソースタブ.プロジェクト順) {
            if(this.プロジェクト別ケース.TryGetValue(プロジェクト,out var ベンチマーク)&&!string.IsNullOrWhiteSpace(ベンチマーク.型完全名)) {
                return ベンチマーク.型完全名;
            }
        }

        return string.Empty;
    }

    private static string 型文字列を抽出(string ベンチマーク名値) {
        if(string.IsNullOrWhiteSpace(ベンチマーク名値)||ベンチマーク名値[0]!='<') {
            return string.Empty;
        }

        var 終端 = ベンチマーク名値.IndexOf('>');
        return 終端>1
            ? ベンチマーク名値[1..終端]
            : string.Empty;
    }

    private string 引数値を取得(スケーラブルベンチマーク引数列 列) =>
        this.プロジェクト別ケース.Values
            .Select(項目 => スケーラブルベンチマーク名フォーマッター.表示引数値を取得(this.ソースタブ,項目.引数一覧,列))
            .FirstOrDefault(static 値 => !string.IsNullOrWhiteSpace(値))??string.Empty;

    public string ベンチマーク引数ツールチップ文字列 => ベンチマーク引数ツールチップフォーマッター.引数ツールチップを作成(this);

    private static double? 引数数値を解析(string 文字列) =>
        double.TryParse(文字列,NumberStyles.Float,CultureInfo.InvariantCulture,out var 値)
            ? 値
            : null;

    private static string 時間を整形(プロジェクト測定値 測定値値) {
        return 測定値値.測定値ありか&&測定値値.時間ナノ秒 is { } 値
            ? ベンチマークフォーマッター.ナノ秒を整形(値)
            : string.Empty;
    }

    private static string 割り当てを整形(プロジェクト測定値 測定値値) {
        return 測定値値.測定値ありか&&測定値値.割り当てバイト数 is { } 値
            ? ベンチマークフォーマッター.バイト数を整形(値)
            : string.Empty;
    }

}

internal static class ベンチマーク引数ツールチップフォーマッター {
    public static string 引数ツールチップを作成(ベンチマーク表行 行) {
        var 項目一覧 = ベンチマーク引数列一覧.取得(行.ソースタブ)
            .Select(列 => (列.ToString(),引数値を取得(行,列)))
            .ToArray();
        var 概要 = 項目一覧
            .Where(static 項目 => 項目.Item1 is "Count" or "Memory" or "Initial")
            .ToArray();
        var 概要文字列 = string.Join(
            " / ",
            概要
                .Where(static 項目 => !string.IsNullOrWhiteSpace(項目.Item2))
                .Select(static 項目 => 項目.Item1+"="+項目.Item2));
        var 詳細文字列 = string.Join(
            " / ",
            項目一覧
                .Where(static 項目 => !string.IsNullOrWhiteSpace(項目.Item2))
                .Select(static 項目 => 項目.Item1+"="+項目.Item2));
        if(string.IsNullOrWhiteSpace(概要文字列)) {
            return 詳細文字列;
        }

        return string.IsNullOrWhiteSpace(詳細文字列)
            ? 概要文字列
            : 概要文字列+"\n"+詳細文字列;
    }

    private static string 引数値を取得(ベンチマーク表行 行,スケーラブルベンチマーク引数列 列) =>
        列 switch {
            スケーラブルベンチマーク引数列.Count => 行.Count引数,
            スケーラブルベンチマーク引数列.Index => 行.Index引数,
            スケーラブルベンチマーク引数列.Page => 行.Page引数,
            スケーラブルベンチマーク引数列.Segment => 行.Segment引数,
            スケーラブルベンチマーク引数列.Memory => 行.Memory引数,
            スケーラブルベンチマーク引数列.Fraction => 行.割合引数,
            スケーラブルベンチマーク引数列.Initial => 行.Initial引数,
            スケーラブルベンチマーク引数列.Data => 行.Data引数,
            _ => string.Empty,
        };
}

public sealed class CPU選択項目(int 論理プロセッサインデックス,CPUコア種別 コア種別,int 物理コアインデックス,int 種別内コアインデックス,bool 選択中か値)
    :ObservableObject {
    private bool    選択中か値    =選択中か値;
    private string  実行状態文字列値 = "待機";
    private string  状態画像ソース値 = ベンチマーク状態画像名.CPU待機中;
    private string  現在ジョブ文字列値= string.Empty;
    private bool    ジョブ実行中か値;
    public  int     論理プロセッサインデックス { get; }=論理プロセッサインデックス;
    public  CPUコア種別 コア種別          { get; }=コア種別;
    public  int     物理コアインデックス    { get; }=物理コアインデックス;
    public  int     種別内コアインデックス   { get; }=種別内コアインデックス;
    public string コアラベル => this.コア種別 switch {
        CPUコア種別.高性能 => "P"+this.種別内コアインデックス,
        CPUコア種別.高効率 => "E"+this.種別内コアインデックス,
        _ => "コア "+this.物理コアインデックス,
    };
    public string コア種別ラベル => this.コア種別 switch {
        CPUコア種別.高性能 => "P",
        CPUコア種別.高効率 => "E",
        _ => "U",
    };
    public string コア種別画像ソース => ベンチマークCPUコア画像名.コア種別から取得(this.コア種別);
    public string 表示名 => "CPU "+this.論理プロセッサインデックス+" ("+this.コアラベル+")";
    public string 実行状態文 {
        get => this.実行状態文字列値;
        private set => this.SetProperty(ref this.実行状態文字列値,value);
    }
    public string 状態画像ソース {
        get => this.状態画像ソース値;
        private set => this.SetProperty(ref this.状態画像ソース値,value);
    }
    public string 現在ジョブ文 {
        get => this.現在ジョブ文字列値;
        private set => this.SetProperty(ref this.現在ジョブ文字列値,value);
    }
    public bool ジョブ実行中か {
        get => this.ジョブ実行中か値;
        private set => this.SetProperty(ref this.ジョブ実行中か値,value);
    }
    public string 状態表示文 =>
        this.論理プロセッサインデックス.ToString(CultureInfo.InvariantCulture)
        +" "+this.コア種別ラベル
        +" "+this.実行状態文
        +(string.IsNullOrWhiteSpace(this.現在ジョブ文) ? string.Empty : " "+this.現在ジョブ文);
    public string 論理プロセッサ文字列 => this.論理プロセッサインデックス.ToString(CultureInfo.InvariantCulture);
    public string セル状態文 => ベンチマークCPU状態画像ロジック.セル状態文を取得(this.実行状態文,this.現在ジョブ文);
    public Color バッジ背景色 => this.コア種別 switch {
        CPUコア種別.高性能 => Color.FromArgb("#DBEAFE"),
        CPUコア種別.高効率 => Color.FromArgb("#DCFCE7"),
        _ => Color.FromArgb("#E2E8F0"),
    };
    public Color バッジ文字色 => this.コア種別 switch {
        CPUコア種別.高性能 => Color.FromArgb("#1D4ED8"),
        CPUコア種別.高効率 => Color.FromArgb("#15803D"),
        _ => Color.FromArgb("#334155"),
    };

    public bool 選択中か {
        get => this.選択中か値;
        set => this.SetProperty(ref this.選択中か値,value);
    }

    public void ジョブを適用(ベンチマーク結果項目 項目,string? ベンチマーク表示文字列) {
        this.ジョブ実行中か=ジョブ状態名.保存値と一致するか(項目.状態,ジョブ状態.実行中);
        this.状態画像ソース=ベンチマークCPU状態画像ロジック.画像ソースを取得(項目.状態);
        this.実行状態文=ベンチマークCPU状態画像ロジック.状態文を取得(項目.状態);
        this.現在ジョブ文=string.IsNullOrWhiteSpace(ベンチマーク表示文字列) ? 項目.メソッド : ベンチマーク表示文字列;
        this.OnPropertyChanged(nameof(this.状態表示文));
        this.OnPropertyChanged(nameof(this.セル状態文));
    }

    public void ジョブをクリア() {
        this.ジョブ実行中か=false;
        this.状態画像ソース=ベンチマーク状態画像名.CPU待機中;
        this.実行状態文="待機";
        this.現在ジョブ文=string.Empty;
        this.OnPropertyChanged(nameof(this.状態表示文));
        this.OnPropertyChanged(nameof(this.セル状態文));
    }
}

public sealed class CPU表セル:ObservableObject {
    private bool 選択中か値;

    public CPU表セル(CPU選択項目 CPU) {
        this.CPU=CPU;
        this.選択中か値=CPU.選択中か;
        CPU.PropertyChanged+=(_,引数) => {
            if(引数.PropertyName==nameof(CPU選択項目.選択中か)) {
                this.選択中か=CPU.選択中か;
            } else if(引数.PropertyName==nameof(CPU選択項目.状態表示文)
                ||引数.PropertyName==nameof(CPU選択項目.現在ジョブ文)
                ||引数.PropertyName==nameof(CPU選択項目.状態画像ソース)
                ||引数.PropertyName==nameof(CPU選択項目.実行状態文)
                ||引数.PropertyName==nameof(CPU選択項目.ジョブ実行中か)) {
                this.OnPropertyChanged(nameof(this.表示文字列));
                this.OnPropertyChanged(nameof(this.論理プロセッサ文字列));
                this.OnPropertyChanged(nameof(this.セル状態文));
                this.OnPropertyChanged(nameof(this.バッジ背景色));
                this.OnPropertyChanged(nameof(this.ジョブ実行中か));
                this.OnPropertyChanged(nameof(this.状態画像ソース));
                this.OnPropertyChanged(nameof(this.状態画像表示中か));
            }
        };
    }

    public CPU選択項目 CPU { get; }
    public string 表示文字列 => this.CPU.状態表示文;
    public string 論理プロセッサ文字列 => this.CPU.論理プロセッサ文字列;
    public string コア種別画像ソース => this.CPU.コア種別画像ソース;
    public string セル状態文 => this.CPU.セル状態文;
    public string 状態画像ソース => this.CPU.状態画像ソース;
    public bool 状態画像表示中か => !this.ジョブ実行中か&&!string.IsNullOrWhiteSpace(this.状態画像ソース);
    public Color バッジ背景色 => this.CPU.バッジ背景色;
    public bool ジョブ実行中か => this.CPU.ジョブ実行中か;

    public bool 選択中か {
        get => this.選択中か値;
        set {
            if(this.SetProperty(ref this.選択中か値,value)) {
                if(this.CPU.選択中か!=value) {
                    this.CPU.選択中か=value;
                }
            }
        }
    }
}

public sealed class CPU表行:ObservableObject {
    private CPU選択状態 状態値;

    public CPU表行(string ラベル,CPUコア種別 コア種別,IReadOnlyList<CPU表セル> セル一覧,int 列数) {
        this.ラベル=ラベル;
        this.コア種別=コア種別;
        this.セル一覧=セル一覧;
        this.グリッドセル一覧=セル一覧
            .Cast<CPU表セル?>()
            .Concat(Enumerable.Repeat<CPU表セル?>(null,Math.Max(0,列数-セル一覧.Count)))
            .ToArray();
        foreach(var セル in セル一覧) {
            セル.PropertyChanged+=(_,引数) => {
                if(引数.PropertyName==nameof(CPU表セル.選択中か)) {
                    this.状態を更新();
                }
            };
        }
        this.状態を更新();
    }

    public string ラベル { get; }
    public CPUコア種別 コア種別 { get; }
    public IReadOnlyList<CPU表セル> セル一覧 { get; }
    public IReadOnlyList<CPU表セル?> グリッドセル一覧 { get; }
    public CPU選択状態 状態 {
        get => this.状態値;
        private set {
            if(this.SetProperty(ref this.状態値,value)) {
                this.OnPropertyChanged(nameof(this.チェック済みか));
            }
        }
    }
    public bool チェック済みか {
        get => this.状態==CPU選択状態.チェック済み;
        set {
            foreach(var セル in this.セル一覧) {
                セル.選択中か=value;
            }
        }
    }
    public string バッジ画像ソース => ベンチマークCPUコア画像名.コア種別から取得(this.コア種別);
    public Color バッジ背景色 => this.コア種別 switch {
        CPUコア種別.高性能 => Color.FromArgb("#DBEAFE"),
        CPUコア種別.高効率 => Color.FromArgb("#DCFCE7"),
        _ => Color.FromArgb("#E2E8F0"),
    };
    public void 状態を更新() {
        this.状態=状態を取得(this.セル一覧);
    }

    private static CPU選択状態 状態を取得(IReadOnlyList<CPU表セル> セル一覧) {
        if(セル一覧.Count==0) {
            return CPU選択状態.未チェック;
        }

        var 選択数 = セル一覧.Count(static セル => セル.選択中か);
        return 選択数==0
            ? CPU選択状態.未チェック
            : 選択数==セル一覧.Count
                ? CPU選択状態.チェック済み
                : CPU選択状態.混在;
    }
}

public sealed class CPU表列(int インデックス,string タイトル):ObservableObject {
    public int     インデックス  { get; }=インデックス;
    public string  タイトル    { get; }=タイトル;
    public CPU表区分? 区分      { get; internal set; }
    public bool    チェック済みか => this.区分?.列状態を取得(this.インデックス)==CPU選択状態.チェック済み;

    public void 更新() {
        this.OnPropertyChanged(nameof(this.チェック済みか));
    }
}

public sealed class CPU表区分:ObservableObject {
    private CPU選択状態 状態値;

    public CPU表区分(string タイトル,CPUコア種別 コア種別,IReadOnlyList<CPU表列> 列一覧,IReadOnlyList<CPU表行> 行一覧) {
        this.タイトル=タイトル;
        this.コア種別=コア種別;
        this.列一覧=列一覧;
        this.行一覧=行一覧;
        foreach(var 列 in 列一覧) {
            列.区分=this;
        }
        this.状態を更新();
        foreach(var セル in 行一覧.SelectMany(static 行 => 行.セル一覧)) {
            セル.PropertyChanged+=(_,引数) => {
                if(引数.PropertyName==nameof(CPU表セル.選択中か)) {
                    this.状態を更新();
                }
            };
        }
    }

    public string タイトル { get; }
    public CPUコア種別 コア種別 { get; }
    public IReadOnlyList<CPU表列> 列一覧 { get; }
    public IReadOnlyList<CPU表行> 行一覧 { get; }
    public IEnumerable<CPU表セル> セル一覧 => this.行一覧.SelectMany(static 行 => 行.セル一覧);

    public CPU選択状態 状態 {
        get => this.状態値;
        private set {
            if(this.SetProperty(ref this.状態値,value)) {
            }
        }
    }

    public string バッジ画像ソース => ベンチマークCPUコア画像名.コア種別から取得(this.コア種別);
    public Color バッジ背景色 => this.コア種別 switch {
        CPUコア種別.高性能 => Color.FromArgb("#DBEAFE"),
        CPUコア種別.高効率 => Color.FromArgb("#DCFCE7"),
        _ => Color.FromArgb("#E2E8F0"),
    };
    public CPU選択状態 列状態を取得(int 列インデックス) {
        var セル一覧 = this.行一覧
            .Where(行 => 列インデックス<行.セル一覧.Count)
            .Select(行 => 行.セル一覧[列インデックス])
            .ToArray();
        return 状態を取得(セル一覧);
    }

    public void 状態を更新() {
        this.状態=状態を取得(this.セル一覧.ToArray());
        foreach(var 行 in this.行一覧) {
            行.状態を更新();
        }
        foreach(var 列 in this.列一覧) {
            列.更新();
        }
    }

    private static CPU選択状態 状態を取得(IReadOnlyList<CPU表セル> セル一覧) {
        if(セル一覧.Count==0) {
            return CPU選択状態.未チェック;
        }

        var 選択数 = セル一覧.Count(static セル => セル.選択中か);
        return 選択数==0
            ? CPU選択状態.未チェック
            : 選択数==セル一覧.Count
                ? CPU選択状態.チェック済み
                : CPU選択状態.混在;
    }
}

public enum CPU選択状態 {
    未チェック,
    混在,
    チェック済み,
}

public sealed record ベンチマーク実行報告(string? 実行ルート);
