namespace Seamless.SessionFunctions;
/// <summary>
/// UnmaagedCollection 用セッション関数。UnmaagedRead は Read でのみ使われる。
/// </summary>
public readonly partial struct UnmaagedRead<TKey,TValue>:ICreate<UnmaagedRead<TKey,TValue>>
    where TKey:unmanaged,IEquatable<TKey>
    where TValue:unmanaged,IEquatable<TValue> {
    public static UnmaagedRead<TKey,TValue> Create()=>new();
}