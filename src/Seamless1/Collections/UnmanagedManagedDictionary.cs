namespace Seamless.Collections;

public sealed partial class UnmanagedManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> {
    /// <summary>
    /// todo 1.1.1 TKeyとTValueの片方が可変長でも可変長の扱いになっている。将来、TKeyの固定長が実装できた場合は書き直す
    /// </summary>
    private protected override TryAddResult PrivateTryAdd(TKey Key,TValue Value) => base.PrivateTryAdd(Key,Value);
}
