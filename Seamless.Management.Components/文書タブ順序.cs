using System.Collections.ObjectModel;

namespace Seamless.Management.Components;

public sealed class 文書タブ順序{
    private readonly List<string> 一覧=[];
    public ReadOnlyCollection<string> 順序{get;}
    public string? 移動元ID{get;private set;}
    public string? 移動先ID{get;private set;}
    public bool 前か{get;private set;}
    public 文書タブ順序(IEnumerable<string> ID一覧){
        this.順序=this.一覧.AsReadOnly();
        foreach(var ID in ID一覧){
            if(!this.追加(ID)){throw new ArgumentException("文書IDが重複しています。",nameof(ID一覧));}
        }
    }
    public bool 追加(string ID){
        ArgumentException.ThrowIfNullOrWhiteSpace(ID);
        if(this.一覧.Contains(ID)){return false;}
        this.一覧.Add(ID);return true;
    }
    public bool 削除(string ID){
        if(ID==this.移動元ID||ID==this.移動先ID){this.取消();}
        return this.一覧.Remove(ID);
    }
    public void ドラッグ開始(string ID){
        if(!this.一覧.Contains(ID)){throw new ArgumentException("文書がありません。",nameof(ID));}
        this.取消();this.移動元ID=ID;
    }
    public void 移動先を設定(string ID,bool 前か){
        this.移動先ID=null;
        if(this.移動元ID is not null&&ID!=this.移動元ID&&this.一覧.Contains(ID)){
            this.移動先ID=ID;this.前か=前か;
        }
    }
    public bool ドロップ(){
        var 移動元=this.移動元ID;var 移動先=this.移動先ID;var 前=this.前か;this.取消();
        if(移動元 is null||移動先 is null){return false;}
        var 元位置=this.一覧.IndexOf(移動元);
        if(元位置<0||!this.一覧.Contains(移動先)){return false;}
        this.一覧.RemoveAt(元位置);
        var 位置=this.一覧.IndexOf(移動先);
        if(!前){位置++;}
        this.一覧.Insert(位置,移動元);return 元位置!=位置;
    }
    public void 取消(){this.移動元ID=null;this.移動先ID=null;this.前か=false;}
}
