using Benchmark.Dynamic.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using UI;

namespace Benchmark.Dynamic.UI;

public sealed class 動的ソース結果画面モデル:ObservableObject {
    private readonly ObservableCollection<動的結果行> 共有結果一覧;
    private readonly 動的ベンチマーク画面モデル 画面モデル;

    public 動的ソース結果画面モデル(SourceTarget 対象,ObservableCollection<動的結果行> 共有結果一覧,動的ベンチマーク画面モデル 画面モデル) {
        this.対象=対象;
        this.共有結果一覧=共有結果一覧;
        this.画面モデル=画面モデル;
        this.行一覧=new ObservableCollection<動的結果行>(
            共有結果一覧.Where(行=>行.対象==対象));
        this.共有結果一覧.CollectionChanged+=this.共有結果変更時;
        this.画面モデル.PropertyChanged+=this.画面モデル変更時;
    }

    public SourceTarget 対象{get;}
    public ObservableCollection<動的結果行> 行一覧{get;}

    public double 進行割合 =>
        this.画面モデル.Query数<=0?0:Math.Clamp((double)this.行一覧.Count/this.画面モデル.Query数,0,1);

    public bool 完了か => this.行一覧.Count>0&&this.行一覧.Count>=this.画面モデル.Query数;

    public bool 進行ありか => this.画面モデル.実行中か||this.完了か;

    public bool 完了アイコン表示中か => this.完了か;

    public bool 進行リング表示中か => this.進行ありか&&!this.完了アイコン表示中か;

    public bool 実行スピナー表示中か => this.画面モデル.実行中か&&!this.完了アイコン表示中か;

    public string 完了アイコンソース => ベンチマーク状態画像名.完了;

    public Color 進行外枠色 =>
        this.画面モデル.実行中か||this.完了か?Color.FromArgb("#22C55E"):Color.FromArgb("#64748B");

    private void 共有結果変更時(object? 送信元,NotifyCollectionChangedEventArgs 引数) {
        if(引数.Action==NotifyCollectionChangedAction.Reset) {
            this.行一覧.Clear();
            this.進行状態を通知();
            return;
        }
        if(引数.OldItems is not null) {
            foreach(var 項目 in 引数.OldItems.OfType<動的結果行>().Where(行=>行.対象==this.対象)) {
                this.行一覧.Remove(項目);
            }
        }
        if(引数.NewItems is not null) {
            foreach(var 項目 in 引数.NewItems.OfType<動的結果行>().Where(行=>行.対象==this.対象)) {
                this.行一覧.Add(項目);
            }
        }
        this.進行状態を通知();
    }

    private void 画面モデル変更時(object? 送信元,PropertyChangedEventArgs 引数) {
        if(引数.PropertyName is nameof(動的ベンチマーク画面モデル.実行中か) or nameof(動的ベンチマーク画面モデル.Query数)) {
            this.進行状態を通知();
        }
    }

    private void 進行状態を通知() {
        this.OnPropertyChanged(nameof(this.進行割合));
        this.OnPropertyChanged(nameof(this.完了か));
        this.OnPropertyChanged(nameof(this.進行ありか));
        this.OnPropertyChanged(nameof(this.完了アイコン表示中か));
        this.OnPropertyChanged(nameof(this.進行リング表示中か));
        this.OnPropertyChanged(nameof(this.実行スピナー表示中か));
        this.OnPropertyChanged(nameof(this.進行外枠色));
    }
}
