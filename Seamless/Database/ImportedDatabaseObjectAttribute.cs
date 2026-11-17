using Seamless.Collections.Tsavorite;
using Tsavorite.core;
namespace Seamless.Database;

/// <summary>
/// 取り込み元の実名。ネイティブ型の意味を変更する属性ではない。
/// Tables に宣言する行の型に付けると、DB の生成器がコンテナ(スキーマ)の表のプロパティへ同じ属性を写す(読む側は表のプロパティから読む)。
/// </summary>
[AttributeUsage(AttributeTargets.Property|AttributeTargets.Method|AttributeTargets.Struct,AllowMultiple=false)]
public sealed class ImportedDatabaseObjectAttribute(string schema,string name,string?sourceFeatures=null):Attribute{
    public string Schema{get;}=schema;
    public string Name{get;}=name;
    public string?SourceFeatures{get;}=sourceFeatures;
}
