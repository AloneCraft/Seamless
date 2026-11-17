using Seamless.Collections.Tsavorite;
using Tsavorite.core;
namespace Seamless.Database;

/// <summary>
/// 取り込んだビューの元の定義(SQL Server の CREATE VIEW)。取り込みの生成器が生成物のアセンブリに付ける。
/// 同じ製品の DB へ書き出すとき、ビューを元の定義のまま作り直すのに使う(訳せずに取り込まなかったビューも残す)。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly,AllowMultiple=true)]
public sealed class ImportedDatabaseViewAttribute(string schema,string name,string definition):Attribute{
    /// <summary>元の DB のスキーマ名。</summary>
    public string Schema{get;}=schema;
    /// <summary>元の DB のビュー名。</summary>
    public string Name{get;}=name;
    /// <summary>元の定義(CREATE VIEW の文)。</summary>
    public string Definition{get;}=definition;
}
