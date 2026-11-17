using System.Text;

namespace Seamless.Management.Components;

public sealed record ブラウザー表示状態(int Format,string[] OpenDatabaseIds,bool SystemExpanded,double ScrollTop){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public double? SidebarWidth{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public bool? SidebarCollapsed{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public string[]? DatabaseOrder{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public ブラウザー保存文書[]? Documents{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public int? SelectedDocument{get;init;}
    public void 検証(){
        if(this.Format is not(1 or 2 or 3 or 4 or 5)||this.OpenDatabaseIds is null||this.OpenDatabaseIds.Length>256||!double.IsFinite(this.ScrollTop)||this.ScrollTop is <0 or >10000000||this.OpenDatabaseIds.Any(ID=>string.IsNullOrWhiteSpace(ID)||!ID.StartsWith("db:",StringComparison.Ordinal)||ID.Length is <4 or >131||ID.Any(char.IsControl))||this.OpenDatabaseIds.Distinct(StringComparer.Ordinal).Count()!=this.OpenDatabaseIds.Length){throw new InvalidOperationException("保存された表示状態が不正です。");}
        if(this.Format==1&&(this.SidebarWidth is not null||this.SidebarCollapsed is not null)||this.Format>=2&&(this.SidebarWidth is null||!double.IsFinite(this.SidebarWidth.Value)||this.SidebarWidth is <200 or >600||this.SidebarCollapsed is null)){throw new InvalidOperationException("保存されたサイドバー配置が不正です。");}
        if(this.Format<3&&this.DatabaseOrder is not null||this.Format>=3&&this.DatabaseOrder is null){throw new InvalidOperationException("保存された表示順が不正です。");}
        if(this.DatabaseOrder is{} 順序){new ブラウザー表示状態(1,順序,false,0).検証();}
        if(this.Format<4&&(this.Documents is not null||this.SelectedDocument is not null)||this.Format>=4&&(this.Documents is null||this.Documents.Length>32||this.SelectedDocument is{} 選択&&(選択<0||選択>=this.Documents.Length))){throw new InvalidOperationException("保存された文書一覧が不正です。");}
        if(this.Documents is{} 文書一覧){
            foreach(var 文書 in 文書一覧){if(文書 is null||this.Format<5&&文書.PublishedOnly){throw new InvalidOperationException("保存された文書参照が不正です。");}文書.検証();}
            if(文書一覧.Select(文書=>(文書.DatabaseId,文書.DefinitionId,文書.Kind,文書.ObjectId,文書.Schema)).Distinct().Count()!=文書一覧.Length){throw new InvalidOperationException("保存された文書参照が重複しています。");}
        }
    }
    public static string 保存キー(Guid インスタンスID,UserInfo? 利用者){
        if(インスタンスID==Guid.Empty||利用者 is null||利用者.Disabled||string.IsNullOrWhiteSpace(利用者.Name)||利用者.Name.Length>256||利用者.Name.Any(char.IsControl)){throw new InvalidOperationException("表示状態の保存には認証済み利用者とインスタンスIDが必要です。");}
        return "Seamless.Manager.Sidebar.v1."+インスタンスID.ToString("N")+"."+Convert.ToBase64String(Encoding.UTF8.GetBytes(利用者.Name));
    }
}
