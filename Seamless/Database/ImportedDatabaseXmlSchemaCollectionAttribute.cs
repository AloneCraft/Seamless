using Seamless.Collections.Tsavorite;
using Tsavorite.core;
namespace Seamless.Database;

/// <summary>
/// 取り込んだ DB の XML スキーマコレクション(型付きの xml の列が従う形の決まり)。取り込みの生成器が生成物のアセンブリに付ける。
/// 同じ製品の DB へ書き出すとき、表より先に作り直すのに使う(列の型と、その列を使うトリガー・関数が同じ形の決まりを要る)。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly,AllowMultiple=true)]
public sealed class ImportedDatabaseXmlSchemaCollectionAttribute(string schema,string name,string definition):Attribute{
    /// <summary>元の DB のスキーマ名。</summary>
    public string Schema{get;}=schema;
    /// <summary>元の DB の XML スキーマコレクションの名前。</summary>
    public string Name{get;}=name;
    /// <summary>決まりの本文(XML Schema の文書。SQL Server の XML_SCHEMA_NAMESPACE の結果)。</summary>
    public string Definition{get;}=definition;
}
