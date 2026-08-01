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

public sealed class CPUコア情報(
    int     論理プロセッサインデックス,
    CPUコア種別 コア種別,
    int     物理コアインデックス,
    int     種別内コアインデックス,
    long    アフィニティマスク,
    bool    アフィニティ対応か)
    :ベンチマークバインド可能オブジェクト {
    private bool    選択中値=false;
    public  int     論理プロセッサインデックス {get;}=論理プロセッサインデックス;
    public  CPUコア種別 コア種別          {get;}=コア種別;
    public  int     物理コアインデックス    {get;}=物理コアインデックス;
    public  int     種別内コアインデックス   {get;}=種別内コアインデックス;
    public  long    アフィニティマスク     {get;}=アフィニティマスク;
    public  bool    アフィニティ対応か     {get;}=アフィニティ対応か;
    public  string  グループ表示文字列     =>this.コアラベル;
    public  string  CPU表示文字列      =>$"{this.論理プロセッサインデックス}({this.グループ表示文字列})";

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
