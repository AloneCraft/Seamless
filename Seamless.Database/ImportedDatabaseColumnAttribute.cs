namespace Seamless.Database;

/// <summary>取り込みとSQL出力の対応情報。手書きのネイティブ定義には必須としない。</summary>
[AttributeUsage(AttributeTargets.Property|AttributeTargets.Field,AllowMultiple=false)]
public sealed class ImportedDatabaseColumnAttribute(string name,int maximumLength=-1,string?sqlTypeName=null,string?collation=null):Attribute{
    public string Name{get;}=name;
    public int MaximumLength{get;}=maximumLength;
    public string?SqlTypeName{get;}=sqlTypeName;
    public string?Collation{get;}=collation;
    public bool IsSynthetic{get;init;}
    /// <summary>元の型の精度(decimal・numeric の桁数)。無ければ -1。SQL へ書き出すとき元の型に戻すのに使う。</summary>
    public int Precision{get;init;}=-1;
    /// <summary>元の型の小数部の桁数(decimal・numeric・time・datetime2・datetimeoffset)。無ければ -1。</summary>
    public int Scale{get;init;}=-1;
    /// <summary>型付きの xml の列が従う XML スキーマコレクションのスキーマ名。型の無い xml と xml 以外は null。</summary>
    public string?XmlSchemaCollectionSchema{get;init;}
    /// <summary>型付きの xml の列が従う XML スキーマコレクションの名前。</summary>
    public string?XmlSchemaCollectionName{get;init;}
    /// <summary>型付きの xml の列が文書(DOCUMENT)だけを持つ。false は断片も持てる(CONTENT)。</summary>
    public bool IsXmlDocument{get;init;}
}
