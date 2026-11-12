namespace Seamless.Database;

/// <summary>取り込みとSQL出力の対応情報。手書きのネイティブ定義には必須としない。</summary>
[AttributeUsage(AttributeTargets.Property|AttributeTargets.Field,AllowMultiple=false)]
public sealed class ImportedDatabaseColumnAttribute(string name,int maximumLength=-1,string?sqlTypeName=null,string?collation=null):Attribute{
    public string Name{get;}=name;
    public int MaximumLength{get;}=maximumLength;
    public string?SqlTypeName{get;}=sqlTypeName;
    public string?Collation{get;}=collation;
    public bool IsSynthetic{get;init;}
}
