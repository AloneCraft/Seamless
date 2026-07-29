using Benchmark.Dynamic.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Benchmark.Dynamic.UI;

public sealed class 動的ソース選択項目:ObservableObject {
    private bool 選択済み状態か;

    public 動的ソース選択項目(SourceTarget 対象,string 経路,string ソース名,bool 選択済みか=false) {
        this.対象=対象;
        this.経路=経路;
        this.ソース名=ソース名;
        this.選択済み状態か=選択済みか;
    }

    public SourceTarget 対象{get;}
    public string 経路{get;}
    public string ソース名{get;}
    public string 表示名=>this.経路+" / "+this.ソース名;

    public bool 選択済みか {
        get=>this.選択済み状態か;
        set=>this.SetProperty(ref this.選択済み状態か,value);
    }
}
