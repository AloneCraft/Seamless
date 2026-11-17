namespace Seamless.Database.Rdbms;

public sealed record DatabaseCatalog(string Name,IReadOnlyList<SchemaCatalog> Schemas,string? DefaultCollation=null){
    /// <summary>XML の列の形の決まり(SQL Server の XML スキーマコレクション)。型付きの xml の列が名前で指す。</summary>
    public IReadOnlyList<XmlSchemaCollectionCatalog> XmlSchemaCollections{get;init;}=[];
}
