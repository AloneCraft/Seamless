namespace Seamless.Management.Components;

public sealed record ブラウザー保存文書(string DatabaseId,long DatabaseVersion,string DatabaseArtifactHash,string DefinitionId,long Version,string ArtifactHash,string DefinitionHash,string Kind,string ObjectId,string Schema,string View){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]public bool PublishedOnly{get;init;}
    public void 検証(){
        new ブラウザー表示状態(1,[this.DatabaseId],false,0).検証();
        if(this.DatabaseVersion is <1 or >9007199254740991||this.Version is <1 or >9007199254740991||!ハッシュか(this.DatabaseArtifactHash)||!ハッシュか(this.ArtifactHash)||!文字列か(this.DefinitionId,128,false)||!文字列か(this.ObjectId,4096,true)||!文字列か(this.Schema,4096,true)||this.Kind is not("Database" or "Schema" or "Table" or "View" or "ScalarFunction" or "TableFunction" or "RegisteredFunction")){throw new InvalidOperationException("保存された文書参照が不正です。");}
        if(this.PublishedOnly){if(this.DefinitionHash!=""||this.View is not("データ" or "DB情報")){throw new InvalidOperationException("保存された公開文書参照が不正です。");}}
        else if(!ハッシュか(this.DefinitionHash)){throw new InvalidOperationException("保存された定義ハッシュが不正です。");}
        if(this.Kind=="Database"&&(this.ObjectId.Length!=0||this.Schema.Length!=0)||this.Kind=="Schema"&&(this.ObjectId.Length!=0||this.Schema.Length==0)||this.Kind is not("Database" or "Schema")&&this.ObjectId.Length==0){throw new InvalidOperationException("保存された対象参照が不正です。");}
        var 許可表示=new[]{"データ","構造","C#","実行計画","実行履歴"};
        if(this.Kind is "Database" or "Schema"){許可表示=["DB情報"];}
        else if(this.Kind=="Table"){許可表示=["データ","構造","C#","アクセス履歴"];}
        else if(this.Kind=="Set"){許可表示=["データ"];}
        if(!許可表示.Contains(this.View,StringComparer.Ordinal)){throw new InvalidOperationException("保存された文書表示が不正です。");}
    }
    internal bool DBが一致するか(CatalogDatabase DB)=>!DB.IsSystem&&this.DatabaseId==DB.Id&&this.DatabaseVersion==DB.Version&&this.DatabaseArtifactHash==DB.ArtifactHash;
    internal bool 定義が一致するか(文書状態 文書,long 版,string 成果物)=>!文書.未保管か&&this.DefinitionId==文書.定義.Id&&this.Version==版&&this.ArtifactHash==成果物&&this.DefinitionHash==文書.定義.Hash()&&文書.基準版==版;
    private static bool ハッシュか(string? 値)=>値 is{Length:64}&&値.All(Uri.IsHexDigit);
    private static bool 文字列か(string? 値,int 上限,bool 空を許可するか)=>値 is not null&&値.Length<=上限&&!値.Any(char.IsControl)&&(空を許可するか||!string.IsNullOrWhiteSpace(値));
}
