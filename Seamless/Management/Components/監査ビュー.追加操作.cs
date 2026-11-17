namespace Seamless.Management.Components;

public partial class 監査ビュー{
    private readonly HashSet<string> 追加条件=new(StringComparer.Ordinal);
    private コマンド登録<string>? 追加登録;
    private コマンド登録<string> 追加コマンド=>this.追加登録??=new([
        new("database","データベース",_=>!this.条件表示か("database"),_=>!this.読込中か,_=>this.条件追加Async("database"),["database","DB"],アイコン:"DB"),
        new("user","利用者",_=>!this.条件表示か("user"),_=>!this.読込中か,_=>this.条件追加Async("user"),["user","利用者"],アイコン:"利用者"),
        new("period","期間 (UTC)",_=>!this.条件表示か("period"),_=>!this.読込中か,_=>this.条件追加Async("period"),["date","time","日時"],アイコン:"監査"),
        new("action","操作",_=>!this.条件表示か("action"),_=>!this.読込中か,_=>this.条件追加Async("action"),["action","操作"],アイコン:"監査"),
        new("outcome","結果",_=>!this.条件表示か("outcome"),_=>!this.読込中か,_=>this.条件追加Async("outcome"),["outcome","結果"],アイコン:"確認")
    ]);
    private bool 条件表示か(string ID)=>this.追加条件.Contains(ID)||(ID switch{
        "database"=>this.DB条件.Length>0,"user"=>this.利用者条件.Length>0,"period"=>this.開始日時 is not null||this.終了日時 is not null,"action"=>this.選択操作 is not null,"outcome"=>this.選択結果 is not null,_=>false
    });
    private async Task 条件追加Async(string ID){this.追加条件.Add(ID);if(this.表示種類==監査種別.転記状態){await this.表示変更時Async(監査種別.イベント);}}
    public async Task 検索条件追加Async(){await this.条件追加Async("database");await this.InvokeAsync(this.StateHasChanged);}
}
