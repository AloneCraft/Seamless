namespace Seamless.Database;

/// <summary>宣言型から生成する主キーの構成プロパティを指定する。</summary>
[AttributeUsage(AttributeTargets.Property,AllowMultiple=false,Inherited=false)]
public sealed class KeyAttribute:Attribute;
