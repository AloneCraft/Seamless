using Seamless.Collections.Tsavorite;
using Tsavorite.core;
namespace Seamless.Database;

/// <summary>
/// 取り込んだ関数・手続きの元の定義(SQL Server の CREATE FUNCTION・CREATE PROCEDURE)。取り込みの生成器が生成物のアセンブリに付ける。
/// 同じ製品の DB へ書き出すとき、元の定義のまま作り直すのに使う(計算列・既定値・CHECK が使う関数を、表より先に作る)。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly,AllowMultiple=true)]
public sealed class ImportedDatabaseRoutineAttribute(string schema,string name,string definition):Attribute{
    /// <summary>元の DB のスキーマ名。</summary>
    public string Schema{get;}=schema;
    /// <summary>元の DB の関数・手続きの名前。</summary>
    public string Name{get;}=name;
    /// <summary>元の定義(CREATE FUNCTION・CREATE PROCEDURE の文)。</summary>
    public string Definition{get;}=definition;
}
