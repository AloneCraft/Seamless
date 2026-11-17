namespace Seamless.Database.Rdbms;

/// <summary>
/// C# のビューへ訳した取り込み元のビュー1つ。ビューは読み取りの入口(I&lt;スキーマ&gt;Reader)への拡張プロパティで、
/// <paramref name="DeclaringTypeName"/> の静的クラスの <paramref name="PropertyName"/> にある。
/// </summary>
/// <param name="SourceSchema">元の DB のスキーマ名。</param>
/// <param name="SourceName">元の DB のビュー名。</param>
/// <param name="SchemaPropertyName">読み取りの入口のスキーマのプロパティ名(生成器が付けた C# の名前)。</param>
/// <param name="DeclaringTypeName">拡張プロパティを持つ静的クラスの完全な名前(global:: なし)。</param>
/// <param name="PropertyName">拡張プロパティの名前。</param>
public sealed record GeneratedViewBinding(string SourceSchema,string SourceName,string SchemaPropertyName,string DeclaringTypeName,string PropertyName);
