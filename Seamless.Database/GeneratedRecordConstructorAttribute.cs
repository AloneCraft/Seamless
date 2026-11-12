namespace Seamless.Database;

/// <summary>生成レコードのコンストラクター引数に対応する公開プロパティ名。</summary>
[AttributeUsage(AttributeTargets.Constructor,Inherited=false)]
public sealed class GeneratedRecordConstructorAttribute(params string[] members):Attribute{
    public IReadOnlyList<string> Members{get;}=members;
}
