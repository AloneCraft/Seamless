namespace Seamless.Database;

/// <summary>取り込み元の実名。ネイティブ型の意味を変更する属性ではない。</summary>
[AttributeUsage(AttributeTargets.Property|AttributeTargets.Method,AllowMultiple=false)]
public sealed class ImportedDatabaseObjectAttribute(string schema,string name,string?sourceFeatures=null):Attribute{
    public string Schema{get;}=schema;
    public string Name{get;}=name;
    public string?SourceFeatures{get;}=sourceFeatures;
}
