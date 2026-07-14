using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UI;

public abstract class ベンチマークバインド可能オブジェクト:INotifyPropertyChanged{
    public event PropertyChangedEventHandler?PropertyChanged;

    protected bool プロパティを設定<T値>(ref T値 フィールド,T値 値,[CallerMemberName]string?プロパティ名=null){
        if(EqualityComparer<T値>.Default.Equals(フィールド,値)){
            return false;
        }

        フィールド=値;
        this.プロパティ変更時(プロパティ名);
        return true;
    }

    protected void プロパティ変更時([CallerMemberName]string?プロパティ名=null){
        this.PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(プロパティ名));
    }
}

// ワーカーとUI実行器で共有するモデル。実行器側で再定義しないこと。
public enum CPUコア種別{
    高性能,
    高効率,
    不明,
}

public sealed class CPUコア情報:ベンチマークバインド可能オブジェクト{
    private bool 選択中値;

    public CPUコア情報(
        int 論理プロセッサインデックス,
        CPUコア種別 コア種別,
        int 物理コアインデックス,
        int 種別内コアインデックス,
        long アフィニティマスク,
        bool アフィニティ対応か){
        this.論理プロセッサインデックス=論理プロセッサインデックス;
        this.コア種別=コア種別;
        this.物理コアインデックス=物理コアインデックス;
        this.種別内コアインデックス=種別内コアインデックス;
        this.アフィニティマスク=アフィニティマスク;
        this.アフィニティ対応か=アフィニティ対応か;
        this.選択中値=false;
    }

    public int 論理プロセッサインデックス{get;}
    public CPUコア種別 コア種別{get;}
    public int 物理コアインデックス{get;}
    public int 種別内コアインデックス{get;}
    public long アフィニティマスク{get;}
    public bool アフィニティ対応か{get;}
    public string グループ表示文字列=>this.コアラベル;
    public string CPU表示文字列=>$"{this.論理プロセッサインデックス}({this.グループ表示文字列})";

    public bool 選択中か{
        get=>this.選択中値;
        set{
            if(this.プロパティを設定(ref this.選択中値,value)){
                this.プロパティ変更時(nameof(this.チェック記号));
            }
        }
    }

    public string 表示文字列=>this.CPU表示文字列;
    public string チェック記号{
        get{
            if(this.選択中値){
                return "X";
            }
            return string.Empty;
        }
    }

    public string コアラベル=>this.コア種別 switch{
        CPUコア種別.高性能=>$"Pコア{this.種別内コアインデックス}",
        CPUコア種別.高効率=>$"Eコア{this.種別内コアインデックス}",
        _=>$"不明なコア{this.物理コアインデックス}",
    };

}

public sealed record 報告行(
    string プロジェクト,
    string メソッド,
    string 型完全名,
    string 完全関数名,
    string 状態,
    string 平均,
    string 誤差,
    string 標準偏差,
    string 割り当てバイト数文字列,
    string 最大RSSバイト数文字列,
    string 推定最大RSSバイト数文字列,
    double 平均ナノ秒,
    double 誤差ナノ秒,
    double 標準偏差ナノ秒,
    long 割り当てバイト数値,
    long 最大RSSバイト数値,
    long 推定最大RSSバイト数値,
    string 論理CPU,
    string コア,
    string CPU表示,
    string 開始日時文字列,
    string 終了日時文字列,
    string 成果物フォルダー,
    int?終了コード,
    string 引数キー="",
    IReadOnlyList<ベンチマーク引数>?引数値一覧=null){
    public IReadOnlyList<ベンチマーク引数>引数一覧{get;}=引数値一覧??[];
}
