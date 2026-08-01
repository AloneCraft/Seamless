using CommunityToolkit.Mvvm.ComponentModel;
using UI;

namespace Benchmark.Dynamic.UI;

public sealed class 動的CPU行:ObservableObject {
    private bool 実行中状態か;

    public required int 論理プロセッサインデックス{get;init;}
    public required CPUコア種別 コア種別{get;init;}
    public required int 物理コアインデックス{get;init;}
    public required string コアラベル{get;init;}

    public bool 実行中か {
        get=>this.実行中状態か;
        set {
            if(this.SetProperty(ref this.実行中状態か,value)) {
                this.OnPropertyChanged(nameof(this.実行中表示));
            }
        }
    }

    public string 実行中表示=>this.実行中か?"実行中":"";
}
