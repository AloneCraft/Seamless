using CommunityToolkit.Mvvm.ComponentModel;

namespace UI.ViewModels;

public sealed partial class 実行ビューモデル:ObservableObject {
    private readonly ベンチマーク実行予測時計 実行予測時計=new();
    private bool 実行予測表示中か;
    private bool 実行予測一時停止中か;
    private string 実行予測概要文字列値=string.Empty;

    public string 実行予測概要文字列=>this.実行予測概要文字列値;

    internal void 実行予測を新規開始() {
        this.実行予測時計.開始();
        this.実行予測表示中か=true;
        this.実行予測一時停止中か=false;
        this.実行予測を更新();
    }

    internal void 実行予測を再開() {
        if(!this.実行予測表示中か) {
            this.実行予測を新規開始();
            return;
        }

        this.実行予測時計.再開();
        this.実行予測一時停止中か=false;
        this.実行予測を更新();
    }

    internal void 実行予測を一時停止() {
        if(!this.実行予測表示中か) {
            return;
        }

        this.実行予測時計.一時停止();
        this.実行予測一時停止中か=true;
    }

    internal void 実行予測を終了() {
        if(!this.実行予測表示中か) {
            return;
        }

        this.実行予測時計.終了();
        this.実行予測一時停止中か=false;
        this.実行予測を更新();
    }

    internal void 実行予測をクリア() {
        this.実行予測時計.クリア();
        this.実行予測表示中か=false;
        this.実行予測一時停止中か=false;
        if(this.実行予測概要文字列値==string.Empty) {
            return;
        }

        this.実行予測概要文字列値=string.Empty;
        this.OnPropertyChanged(nameof(this.実行予測概要文字列));
    }

    internal void 実行予測を更新() {
        if(!this.実行予測表示中か||this.実行予測一時停止中か) {
            return;
        }

        var 表示=ベンチマーク実行予測ロジック.表示文字列(
            this.実行予測時計.経過時間,
            this.全体ソースタブ進行.完了数,
            this.全体ソースタブ進行.合計数,
            DateTimeOffset.Now);
        if(this.実行予測概要文字列値==表示) {
            return;
        }

        this.実行予測概要文字列値=表示;
        this.OnPropertyChanged(nameof(this.実行予測概要文字列));
    }
}
