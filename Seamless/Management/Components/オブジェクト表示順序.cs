namespace Seamless.Management.Components;

/// <summary>一つのDB画面内だけで使う表示順。宣言や保存データの順序には適用しない。</summary>
public sealed class オブジェクト表示順序{
    private readonly Dictionary<(string 所属,string 分類),string[]> 順序=[];
    public bool 変更ありか=>this.順序.Count>0;
    public static string 分類(string 種類)=>種類 switch{
        "ScalarFunction" or "TableFunction" or "RegisteredFunction"=>"Function",
        "SystemProperty"=>"SystemView",
        _=>種類
    };
    public CatalogObject[] 適用(IEnumerable<CatalogObject> 項目,string 所属,string 種類){
        var 一覧=項目.Where(項目=>項目.Schema==所属&&分類(項目.Kind)==種類).ToArray();
        if(!this.順序.TryGetValue((所属,種類),out var 順序)){return 一覧;}
        var 位置=順序.Select((ID,番号)=>(ID,番号)).ToDictionary(項目=>項目.ID,項目=>項目.番号,StringComparer.Ordinal);
        return 一覧.OrderBy(項目=>位置.GetValueOrDefault(項目.Id,int.MaxValue)).ToArray();
    }
    public bool 移動できるか(IEnumerable<CatalogObject> 項目,string ID,int 差){
        var 配列=項目.ToArray();var 対象=配列.SingleOrDefault(項目=>項目.Id==ID);if(対象 is null){return false;}
        var 一覧=this.適用(配列,対象.Schema,分類(対象.Kind));var 位置=Array.FindIndex(一覧,項目=>項目.Id==ID);
        return 差 is -1 or 1&&位置>=0&&位置+差>=0&&位置+差<一覧.Length;
    }
    public bool 移動(IEnumerable<CatalogObject> 項目,string ID,int 差){
        var 配列=項目.ToArray();if(!this.移動できるか(配列,ID,差)){return false;}
        var 対象=配列.Single(項目=>項目.Id==ID);var 種類=分類(対象.Kind);var 一覧=this.適用(配列,対象.Schema,種類);var 位置=Array.FindIndex(一覧,項目=>項目.Id==ID);
        (一覧[位置],一覧[位置+差])=(一覧[位置+差],一覧[位置]);this.順序[(対象.Schema,種類)]=一覧.Select(項目=>項目.Id).ToArray();return true;
    }
    public bool 対象位置へ移動(IEnumerable<CatalogObject> 項目,string 所属,string 種類,string ID,string 対象ID,bool 前か){
        var 一覧=this.適用(項目,所属,種類).Select(項目=>項目.Id).ToList();
        if(ID==対象ID||!一覧.Contains(ID,StringComparer.Ordinal)||!一覧.Contains(対象ID,StringComparer.Ordinal)){return false;}
        一覧.Remove(ID);var 位置=一覧.IndexOf(対象ID);if(!前か){位置++;}一覧.Insert(位置,ID);this.順序[(所属,種類)]=一覧.ToArray();return true;
    }
    public void 既定順に戻す()=>this.順序.Clear();
    public void 既定順に戻す(IEnumerable<CatalogObject> 項目,string ID){var 対象=項目.SingleOrDefault(項目=>項目.Id==ID);if(対象 is not null){this.順序.Remove((対象.Schema,分類(対象.Kind)));}}
}
