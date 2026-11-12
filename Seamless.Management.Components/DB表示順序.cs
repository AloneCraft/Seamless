namespace Seamless.Management.Components;

public sealed class DB表示順序{
    private readonly List<string> 一覧=[];
    public string[] 保存値=>this.一覧.ToArray();
    public void 復元(string[] ID一覧){new ブラウザー表示状態(1,ID一覧,false,0).検証();this.一覧.Clear();this.一覧.AddRange(ID一覧);}
    public CatalogDatabase[] 適用(IEnumerable<CatalogDatabase> DB一覧){
        var 位置=this.一覧.Select((ID,index)=>(ID,index)).ToDictionary(項目=>項目.ID,項目=>項目.index,StringComparer.Ordinal);
        return DB一覧.Where(DB=>!DB.IsSystem).OrderBy(DB=>{if(位置.TryGetValue(DB.Id,out var index)){return index;}return int.MaxValue;}).ToArray();
    }
    public bool 移動(IEnumerable<CatalogDatabase> DB一覧,string ID,int 差){
        if(差 is not(-1 or 1)){throw new ArgumentOutOfRangeException(nameof(差));}
        var 表示=this.適用(DB一覧).Select(DB=>DB.Id).ToList();var 元=表示.IndexOf(ID);var 次=元+差;
        if(元<0||次<0||次>=表示.Count){return false;}
        (表示[元],表示[次])=(表示[次],表示[元]);
        this.表示順を保存(表示);return true;
    }
    public bool 対象位置へ移動(IEnumerable<CatalogDatabase> DB一覧,string ID,string 対象ID,bool 前か){
        var 表示=this.適用(DB一覧).Select(DB=>DB.Id).ToList();
        if(ID==対象ID||!表示.Contains(ID)||!表示.Contains(対象ID)){return false;}
        var 元順序=表示.ToArray();表示.Remove(ID);var 位置=表示.IndexOf(対象ID);if(!前か){位置++;}表示.Insert(位置,ID);
        if(表示.SequenceEqual(元順序)){return false;}
        this.表示順を保存(表示);return true;
    }
    private void 表示順を保存(List<string> 表示){
        var 表示中=new HashSet<string>(表示,StringComparer.Ordinal);var 残り=new Queue<string>(表示);var 新順序=new List<string>();
        foreach(var 保存ID in this.一覧){if(表示中.Contains(保存ID)){新順序.Add(残り.Dequeue());}else{新順序.Add(保存ID);}}
        新順序.AddRange(残り);this.復元(新順序.ToArray());
    }
    public void 既定順に戻す()=>this.一覧.Clear();
}
