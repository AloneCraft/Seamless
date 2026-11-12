namespace Seamless.Management.Components;

public sealed class ツリー展開状態{
    public bool 展開中か{get;set;}
    public オブジェクト表示順序 表示順序{get;}=new();
    private readonly HashSet<string> 開いたスキーマ=new(StringComparer.Ordinal);
    private readonly HashSet<(string 所属,string 種類)> 開いた分類=[];
    private readonly HashSet<string> 開いたテーブル=new(StringComparer.Ordinal);

    public bool テーブル展開中か(string ID)=>this.開いたテーブル.Contains(ID);
    public void テーブルを開閉(string ID,bool 展開か){if(展開か){this.開いたテーブル.Add(ID);}else{this.開いたテーブル.Remove(ID);}}

    public bool スキーマ展開中か(string 名前)=>this.開いたスキーマ.Contains(名前);
    public void スキーマを開閉(string 名前,bool 展開か){
        if(展開か){this.開いたスキーマ.Add(名前);}else{this.開いたスキーマ.Remove(名前);}
    }
    public bool 分類展開中か(string 所属,string 種類)=>this.開いた分類.Contains((所属,種類));
    public void 分類を開閉(string 所属,string 種類,bool 展開か){
        if(展開か){this.開いた分類.Add((所属,種類));}else{this.開いた分類.Remove((所属,種類));}
    }
}
