using Benchmark.Dynamic.Core;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Benchmark.Dynamic;

public sealed class 動的ソース結果画面モデル {
    private readonly ObservableCollection<動的結果行> 共有結果一覧;

    public 動的ソース結果画面モデル(SourceTarget 対象,ObservableCollection<動的結果行> 共有結果一覧) {
        this.対象=対象;
        this.共有結果一覧=共有結果一覧;
        this.行一覧=new ObservableCollection<動的結果行>(
            共有結果一覧.Where(行=>行.対象==対象));
        this.共有結果一覧.CollectionChanged+=this.共有結果変更時;
    }

    public SourceTarget 対象{get;}
    public ObservableCollection<動的結果行> 行一覧{get;}

    private void 共有結果変更時(object? 送信元,NotifyCollectionChangedEventArgs 引数) {
        if(引数.Action==NotifyCollectionChangedAction.Reset) {
            this.行一覧.Clear();
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
    }
}
