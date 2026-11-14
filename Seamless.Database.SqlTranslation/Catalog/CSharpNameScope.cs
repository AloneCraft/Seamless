namespace Seamless.Database.SqlTranslation;
/// <summary>
/// 同じ入れ物の中で C# の名前がぶつからないように付ける(大文字小文字は区別しない)。
/// ぶつかったら <c>名前_ハッシュ8桁</c> にする。取込の生成器(Seamless.Database.Rdbms の CSharpIdentifierScope)と同じ規則。
/// 例: 表 EmailAddress の列 EmailAddress は、型の名前を予約するので EmailAddress_58675888 になる。
/// </summary>
public sealed class CSharpNameScope{
    private readonly Dictionary<string,string> names=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,string> originals=new(StringComparer.Ordinal);
    /// <summary>その名前を使わせない(囲む型の名前など)。</summary>
    public void Reserve(string identifier)=>this.names.TryAdd(identifier,"\\0"+identifier);
    /// <summary>SQL の名前に、この入れ物で一意な C# の名前を付ける。同じ名前には同じ結果を返す。</summary>
    public string Get(string original){
        if(this.originals.TryGetValue(original,out var existing)){
            return existing;
        }
        var normalized=CSharpNames.Identifier(original);
        var candidate=normalized;
        if(this.names.TryGetValue(normalized,out var owner)&&owner!=original){
            candidate=$"{normalized}_{CSharpNames.HashSuffix(original)}";
            while(this.names.ContainsKey(candidate)){
                candidate=$"{candidate}_{CSharpNames.HashSuffix(candidate)}";
            }
        }
        this.names.Add(candidate,original);
        this.originals.Add(original,candidate);
        return candidate;
    }
}
