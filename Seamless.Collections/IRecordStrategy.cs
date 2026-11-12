namespace Seamless.Collections;

/// <summary>型へ固定した既定の正規化・比較・ハッシュ契約。</summary>
public interface IRecordStrategy<T>{
    static abstract string ContractId{get;}
    static abstract bool IsGraphFree{get;}
    static abstract T Normalize(T value);
    static abstract bool Equals(T first,T second);
    static abstract int GetHashCode(T value);
}
