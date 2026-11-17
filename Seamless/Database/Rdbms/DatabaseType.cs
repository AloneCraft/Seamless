namespace Seamless.Database.Rdbms;

public sealed record DatabaseType(
    DatabaseTypeKind Kind,
    bool IsNullable,
    int? MaximumLength=null,
    byte? Precision=null,
    byte? Scale=null,
    global::System.Type? RuntimeType=null,
    string? SqlTypeName=null,
    string? Collation=null,
    short? SourceMaximumLength=null,
    byte? SourcePrecision=null,
    byte? SourceScale=null,
    string? ResultSqlTypeName=null,
    int? ResultMaximumLength=null){
    /// <summary>型付きの xml の列が従う XML スキーマコレクションのスキーマ名。型の無い xml と xml 以外は null。</summary>
    public string? XmlSchemaCollectionSchema{get;init;}
    /// <summary>型付きの xml の列が従う XML スキーマコレクションの名前。</summary>
    public string? XmlSchemaCollectionName{get;init;}
    /// <summary>型付きの xml の列が文書(DOCUMENT、根の要素が1つ)だけを持つ。false は断片も持てる(CONTENT)。</summary>
    public bool IsXmlDocument{get;init;}
}
