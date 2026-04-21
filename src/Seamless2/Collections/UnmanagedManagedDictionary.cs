namespace Seamless.Collections;

public sealed partial class UnmanagedManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> {
    /// <summary>
    /// todo 2.0beta3 TKeyとTValueの片方が可変長でも可変長の扱いになっている。将来、TKeyの固定長が実装できた場合は書き直す
    /// </summary>
    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value) => base.PrivateTryAdd(key,value);
}