namespace Seamless.Database.Client;

/// <summary>Seamless生成formatterの、ヘッダーを持たない論理メンバー順。</summary>
[AttributeUsage(AttributeTargets.Struct,Inherited=false)]
public sealed class NativeWireLayoutAttribute(int version,params string[] members):Attribute{
    public int Version{get;}=version;
    public IReadOnlyList<string> Members{get;}=members;
}
