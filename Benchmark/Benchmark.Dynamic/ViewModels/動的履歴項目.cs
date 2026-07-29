using CommunityToolkit.Mvvm.ComponentModel;

namespace Benchmark.Dynamic;

public sealed class 動的履歴項目:ObservableObject {
    private bool 削除済み状態か;

    public required string 実行ID{get;init;}
    public required DateTimeOffset 開始日時{get;init;}
    public required string 状態{get;init;}
    public required string 条件{get;init;}

    public bool 削除済みか {
        get=>this.削除済み状態か;
        set=>this.SetProperty(ref this.削除済み状態か,value);
    }
}
