namespace Seamless.Execution.Codecs;

/// <summary>保持量の見積もりと、Dispose まで予約を保持する復元を提供する。</summary>
public interface IOwnedAdapter<T>:IAdapter<T>{
    long GetRetainedSize(T value);
    OwnedValue<T> DecodeOwned(ReadOnlySpan<byte> source);
}
