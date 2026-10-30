namespace Seamless.Execution.Codecs;

/// <summary>予算管理された呼び出し側に、決定的な符号化と形式IDを提供する。</summary>
public interface IStrategy<TSelf,T> where TSelf:struct,IStrategy<TSelf,T>{
    static abstract string FormatId(in TSelf self);
    static abstract int GetEncodedLength(ref TSelf self,T value);
    static abstract void Encode(ref TSelf self,T value,Span<byte> destination);
    static abstract T Decode(ref TSelf self,ReadOnlySpan<byte> source);
}

/// <summary>実行時に決まる型とレジストリに接続する入口。</summary>
public interface IAdapter<T>{
    string FormatId{get;}
    int GetEncodedLength(T value);
    void Encode(T value,Span<byte> destination);
    T Decode(ReadOnlySpan<byte> source);
}
