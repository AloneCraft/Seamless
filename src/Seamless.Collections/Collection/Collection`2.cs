namespace Seamless.Collections;

using Garnet.common;
using Tsavorite.core;
/// <summary>
/// Tsavorite をバックエンドに使うコレクションの抽象クラス
/// <see cref="IDictionary{TKey,TValue}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 静的ポリモーフィズムにより仮想ディスパッチを回避する。
/// </summary>
public abstract class Collection<TKey,TValue>:Collection<TKey,TValue,AddSessionFunctions,StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>>where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>{
    protected Collection(TsavoriteKV<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>,ObjectAllocator<StoreFunctions<SpanByteComparer,DefaultRecordDisposer>>> Store,long LogMemorySize=0,double MutableFraction=1.0):base(Store,new AddSessionFunctions(),LogMemorySize,MutableFraction){}
}
