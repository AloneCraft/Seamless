namespace Seamless.Database.Rdbms;

/// <summary>XML の列の形の決まり1つ(SQL Server の XML スキーマコレクション)。<paramref name="Definition"/> は決まりの本文(XML Schema の文書)。</summary>
public sealed record XmlSchemaCollectionCatalog(string Schema,string Name,string Definition);
