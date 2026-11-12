namespace Seamless.Database.Client;

/// <summary>Seamless生成formatterの、ヘッダーを持たない論理メンバー順。</summary>
[AttributeUsage(AttributeTargets.Struct,Inherited=false)]
public sealed class NativeWireLayoutAttribute(int version,params string[] members):Attribute{
    public int Version{get;}=version;
    public IReadOnlyList<string> Members{get;}=members;
}

/// <summary>検査済みのNative行を、副作用のない固定の復元処理で読み取る契約。</summary>
public interface INativeReplayDecoder<T> where T:INativeReplayDecoder<T>{
    static abstract T Decode(ReadOnlySpan<byte> payload);
}
